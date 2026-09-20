const test=require('node:test'),assert=require('node:assert/strict'),{sideEffects,correctByReversal}=require('../shared/calculations.cjs');
const base={I:0.005,B:0.4,RH:0.01,d:5e-4};
const approx=(a,b,eps=1e-12)=>assert.ok(Math.abs(a-b)<=eps,`${a} !≈ ${b}`);

test('零磁场：VH、VE 为 0，V0 保留',()=>{
  const r=sideEffects({...base,B:0,V0:0.002});
  assert.equal(r.VH,0);assert.equal(r.VE,0);approx(r.V0,0.002);approx(r.Vmeasured,0.002);
});
test('零电流：VH、V0、VE 为 0，固定零点保留',()=>{
  const r=sideEffects({...base,I:0,V0:0.002,ettinghausenCoeff:0.005,extraVoltage:0.001});
  assert.equal(r.VH,0);assert.equal(r.V0,0);assert.equal(r.VE,0);approx(r.Vmeasured,0.001);
});
test('只有 V0（B=0，直接给定）',()=>{
  const r=sideEffects({...base,B:0,V0:0.003});
  assert.equal(r.VH,0);assert.equal(r.VE,0);approx(r.Vmeasured,0.003);
});
test('r0 路径：V0=r0·I，且随电流换向变号',()=>{
  const p=sideEffects({...base,B:0,r0:0.4});approx(p.V0,0.002);
  const n=sideEffects({...base,B:0,r0:0.4,polarity:{current:-1,field:1}});approx(n.V0,-0.002);
});
test('只有 VE（RH=0，系数路径）',()=>{
  const r=sideEffects({I:0.005,B:0.4,RH:0,d:5e-4,ettinghausenCoeff:0.005});
  assert.equal(r.VH,0);assert.equal(r.V0,0);approx(r.VE,1e-5);approx(r.Vmeasured,1e-5);
});
test('温度梯度路径 VE = S·∇T·L_E',()=>{
  const r=sideEffects({...base,temperatureGradient:100,seebeckCoeff:2e-4,ettinghausenLength:0.004});
  assert.equal(r.VH,0.04);approx(r.VE,8e-5);
});
test('磁场换向：VH、VE 变号，V0 不变',()=>{
  const p={...base,V0:0.002,ettinghausenCoeff:0.005};
  const a=sideEffects(p),b=sideEffects({...p,polarity:{current:1,field:-1}});
  approx(b.VH,-a.VH);approx(b.VE,-a.VE);approx(b.V0,a.V0);
});
test('电流换向：VH、V0、VE 均变号',()=>{
  const p={...base,V0:0.002,ettinghausenCoeff:0.005};
  const a=sideEffects(p),b=sideEffects({...p,polarity:{current:-1,field:1}});
  approx(b.VH,-a.VH);approx(b.V0,-a.V0);approx(b.VE,-a.VE);
});
test('电流与磁场同时换向：VH、VE 不变，V0 变号',()=>{
  const p={...base,V0:0.002,ettinghausenCoeff:0.005};
  const a=sideEffects(p),b=sideEffects({...p,polarity:{current:-1,field:-1}});
  approx(b.VH,a.VH);approx(b.VE,a.VE);approx(b.V0,-a.V0);
});
test('四方向对称修正：消除 V0/零点，残留 VE，可分离理想 VH',()=>{
  const r=correctByReversal({...base,V0:0.002,ettinghausenCoeff:0.005,extraVoltage:0.001});
  approx(r.correctedHall,0.04001);approx(r.residual.value,1e-5);approx(r.idealVH,0.04);
});
test('修正结果不受 V0 与零点大小影响',()=>{
  const a=correctByReversal({...base,ettinghausenCoeff:0.005}).correctedHall;
  const b=correctByReversal({...base,ettinghausenCoeff:0.005,V0:0.05,extraVoltage:0.03}).correctedHall;
  approx(a,b);
});
test('实测四值模式：corrected 正确，VE 与理想 VH 未知',()=>{
  const r=correctByReversal({readings:{V1:0.04301,V2:-0.03701,V3:0.03901,V4:-0.04101}});
  approx(r.correctedHall,0.04001);
  assert.equal(r.idealVH,null);assert.equal(r.residual.value,null);assert.equal(r.residual.separated,false);
});
test('极端输入溢出抛“计算范围”错误',()=>{
  assert.throws(()=>sideEffects({I:1e300,B:1e300,RH:1,d:1e-300}),/计算范围/);
});
test('非法输入：缺失/空/非数字',()=>{
  assert.throws(()=>sideEffects(),/电流 I/);
  assert.throws(()=>sideEffects({B:1,RH:1,d:1}),/电流 I/);
  assert.throws(()=>sideEffects({...base,I:null}),/电流 I/);
  assert.throws(()=>sideEffects({...base,I:'abc'}),/电流 I/);
  assert.throws(()=>sideEffects({...base,I:NaN}),/电流 I/);
});
test('负幅值与厚度非法抛错',()=>{
  assert.throws(()=>sideEffects({...base,I:-0.001}),/幅值/);
  assert.throws(()=>sideEffects({...base,B:-0.1}),/幅值/);
  assert.throws(()=>sideEffects({...base,d:0}),/厚度 d/);
  assert.throws(()=>sideEffects({...base,d:-1}),/厚度 d/);
});
test('极性非法与配置冲突抛错',()=>{
  assert.throws(()=>sideEffects({...base,polarity:{current:0,field:1}}),/polarity.current/);
  assert.throws(()=>sideEffects({...base,polarity:{current:1,field:2}}),/polarity.field/);
  assert.throws(()=>sideEffects({...base,V0:0.001,r0:0.2}),/只能提供一种/);
  assert.throws(()=>sideEffects({...base,ettinghausenCoeff:0.005,temperatureGradient:100,seebeckCoeff:2e-4,ettinghausenLength:0.004}),/只能提供一种/);
  assert.throws(()=>sideEffects({...base,temperatureGradient:100,seebeckCoeff:2e-4}),/temperatureGradient\(K\/m\)、seebeckCoeff\(V\/K\)、ettinghausenLength\(m\)/);
});
test('实测模式非法/缺失值抛错',()=>{
  assert.throws(()=>correctByReversal({readings:{V1:'x',V2:0,V3:0,V4:0}}),/V1/);
  assert.throws(()=>correctByReversal({readings:{V2:0,V3:0,V4:0}}),/V1/);
});
test('返回逐步解释文本',()=>{
  const r=sideEffects({...base,V0:0.002});
  assert.ok(Array.isArray(r.steps)&&r.steps.length>=6);
  assert.ok(r.steps.some(s=>s.includes('Vmeasured')));
});
