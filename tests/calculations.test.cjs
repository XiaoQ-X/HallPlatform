const test=require('node:test'),assert=require('node:assert/strict'),{fit,uncertainty}=require('../shared/calculations.cjs');
test('overflow cannot silently become null in stored calculations',()=>{
  assert.throws(()=>uncertainty({vals:[1e308,-1e308],delta:0,dist:'uniform',k:2}),/计算范围/);
  assert.throws(()=>fit([{x:1e308,y:1},{x:1.5e308,y:2},{x:1,y:3}]),/计算范围|不可辨识/);
  assert.throws(()=>uncertainty({mode:'hall',independent:true,V:1e308,d:1e308,I:1,B:1,uV:1,ud:1,uI:1,uB:1,k:2}),/计算范围/);
});
test('numeric and physical calculation edge cases',()=>{
  assert.throws(()=>uncertainty({vals:[1,'',2],delta:0,dist:'uniform',k:2}));
  assert.equal(uncertainty({vals:[1,2],delta:0,dist:'uniform',k:2}).mean,1.5);
  assert.throws(()=>uncertainty({vals:[1,2],delta:-1,dist:'uniform',k:2}));
  assert.throws(()=>fit([{x:1,y:1},{x:1,y:2}]));
  const r=fit([{x:1,y:3},{x:2,y:5},{x:3,y:7}]);assert(Math.abs(r.a-1)<1e-12);assert(Math.abs(r.b-2)<1e-12);
  assert.equal(fit([{x:1,y:2},{x:2,y:2}]).r2,null);
  const multi=fit([{x:1,z:0,y:3},{x:0,z:1,y:4},{x:2,z:1,y:8},{x:1,z:3,y:12}],'multi');assert(Math.abs(multi.coefs[0]-1)<1e-10);assert(Math.abs(multi.coefs[1]-2)<1e-10);assert(Math.abs(multi.coefs[2]-3)<1e-10);
});
test('confidence intervals and Hall propagation',()=>{
  const r=fit([{x:1,y:1},{x:2,y:2},{x:3,y:1.3},{x:4,y:3.75},{x:5,y:2.25}]);
  assert(r.ci95.every((bounds,i)=>bounds[0]<=r.coefs[i]&&bounds[1]>=r.coefs[i]));assert(r.standardErrors.every(x=>x>0));
  assert.equal(fit([{x:1,y:2},{x:2,y:3}]).ci95,null);
  const inputs={mode:'hall',independent:true,V:.001,d:.0005,I:.003,B:.2,uV:.00001,ud:.000005,uI:.00003,uB:.002,k:2};
  const h=uncertainty(inputs);assert(Math.abs(h.R-1/1200)<1e-15);assert(Math.abs(h.uR/Math.abs(h.R)-.02)<1e-12);assert(Math.abs(h.un/h.n-.02)<1e-12);
  assert.throws(()=>uncertainty({...inputs,independent:false}));
});
