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
// ===== 副效应与误差修正（教学模型）=====
// 约定：I、B 为非负幅值，方向由 polarity.current / polarity.field(±1) 表达。
// 所有副效应系数必须由调用方按实际样品/测量提供；本模块不内置任何样品常数，缺省按 0 处理。
// The four transverse magnetic effects are Hall, Ettinghausen, Nernst and
// Righi–Leduc.  V0 is kept as a separate contact-misalignment/instrument
// term, so the teaching model does not accidentally call it a magnetic effect.
const SIDE_EFFECT_MODEL='side-effects-v2：V⊥=VH+VE+VN+VRL；Vmeasured=V⊥+V0+Voffset；系数需来自实际样品/测量';
function fmt(x){if(!finite(x))return String(x);if(x===0)return '0';const a=Math.abs(x);if(a<1e-4||a>=1e6)return x.toExponential(4);return String(Math.round(x*1e10)/1e10);}
function seNum(v,label,unit){if(!finite(v))throw Error(label+'无效：需为有限数值（单位：'+unit+'）');return v;}
function seAmp(v,label,unit){seNum(v,label,unit);if(v<0)throw Error(label+'：请填非负幅值；方向通过 polarity.current / polarity.field 设置（±1）');return v;}
function seSign(v,label){if(v!==1&&v!==-1)throw Error(label+'须为 1 或 -1（换向状态）');return v;}
function unitNotes(p){
  const n=[];
  if(p.d>0.01)n.push('厚度 d 量级偏大：若填的是毫米请换算为米（0.5 mm = 5e-4 m）');
  if(p.d>0&&p.d<1e-9)n.push('厚度 d 量级偏小，请确认单位为米');
  if(p.I>0.5)n.push('电流 I 偏大：请确认单位为安（毫安需乘 1e-3）');
  if(p.B>10)n.push('磁感应强度 B 偏大：毫特斯拉乘 1e-3、高斯乘 1e-4');
  return n;
}
function sideEffects(input={}){
  if(typeof input!=='object'||input===null)throw Error('输入须为配置对象');
  const I=seAmp(input.I,'电流 I','A');
  const B=seAmp(input.B,'磁感应强度 B','T');
  const RH=seNum(input.RH,'霍尔系数 RH','m³/C');
  const d=seNum(input.d,'样品厚度 d','m');
  if(d<=0)throw Error('样品厚度 d 须为正（单位 m）；毫米请换算，如 0.5 mm = 5e-4 m');
  const pol=input.polarity||{};
  const sI=pol.current===undefined?1:seSign(pol.current,'polarity.current');
  const sB=pol.field===undefined?1:seSign(pol.field,'polarity.field');

  // 不等位 V0：直接电势 V0(V) 或 不等位电阻 r0(Ω)，二选一
  if(input.V0!==undefined&&input.r0!==undefined)throw Error('不等位电势 V0 与不等位电阻 r0 只能提供一种');
  let V0amp=0,v0Source='未配置，按 0 处理';
  if(input.r0!==undefined){V0amp=seNum(input.r0,'不等位电阻 r0','Ω')*I;v0Source='r0·I';}
  else if(input.V0!==undefined){V0amp=seNum(input.V0,'不等位电势 V0','V');v0Source='直接给定（参考 +I）';}

  // 厄廷豪森 VE：有效电压系数 kE(V/(A·T)) 或 温度梯度路径（∇T、S、L_E 三件套），二选一
  // kE 包含样品几何与热电转换，不是以温度梯度定义的材料厄廷豪森系数。
  const tKeys=['temperatureGradient','seebeckCoeff','ettinghausenLength'];
  const tGiven=tKeys.filter(k=>input[k]!==undefined);
  if(input.ettinghausenCoeff!==undefined&&tGiven.length)throw Error('厄廷豪森有效电压系数与温度梯度参数只能提供一种');
  if(tGiven.length&&tGiven.length<3)throw Error('温度梯度路径需同时提供 temperatureGradient(K/m)、seebeckCoeff(V/K)、ettinghausenLength(m)');
  let VEamp=0,veSource='未配置，按 0 处理';
  if(input.ettinghausenCoeff!==undefined){VEamp=seNum(input.ettinghausenCoeff,'厄廷豪森有效电压系数 ettinghausenCoeff','V/(A·T)')*I*B;veSource='kE·I·B';}
  else if(tGiven.length===3){
    const g=seNum(input.temperatureGradient,'温度梯度 temperatureGradient','K/m');
    const S=seNum(input.seebeckCoeff,'温差电动势率 seebeckCoeff','V/K');
    const L=seNum(input.ettinghausenLength,'厄廷豪森特征长度 ettinghausenLength','m');
    if(L<=0)throw Error('ettinghausenLength 须为正（m）');
    // Keep the signed Seebeck/temperature-gradient product.  The sign fixes
    // the measured voltage polarity; taking an absolute value would erase a
    // physically meaningful reversal when either the gradient or S changes.
    VEamp=S*g*L;veSource='S·∇T·L_E（保留符号）';
  }
  // Effective teaching coefficients for the other two transverse magnetic
  // effects.  They are supplied in V/T and are odd under B reversal.  This
  // compact model intentionally leaves the thermal-gradient geometry inside
  // the effective coefficients; a calibrated sample can provide its own.
  const VNamp=input.nernstCoeff===undefined?0:seNum(input.nernstCoeff,'能斯特有效电压系数 nernstCoeff','V/T')*B;
  const VRLamp=input.righiLeducCoeff===undefined?0:seNum(input.righiLeducCoeff,'里纪-勒杜克有效电压系数 righiLeducCoeff','V/T')*B;
  const extra=input.extraVoltage===undefined?0:seNum(input.extraVoltage,'固定零点偏置 extraVoltage','V');

  // 物理归零：无电流则无 V0；无电流或磁场则无 I·B 类效应（含温差）
  if(I===0){V0amp=0;VEamp=0;}
  if(B===0)VEamp=0;

  const product=sI*sB;
  const VH=RH*I*B*product/d;
  const V0=V0amp*sI;
  const VE=VEamp*product;
  const VN=VNamp*sB;
  const VRL=VRLamp*sB;
  const Vmeasured=VH+V0+VE+VN+VRL+extra;
  const notes=unitNotes({I,B,d});
  const steps=[
    '模型：'+SIDE_EFFECT_MODEL+'（教学演示，非样品标定值）',
    '极性：电流 '+(sI>0?'+':'-')+'，磁场 '+(sB>0?'+':'-')+'（I·B 组合符号 '+(product>0?'+':'-')+'）',
    '理想霍尔电压 VH = RH·I·B/d = '+RH+'×'+I+'×'+B+'/'+d+'，乘 I·B 符号 '+product+'，得 '+fmt(VH)+' V',
    '不等位电势 V0（'+v0Source+'）随电流换向：'+fmt(V0)+' V',
    '厄廷豪森附加电压 VE（'+veSource+'）随 I·B 换向：'+fmt(VE)+' V',
    '能斯特附加电压 VN（有效系数·B）随 B 换向：'+fmt(VN)+' V',
    '里纪-勒杜克附加电压 VRL（有效系数·B）随 B 换向：'+fmt(VRL)+' V',
    '固定零点偏置：'+fmt(extra)+' V（不随换向）',
    '横向物理信号 V⊥ = VH + VE + VN + VRL = '+fmt(VH+VE+VN+VRL)+' V',
    '实际测量电压 Vmeasured = V⊥ + V0 + 零点 = '+fmt(Vmeasured)+' V'
  ];
  if(notes.length)steps.push('单位提示：'+notes.join('；'));
  return checked({model:SIDE_EFFECT_MODEL,units:{I:'A',B:'T',RH:'m³/C',d:'m'},polarity:{current:sI,field:sB},
    VH,V0,VE,VN,VRL,extraVoltage:extra,Vmeasured,
    breakdown:[
      {symbol:'VH',value:VH,dependsOn:'I·B',signRule:'随 I 或 B 换向变号'},
      {symbol:'V0',value:V0,dependsOn:'I',signRule:'随 I 换向变号，与 B 无关'},
      {symbol:'VE',value:VE,dependsOn:'I·B',signRule:'随 I·B 换向；I、B 同时换向时不变'},
      {symbol:'VN',value:VN,dependsOn:'B',signRule:'随 B 换向，与 I 无关'},
      {symbol:'VRL',value:VRL,dependsOn:'B',signRule:'随 B 换向，与 I 无关'},
      {symbol:'零点',value:extra,dependsOn:'—',signRule:'固定，不随换向'}
    ],notes,steps});
}
function correctByReversal(input={}){
  if(typeof input!=='object'||input===null)throw Error('输入须为配置对象');
  const quadrants=[
    {name:'V1',current:1,field:1},{name:'V2',current:1,field:-1},
    {name:'V3',current:-1,field:-1},{name:'V4',current:-1,field:1}
  ];
  let readings,VEref=0,canSeparate=false,notes=[];
  if(input.readings){
    readings=quadrants.map(q=>{const v=input.readings[q.name];if(!finite(v))throw Error('实测 '+q.name+' 无效：需为有限数值（V）');
      return {name:q.name,current:q.current,field:q.field,Vmeasured:v};});
    notes.push('实测模式：仅给四方向电压，无法获知 VE，故只能得到 VH+VE 合成值，理想 VH 与 VE 残差未知');
  }else{
    readings=quadrants.map(q=>{const se=sideEffects({...input,polarity:{current:q.current,field:q.field}});
      return {name:q.name,current:q.current,field:q.field,VH:se.VH,V0:se.V0,VE:se.VE,VN:se.VN,VRL:se.VRL,extraVoltage:se.extraVoltage,Vmeasured:se.Vmeasured};});
    canSeparate=input.ettinghausenCoeff!==undefined||input.temperatureGradient!==undefined;
    VEref=readings[0].VE; // (+I,+B) 方向的 VE
    notes=unitNotes({I:input.I,B:input.B,d:input.d});
  }
  const [V1,V2,V3,V4]=readings.map(r=>r.Vmeasured);
  const correctedHall=(V1-V2+V3-V4)/4;
  const idealVH=canSeparate?correctedHall-VEref:null;
  const steps=[
    '四方向对称测量顺序：V1(+I,+B)、V2(+I,-B)、V3(-I,-B)、V4(-I,+B)',
    '修正公式：Vcorr = (V1 − V2 + V3 − V4)/4 = VH + VE',
    '代入：('+fmt(V1)+' − ('+fmt(V2)+') + '+fmt(V3)+' − ('+fmt(V4)+'))/4 = '+fmt(correctedHall)+' V',
    '该组合使 V0（∝I）、VN（∝B）、VRL（∝B）与固定零点偏置正负相消，故被消除',
    'VE 与 VH 同为 I·B 换向规律，四方向对称法无法消除 VE，修正结果实为 VH + VE',
    canSeparate
      ? '已配置厄廷豪森参数，VE(+，+)='+fmt(VEref)+' V，可进一步分离：理想 VH = 修正值 − VE = '+fmt(idealVH)+' V'
      : '未提供有效电压系数/完整温度梯度参数，无法分离 VE；如需理想 VH，请补充 ettinghausenCoeff 或温度梯度参数（∇T、S、L_E）'
  ];
  if(notes.length)steps.push('单位提示：'+notes.join('；'));
  return checked({model:SIDE_EFFECT_MODEL,readings,formula:'Vcorr = (V1 - V2 + V3 - V4)/4 = VH + VE',
    correctedHall,eliminated:['V0（∝I，随电流换向抵消）','VN（∝B，随磁场换向抵消）','VRL（∝B，随磁场换向抵消）','固定零点偏置'],
    residual:{symbol:'VE',value:canSeparate?VEref:null,separated:canSeparate,
      note:'VE 与 VH 同为 I·B 换向规律，四方向对称法不能消除，需借助温度/厄廷豪森参数分离'},
    idealVH,notes,steps});
}
module.exports={uncertainty,fit,sideEffects,correctByReversal};
