const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const seed = fs.readFileSync(path.join(__dirname, '..', 'server', 'seed.js'), 'utf8');

test('量子霍尔种子内容区分 R_xy 与 R_K', () => {
  assert.match(seed, /R_\{xy\}=\\\\frac\{h\}\{\\\\nu e\^2\}/);
  assert.match(seed, /R_K=\\\\frac\{h\}\{e\^2\}/);
  assert.doesNotMatch(seed, /量子霍尔效应中霍尔电阻平台 R_K = h\/\(νe²\)/);
});

test('运动电动势种子任务不把磁场扫描写成流速扫描', () => {
  assert.match(seed, /E=B·D·v/);
  assert.match(seed, /不把改变磁场误认为改变流速/);
  assert.doesNotMatch(seed, /改变励磁电流（对应不同流速下的感应信号）/);
});
