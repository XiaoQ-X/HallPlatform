const {Matrix,QrDecomposition,SingularValueDecomposition,inverse}=require('ml-matrix');
const {jStat}=require('jstat');
function finite(value){return typeof value==='number'&&Number.isFinite(value);}
function checked(value){if(typeof value==='number'&&!Number.isFinite(value))throw Error('数值超出计算范围，请检查单位与数量级');if(value&&typeof value==='object')Object.values(value).forEach(checked);return value;}
function uncertainty(inputs){
  if(inputs.mode==='hall')return hallUncertainty(inputs);
  const {vals,delta,dist,k}=inputs;if(!Array.isArray(vals)||vals.length<2||vals.some(v=>!finite(v)))throw Error('A类评定需要至少两次有效重复测量，不能有空白');
  if(!finite(delta)||delta<0||!finite(k)||k<=0)throw Error('误差限须非负，包含因子须大于0');
  const divisor={uniform:Math.sqrt(3),normal:3,tri:Math.sqrt(6)}[dist];if(!divisor)throw Error('分布无效');
  const n=vals.length,mean=vals.reduce((a,b)=>a+b,0)/n,s=Math.sqrt(vals.reduce((a,b)=>a+(b-mean)**2,0)/(n-1)),uA=s/Math.sqrt(n),uB=delta/divisor,uC=Math.hypot(uA,uB);
  return checked({n,mean,s,uA,uB,uC,U:k*uC});
}
function hallUncertainty(inputs){
  if(inputs.independent!==true)throw Error('相关量需提供协方差模型；此模式仅适用于独立输入量');
  const keys=['V','d','I','B'];for(const key of keys){if(!finite(inputs[key])||!finite(inputs['u'+key])||inputs['u'+key]<0)throw Error('请填写有限测量值与非负标准不确定度');}
  if(inputs.d<=0||inputs.I===0||inputs.B===0||inputs.V===0||!finite(inputs.k)||inputs.k<=0)throw Error('厚度、包含因子须为正，电压、电流、磁场不能为零');
  const {V,d,I,B,k}=inputs,R=V*d/(I*B);
  const derivatives=[d/(I*B),V/(I*B),-V*d/(I*I*B),-V*d/(I*B*B)];
  const uR=Math.hypot(...keys.map((key,i)=>derivatives[i]*inputs['u'+key]));const e=1.602176634e-19,n=1/(e*Math.abs(R)),un=n*uR/Math.abs(R);
  return checked({R,uR,UR:k*uR,n,un,Un:k*un,assumption:'输入量独立，SI单位，一阶传播'});
}
function fit(data,model='linear'){
  const p=model==='quad'?3:model==='multi'?3:2;if(!['linear','quad','multi'].includes(model))throw Error('拟合模型无效');
  if(!Array.isArray(data)||data.length<p||data.some(d=>!finite(d.x)||!finite(d.y)||(model==='multi'&&!finite(d.z))))throw Error('数据点不足或存在空白、无效数值');
  const centers=[data.reduce((s,d)=>s+d.x,0)/data.length,data.reduce((s,d)=>s+(d.z||0),0)/data.length];
  const scales=[Math.max(...data.map(d=>Math.abs(d.x-centers[0]))),Math.max(...data.map(d=>Math.abs((d.z||0)-centers[1])))];if(!scales[0]||(model==='multi'&&!scales[1]))throw Error('自变量无变化，无法拟合');
  const design=data.map(d=>{const x=(d.x-centers[0])/scales[0];return model==='quad'?[1,x,x*x]:model==='multi'?[1,x,(d.z-centers[1])/scales[1]]:[1,x];});
  checked(design);
  const matrix=new Matrix(design),svd=new SingularValueDecomposition(matrix,{autoTranspose:true});if(svd.rank<p||svd.condition>1e10)throw Error('自变量重复或共线，模型不可辨识');
  const normalized=new QrDecomposition(matrix).solve(Matrix.columnVector(data.map(d=>d.y))).to1DArray();
  let coefs;
  if(model==='quad'){const c=normalized[2]/scales[0]**2,b=normalized[1]/scales[0]-2*c*centers[0];coefs=[normalized[0]-normalized[1]*centers[0]/scales[0]+c*centers[0]**2,b,c];}
  else if(model==='multi')coefs=[normalized[0]-normalized[1]*centers[0]/scales[0]-normalized[2]*centers[1]/scales[1],normalized[1]/scales[0],normalized[2]/scales[1]];
  else coefs=[normalized[0]-normalized[1]*centers[0]/scales[0],normalized[1]/scales[0]];
  const mean=data.reduce((s,d)=>s+d.y,0)/data.length;let sse=0,sst=0;
  const rows=data.map((d,i)=>{const yhat=design[i].reduce((s,x,j)=>s+x*normalized[j],0),res=d.y-yhat;sse+=res*res;sst+=(d.y-mean)**2;return {...d,yhat,res};});
  const dof=data.length-p,rmse=dof>0?Math.sqrt(sse/dof):null;
  let standardErrors=null,ci95=null;
  if(dof>0){
    const [cx,cz]=centers,[sx,sz]=scales;
    const transform=new Matrix(model==='quad'?[[1,-cx/sx,cx*cx/(sx*sx)],[0,1/sx,-2*cx/(sx*sx)],[0,0,1/(sx*sx)]]:model==='multi'?[[1,-cx/sx,-cz/sz],[0,1/sx,0],[0,0,1/sz]]:[[1,-cx/sx],[0,1/sx]]);
    const covariance=transform.mmul(inverse(matrix.transpose().mmul(matrix))).mmul(transform.transpose()).mul(sse/dof);
    standardErrors=coefs.map((_,i)=>Math.sqrt(Math.max(0,covariance.get(i,i))));
    const critical=jStat.studentt.inv(.975,dof);ci95=coefs.map((c,i)=>[c-critical*standardErrors[i],c+critical*standardErrors[i]]);
  }
  checked({sse,sst});
  return checked({a:coefs[0],b:coefs[1],coefs,r2:sst>0?1-sse/sst:null,rows,rmse,dof,standardErrors,ci95,algorithm:'QR-centered-v2'});
}
module.exports={uncertainty,fit};
