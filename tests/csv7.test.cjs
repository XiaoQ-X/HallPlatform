const { test } = require('node:test');
const assert = require('node:assert');
const {
  parse7, summarize, markOutliers, cleanRows, to7, TEMPLATE, FIELDS
} = require('../shared/csv7.cjs');

const header = 'Timestamp,Operation_Type,Is_Stable,Work_Current_Is(mA),Excitation_Current_Im(A),Measured_Voltage_Vh(mV),Is_Valid_Record';
function row(ts, op, stable, is, im, vh, valid) {
  return [ts, op, stable, is, im, vh, valid].join(',');
}
const sample = [
  header,
  row('2026-09-20 10:00:00', '+I+B', 'True', 5.0, 0.5, 10.2, 'True'),
  row('2026-09-20 10:01:00', '+I-B', 'True', 5.0, 0.5, -10.1, 'True'),
  row('2026-09-20 10:02:00', '-I+B', 'False', 5.0, 0.5, 10.3, 'False'),
  row('2026-09-20 10:03:00', '-I-B', '1', 5.0, 0.5, -10.2, '1')
].join('\n');

test('标准 7 列 CSV 解析为正确类型', () => {
  const p = parse7(sample);
  assert.strictEqual(p.rows.length, 4);
  assert.strictEqual(p.rows[0].Is_Stable, true);
  assert.strictEqual(p.rows[0].Work_Current_Is_mA, 5.0);
  assert.strictEqual(p.rows[0].Measured_Voltage_Vh_mV, 10.2);
  assert.strictEqual(p.rows[1].Is_Valid_Record, true);
  assert.strictEqual(p.rows[2].Is_Valid_Record, false);
  assert.strictEqual(p.rows[3].Is_Stable, true); // '1' 识别为 true
  assert.strictEqual(p.errors.length, 0);
});

test('表头大小写/空格/列名变体可识别', () => {
  const variant = ' timestamp , operation type , is stable , work current is (ma) , excitation current im (a) , measured voltage vh (mv) , is valid record \n'
    + 't,op,yes,5,0.5,10,y';
  const p = parse7(variant);
  assert.strictEqual(p.rows[0].Work_Current_Is_mA, 5);
  assert.strictEqual(p.rows[0].Is_Valid_Record, true);
});

test('空内容抛错', () => {
  assert.throws(() => parse7(''), /空/);
  assert.throws(() => parse7(null), /空/);
  assert.throws(() => parse7('   \n \n'), /空/);
});

test('缺列与列数不足报错', () => {
  const bad = 'Timestamp,Operation_Type,Is_Stable,Work_Current_Is(mA)\n1,op,true,5';
  const p = parse7(bad);
  assert.ok(p.errors.some(e => e.includes('列数不足') || e.includes('无法识别')));
});

test('非法布尔与非数值逐行报错', () => {
  const bad = header + '\n' + 't,op,maybe,abc,0.5,xyz,yes';
  const p = parse7(bad);
  assert.ok(p.errors.some(e => e.includes('Is_Stable')));
  assert.ok(p.errors.some(e => e.includes('工作电流')));
  assert.ok(p.errors.some(e => e.includes('测量电压')));
});

test('summarize 统计口径正确', () => {
  const s = summarize(parse7(sample));
  assert.deepStrictEqual(
    { total: s.total, valid: s.valid, stable: s.stable, invalid: s.invalid },
    { total: 4, valid: 3, stable: 3, invalid: 1 }
  );
});

test('MAD 稳健离群：明显离群点被标记，正常值不标记', () => {
  const vals = [10, 10.1, 9.9, 10.0, 10.2, 100];
  const rows = vals.map(v => ({ Measured_Voltage_Vh_mV: v, Is_Valid_Record: true }));
  const flags = markOutliers(rows, 3.5);
  assert.ok(flags.get(rows[5]));
  assert.ok(!flags.get(rows[0]));
});

test('MAD 按操作条件分组：同组离群被标记，跨组正常差异不干扰', () => {
  const rows = [
    { Operation_Type: 'A', Measured_Voltage_Vh_mV: 10.0, Is_Valid_Record: true },
    { Operation_Type: 'A', Measured_Voltage_Vh_mV: 10.1, Is_Valid_Record: true },
    { Operation_Type: 'A', Measured_Voltage_Vh_mV: 9.9, Is_Valid_Record: true },
    { Operation_Type: 'A', Measured_Voltage_Vh_mV: 50.0, Is_Valid_Record: true },
    { Operation_Type: 'B', Measured_Voltage_Vh_mV: -10.0, Is_Valid_Record: true },
    { Operation_Type: 'B', Measured_Voltage_Vh_mV: -10.1, Is_Valid_Record: true },
    { Operation_Type: 'B', Measured_Voltage_Vh_mV: -9.9, Is_Valid_Record: true }
  ];
  const flags = markOutliers(rows);
  assert.ok(flags.get(rows[3]));   // A 组 50 为离群
  assert.ok(!flags.get(rows[0]));  // 正常值不标记
});

test('MAD 为 0（分辨率下限）时不判定离群', () => {
  const rows = [10, 10, 10, 10].map(v => ({ Measured_Voltage_Vh_mV: v }));
  assert.strictEqual(markOutliers(rows).size, 0);
});

test('cleanRows 过滤无效记录与离群点', () => {
  const vals = [10, 10.1, 9.9, 10.0, 10.2, 100];
  const rows = vals.map(v => ({ Measured_Voltage_Vh_mV: v, Is_Valid_Record: true }));
  const parsed = { rows };
  const kept = cleanRows(parsed, { useMad: true });
  assert.strictEqual(kept.length, 5);
  assert.ok(!kept.some(r => r.Measured_Voltage_Vh_mV === 100));
});

test('to7 与 parse 往返一致', () => {
  const p = parse7(sample);
  const out = to7(p.rows.filter(r => r.Is_Valid_Record));
  const again = parse7(out);
  assert.strictEqual(again.rows.length, 3);
  assert.strictEqual(again.rows[0].Measured_Voltage_Vh_mV, 10.2);
  assert.strictEqual(again.rows[2].Measured_Voltage_Vh_mV, -10.2);
});

test('模板为 7 列表头', () => {
  assert.strictEqual(TEMPLATE.trim(), FIELDS.join(','));
  assert.strictEqual(FIELDS.length, 7);
});
