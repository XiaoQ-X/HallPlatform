<template>
  <div class="max-w-7xl space-y-5">
    <div class="grid lg:grid-cols-3 gap-5">
      <!-- 数据输入 -->
      <div class="card p-5 space-y-4">
        <h3 class="font-bold text-ink-900 flex items-center gap-2"><Icon name="layers" class="text-brand-600"/> 数据集</h3>
        <div>
          <label class="text-xs text-ink-400">从实验会话导入</label>
          <select class="input" @change="importSession" v-model="importId">
            <option value="">选择会话…</option>
            <option v-for="b in briefs" :key="b.id" :value="b.id">会话 #{{ b.id }}（{{ b.groups }}组）</option>
          </select>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <label class="text-xs text-ink-400">X 轴
            <select v-model="xfield" class="input mt-1"><option value="IS_mA">IS / mA</option><option value="IM_A">IM / A</option></select></label>
          <label class="text-xs text-ink-400">模型
            <select v-model="model" class="input mt-1"><option value="linear">线性</option><option value="quad">二次</option></select></label>
        </div>

        <div>
          <div class="flex items-center"><b class="text-sm text-ink-800">数据点（{{ pts.length }}）</b>
            <button class="ml-auto btn-soft text-xs" @click="pts.push({x:null,y:null})"><Icon name="plus":size="13"/> 添加</button></div>
          <div class="space-y-2 mt-2 max-h-56 overflow-y-auto">
            <div v-for="(p,i) in pts" :key="i" class="flex gap-2 items-center">
              <span class="text-xs text-ink-400 w-5">{{i+1}}</span>
              <input v-model.number="p.x" type="number" step="0.01" class="input !px-3"/>
              <input v-model.number="p.y" type="number" step="0.01" class="input !px-3"/>
              <button class="text-ink-400 hover:text-rose-500" @click="pts.splice(i,1)"><Icon name="x":size="15"/></button>
            </div>
          </div>
        </div>
        <button class="btn-primary w-full" @click="save">保存分析记录</button>
      </div>

      <!-- 图表 -->
      <div class="lg:col-span-2 space-y-5">
        <div class="card p-6">
          <div class="flex items-center mb-2">
            <h3 class="font-bold text-ink-900">拟合曲线</h3>
            <div class="ml-auto flex gap-2">
              <span class="chip">R² = {{ fmt(fit.r2) }}</span>
              <span class="chip-gray">{{ model==='linear'?'y='+fmt(fit.a)+'+'+fmt(fit.b)+'x':'二次拟合' }}</span>
            </div>
          </div>
          <Chart :option="fitOption" height="340px"/>
        </div>
        <div class="card p-6">
          <h3 class="font-bold text-ink-900 mb-2">残差分析</h3>
          <Chart :option="resOption" height="220px"/>
        </div>
      </div>
    </div>

    <!-- 参数与残差表 -->
    <div class="grid lg:grid-cols-2 gap-5">
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-3">拟合参数</h3>
        <div class="space-y-2 text-sm">
          <div v-if="model==='linear'" class="flex justify-between p-3 rounded-2xl bg-ink-50"><span class="text-ink-500">截距 a</span><b>{{ fmt(fit.a) }}</b></div>
          <div v-if="model==='linear'" class="flex justify-between p-3 rounded-2xl bg-brand-50/60"><span class="text-brand-700">斜率 b（灵敏度）</span><b>{{ fmt(fit.b) }}</b></div>
          <template v-else>
            <div v-for="(c,i) in fit.coefs||[]" :key="i" class="flex justify-between p-3 rounded-2xl bg-ink-50"><span class="text-ink-500">系数 c{{i}}</span><b>{{ fmt(c) }}</b></div>
          </template>
          <div class="flex justify-between p-3 rounded-2xl bg-amber-50"><span class="text-amber-700">决定系数 R²</span><b>{{ fmt(fit.r2) }}</b></div>
        </div>
      </div>
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-3">残差明细</h3>
        <div class="overflow-x-auto"><table class="w-full text-sm">
          <thead><tr class="text-xs text-ink-400"><th class="py-2 text-left">点</th><th>x</th><th>y</th><th>ŷ</th><th>残差</th></tr></thead>
          <tbody>
            <tr v-for="(p,i) in fit.rows||[]" :key="i" class="border-t border-ink-50 text-center">
              <td class="text-left py-2">{{i+1}}</td><td>{{ fmt(p.x) }}</td><td>{{ fmt(p.y) }}</td>
              <td>{{ fmt(p.yhat) }}</td><td :class="Math.abs(p.res)>.05?'text-rose-500':'text-ink-600'">{{ fmt(p.res) }}</td>
            </tr>
          </tbody></table></div>
      </div>
    </div>

    <!-- 历史 -->
    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-3">历史分析记录</h3>
      <div class="flex flex-wrap gap-3">
        <div v-for="h in history" :key="h.id" class="chip-gray !text-sm px-4 py-2">{{ h.title }} · R²={{fmt(h.metrics?.r2)}}
          <button class="text-rose-400 ml-1" @click="remove(h)"><Icon name="trash":size="13"/></button></div>
        <div v-if="!history.length" class="text-sm text-ink-400">暂无记录</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import Chart from '../../components/Chart.vue';
import { api } from '../../api';
const briefs = ref([]), pts = ref([]), model = ref('linear'), xfield = ref('IS_mA');
const importId = ref(null); const history = ref([]);

async function importSession(e){
  if(!importId.value) return;
  const d = await api('/workshop/session-points/'+importId.value);
  pts.value = d.groups.map(g=>({ x: g[xfield.value], y: g.VH_mV }));
}
function gauss(A,b){
  const n=b.length;
  for(let i=0;i<n;i++){ let p=i;
    for(let k=i+1;k<n;k++) if(Math.abs(A[k][i])>Math.abs(A[p][i]))p=k;
    [A[i],A[p]]=[A[p],A[i]];[b[i],b[p]]=[b[p],b[i]];
    for(let k=i+1;k<n;k++){ const f=A[k][i]/A[i][i];
      for(let j=i;j<n;j++)A[k][j]-=f*A[i][j]; b[k]-=f*b[i]; }
  }
  const x=Array(n).fill(0);
  for(let i=n-1;i>=0;i--){ let s=b[i];
    for(let j=i+1;j<n;j++)s-=A[i][j]*x[j]; x[i]=s/A[i][i]; }
  return x;
}
const fit = computed(()=>{
  const data = pts.value.filter(p=>p.x!=null&&p.y!=null&&!isNaN(p.x)&&!isNaN(p.y));
  if(data.length<2) return {};
  let coefs;
  if(model.value==='linear'){
    const n=data.length; let sx=0,sy=0,sxx=0,sxy=0;
    data.forEach(p=>{sx+=p.x;sy+=p.y;sxx+=p.x*p.x;sxy+=p.x*p.y;});
    const b=(n*sxy-sx*sy)/(n*sxx-sx*sx), a=(sy-b*sx)/n;
    coefs=[a,b];
  } else {
    if(data.length<3) return {};
    const A=[[0,0,0],[0,0,0],[0,0,0]], B=[0,0,0];
    data.forEach(p=>{ const z=[1,p.x,p.x*p.x];
      for(let i=0;i<3;i++){B[i]+=z[i]*p.y;for(let j=0;j<3;j++)A[i][j]+=z[i]*z[j];}});
    coefs=gauss(A,B);
  }
  const pred=x=>{ let s=0; coefs.forEach((c,i)=>s+=c*x**i); return s; };
  const mean=data.reduce((a,p)=>a+p.y,0)/data.length;
  let ssres=0,sstot=0; const rows=[];
  data.forEach(p=>{ const yhat=pred(p.x),res=p.y-yhat; ssres+=res*res;
    sstot+=(p.y-mean)**2; rows.push({x:p.x,y:p.y,yhat,res}); });
  const r2=1-ssres/sstot;
  return model.value==='linear'?{a:coefs[0],b:coefs[1],r2,rows}:{coefs,r2,rows};
});
const fmt=v=>v==null||isNaN(v)?'—':Math.round(v*10000)/10000;

const fitOption=computed(()=>{
  const rows=fit.value.rows||[]; if(!rows.length)return{};
  const xs=rows.map(r=>r.x); const lo=Math.min(...xs),hi=Math.max(...xs);
  const line=[]; for(let i=0;i<=40;i++){const x=lo+(hi-lo)*i/40;
    let y=0; const c=(fit.value.coefs||[fit.value.a,fit.value.b]);
    c.forEach((z,k)=>y+=z*x**k); line.push([x,y]);}
  return {
    grid:{left:54,right:24,top:24,bottom:40},
    tooltip:{trigger:'axis'},
    xAxis:{type:'value',scale:true,name:xfield.value,axisLine:{lineStyle:{color:'#aebccd'}},splitLine:{lineStyle:{color:'#eef2f6'}}},
    yAxis:{type:'value',scale:true,name:'VH/mV',axisLine:{lineStyle:{color:'#aebccd'}},splitLine:{lineStyle:{color:'#eef2f6'}}},
    series:[
      {type:'scatter',data:rows.map(r=>[r.x,r.y]),symbolSize:11,
        itemStyle:{color:'#0d9488',borderColor:'#fff',borderWidth:2}},
      {type:'line',data:line,showSymbol:false,smooth:true,
        lineStyle:{width:3,color:'#0284c7'},
        areaStyle:{color:{type:'linear',x:0,y:0,x2:0,y2:1,colorStops:[{offset:0,color:'rgba(2,132,199,.18)'},{offset:1,color:'rgba(2,132,199,0)'}]}}}
    ]
  };
});
const resOption=computed(()=>{
  const rows=fit.value.rows||[];
  return { grid:{left:54,right:24,top:20,bottom:36}, tooltip:{},
    xAxis:{type:'category',data:rows.map((r,i)=>i+1)},
    yAxis:{type:'value',name:'残差',splitLine:{lineStyle:{color:'#eef2f6'}}},
    series:[{type:'bar',data:rows.map(r=>Math.round(r.res*1000)/1000),
      itemStyle:{color:p=>p.value>=0?'#14b8a6':'#fb7185',borderRadius:[6,6,6,6]}}]};
});
async function save(){
  const title=prompt('分析记录标题'); if(!title)return;
  await api('/workshop/analysis',{method:'POST',body:{title,model:model.value,
    dataset:pts.value,params:model.value==='linear'?{a:fit.value.a,b:fit.value.b}:{coefs:fit.value.coefs},
    metrics:{r2:fit.value.r2}}});
  load();
}
async function remove(h){ await api('/workshop/analysis/'+h.id,{method:'DELETE'}); load(); }
async function load(){ briefs.value=await api('/workshop/sessions-brief');
  history.value=await api('/workshop/analysis'); }
onMounted(load);
</script>