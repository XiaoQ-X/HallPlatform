const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
test('identity changes reject stale requests without clearing new credentials',async()=>{
 const keys=['window','localStorage','sessionStorage','location','fetch'],saved=Object.fromEntries(keys.map(k=>[k,global[k]]));
 let token='old',cleared=false;const navigation=[],listeners={};
 global.localStorage={getItem:()=>token,setItem:(k,v)=>token=v,removeItem:()=>token=null};
 global.sessionStorage={clear:()=>cleared=true};global.location={replace:p=>navigation.push(p)};
 global.window={addEventListener:(k,v)=>listeners[k]=v,dispatchEvent:()=>{}};
 try{
  const source=fs.readFileSync(require('path').join(__dirname,'../src/api.js'),'utf8');
  const api=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
  let release;global.fetch=()=>new Promise(r=>release=r);
  const pending=api.api('/me');api.setToken('new');
  release({status:401,ok:false,json:async()=>({error:'expired old token'})});
  await assert.rejects(pending,/登录账号已变更/);assert.equal(token,'new');assert.deepEqual(navigation,[]);
  let responseBody;global.fetch=async()=>({status:200,ok:true,json:()=>new Promise(r=>responseBody=r)});
  const oldBody=api.api('/me');await new Promise(r=>setImmediate(r));api.setToken('newer');responseBody({name:'old user'});
  await assert.rejects(oldBody,/登录账号已变更/);assert.equal(token,'newer');
  token='other-tab';let requests=0;global.fetch=()=>{requests++;};
  assert.throws(()=>api.api('/teacher/grades',{method:'POST',body:{}}),/登录账号已变更/);
  assert.equal(requests,0);assert(cleared);assert.deepEqual(navigation,['/']);
  assert.equal(api.clearToken('newer'),false);assert.equal(token,'other-tab');
 }finally{for(const k of keys){if(saved[k]===undefined)delete global[k];else global[k]=saved[k];}}
});
