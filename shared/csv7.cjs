/* 统一 7 列 CSV（与「霍尔效应数据工作台」互通）纯函数模块：
   解析校验、汇总、MAD 稳健离群、清洗、序列化。教学/数据处理用途，不做真实仪器标定。 */
'use strict';

const FIELDS = [
  'Timestamp',
  'Operation_Type',
  'Is_Stable',
  'Work_Current_Is(mA)',
  'Excitation_Current_Im(A)',
  'Measured_Voltage_Vh(mV)',
  'Is_Valid_Record'
];
const TEMPLATE = FIELDS.join(',') + '\n';

// 处理引号内逗号的单行切分
function splitCsvLine(line) {
  const out = [];
  let cur = '', inQ = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (inQ) {
      if (ch === '"') {
        if (line[i + 1] === '"') { cur += '"'; i++; } else { inQ = false; }
      } else { cur += ch; }
    } else if (ch === '"') {
      inQ = true;
    } else if (ch === ',') {
      out.push(cur); cur = '';
    } else {
      cur += ch;
    }
  }
  out.push(cur);
  return out;
}

function parseBool(v) {
  if (v == null) return null;
  const s = String(v).trim().toLowerCase();
  if (['true', '1', 'y', 'yes', '是', 't'].includes(s)) return true;
  if (['false', '0', 'n', 'no', '否', 'f'].includes(s)) return false;
  return null;
}

function toNumber(v) {
  if (v == null || String(v).trim() === '') return null;
  const n = Number(String(v).trim());
  return Number.isFinite(n) ? n : NaN;
}

function mapHeader(headers) {
  const norm = headers.map(h => String(h).toLowerCase().replace(/\s+/g, ''));
  const find = (...kw) => {
    for (let i = 0; i < norm.length; i++) {
      if (kw.some(k => norm[i].includes(k))) return i;
    }
    return -1;
  };
  return {
    time: find('timestamp'),
    op: find('operation', 'type'),
    stable: find('stable'),
    is: find('work_current', 'is('),
    im: find('excitation', 'im('),
    vh: find('voltage', 'vh'),
    valid: find('valid')
  };
}

function parse7(text) {
  if (text == null || String(text).trim() === '') {
    throw new Error('CSV 内容为空');
  }
  const lines = String(text).replace(/^﻿/, '').split(/\r?\n/).filter(l => l.trim() !== '');
  if (lines.length < 1) {
    throw new Error('CSV 内容为空');
  }
  const header = splitCsvLine(lines[0]).map(h => h.trim());
  const indexMap = mapHeader(header);
  const errors = [];
  Object.entries(indexMap).forEach(([k, v]) => {
    if (v < 0) errors.push('表头缺少或无法识别「' + k + '」对应列');
  });
  if (header.length < 7) errors.push('表头列数不足 7 列（实际 ' + header.length + ' 列）');

  const at = (cells, name) => (indexMap[name] >= 0 ? cells[indexMap[name]] : '');
  const rows = [];
  for (let i = 1; i < lines.length; i++) {
    const cells = splitCsvLine(lines[i]);
    const stable = parseBool(at(cells, 'stable'));
    const valid = parseBool(at(cells, 'valid'));
    const is = toNumber(at(cells, 'is'));
    const im = toNumber(at(cells, 'im'));
    const vh = toNumber(at(cells, 'vh'));
    const row = {
      _line: i + 1,
      Timestamp: at(cells, 'time').trim(),
      Operation_Type: at(cells, 'op').trim(),
      Is_Stable: stable,
      Work_Current_Is_mA: is,
      Excitation_Current_Im_A: im,
      Measured_Voltage_Vh_mV: vh,
      Is_Valid_Record: valid,
      _errors: []
    };
    if (stable == null) row._errors.push('Is_Stable 不是有效布尔值');
    if (valid == null) row._errors.push('Is_Valid_Record 不是有效布尔值');
    if (Number.isNaN(is)) row._errors.push('工作电流不是数值');
    if (Number.isNaN(im)) row._errors.push('励磁电流不是数值');
    if (Number.isNaN(vh)) row._errors.push('测量电压不是数值');
    if (row._errors.length) errors.push('第 ' + (i + 1) + ' 行：' + row._errors.join('、'));
    rows.push(row);
  }
  return { header, indexMap, rows, errors };
}

function summarize(parsed) {
  const rows = parsed.rows;
  const total = rows.length;
  const valid = rows.filter(r => r.Is_Valid_Record === true).length;
  const stable = rows.filter(r => r.Is_Stable === true).length;
  const invalid = rows.filter(r => r.Is_Valid_Record === false).length;
  const nonNumeric = rows.filter(r => r._errors.some(e => e.includes('数值'))).length;
  const malformedBool = rows.filter(r => r._errors.some(e => e.includes('布尔'))).length;
  return { total, valid, stable, invalid, nonNumeric, malformedBool };
}

function median(values) {
  const a = values.slice().sort((x, y) => x - y);
  const n = a.length;
  if (n === 0) return NaN;
  const mid = Math.floor(n / 2);
  return n % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

// 在一组同条件读数上做 MAD 稳健离群（口径 1.4826·MAD），命中写入 flags
function markWithin(group, flags, k) {
  const vals = group.map(r => r.Measured_Voltage_Vh_mV).filter(Number.isFinite);
  if (vals.length < 3) return; // 同条件重复不足 3 次，不判定
  const med = median(vals);
  const mad = median(vals.map(v => Math.abs(v - med)));
  if (mad < 1e-12) return; // MAD 为 0（分辨率下限），不判定
  const threshold = k * 1.4826 * mad;
  group.forEach(r => {
    const v = r.Measured_Voltage_Vh_mV;
    if (Number.isFinite(v) && Math.abs(v - med) > threshold) flags.set(r, true);
  });
}

// 返回需要排除的行集合；默认按 Operation_Type（操作条件）分组，
// 避免不同方向/条件的正常差异把 MAD 放大而漏检
function markOutliers(rows, k = 3.5, groupBy = true) {
  const flags = new Map();
  if (!groupBy) {
    markWithin(rows, flags, k);
    return flags;
  }
  const groups = new Map();
  rows.forEach(r => {
    const key = r.Operation_Type || '__';
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(r);
  });
  groups.forEach(g => markWithin(g, flags, k));
  return flags;
}

function cleanRows(parsed, options = {}) {
  let rows = parsed.rows.filter(r => r.Is_Valid_Record === true);
  if (options.useMad) {
    const outliers = markOutliers(rows, options.k, options.groupBy !== false);
    rows = rows.filter(r => !outliers.get(r));
  }
  return rows;
}

function escCsv(v) {
  v = v == null ? '' : String(v);
  return /[",\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v;
}

function to7(rows) {
  const lines = [FIELDS.join(',')];
  rows.forEach(r => {
    lines.push([
      r.Timestamp,
      r.Operation_Type,
      r.Is_Stable,
      r.Work_Current_Is_mA,
      r.Excitation_Current_Im_A,
      r.Measured_Voltage_Vh_mV,
      r.Is_Valid_Record
    ].map(escCsv).join(','));
  });
  return lines.join('\n');
}

module.exports = {
  FIELDS, TEMPLATE, parse7, summarize, median,
  markOutliers, cleanRows, to7
};
