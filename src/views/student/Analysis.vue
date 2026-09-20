<template>
<div class="space-y-5 max-w-7xl">
  <div class="grid lg:grid-cols-3 gap-5">
    <section class="space-y-3">
      <h2 class="font-bold">数据与模型</h2>
      <label class="block">实验会话<select v-model="importId" class="input" @change="importSession"><option :value="null">手工数据</option><option v-for="s in briefs" :value="s.id">#{{s.id}} · {{s.groups}}组</option></select></label>
      <label class="block">X<select v-model="xfield" class="input"><option value="IS_mA">IS / mA</option><option value="IM_A">IM / A</option><option value="B_T">B / T</option></select></label>
      <label class="block">模型<select v-model="model" class="input"><option value="linear">y=a+bx</option><option value="quad">y=a+bx+cx²</option><option value="multi">y=a+bx+cz</option></select></label>
      <label v-if="model==='multi'" class="block">Z<select v-model="zfield" class="input"><option value="IS_mA">IS / mA</option><option value="IM_A">IM / A</option><option value="B_T">B / T</option></select></label>
      <label v-if="raw.length&&model!=='multi'" class="block">固定条件 / 系列<select v-model="series" class="input"><option v-for="s in seriesOptions" :value="s">{{s}}</option></select></label>
      <label class="flex gap-2"><input type="checkbox" v-model="cleaned"/>采用清洗后数据</label>
      <div class="flex justify-between"><b>{{pts.length}} 个点</b><button class="btn-soft" @click="add" title="添加数据点"><Icon name="plus" :size="16"/></button></div>
      <div class="max-h-72 overflow-auto space-y-2"><div v-for="(p,i) in pts" :key="i" class="flex gap-1">
        <input v-model.number="p.x" @input="manual=true" aria-label="X值" type="number" class="input min-w-0"/><input v-if="model==='multi'" v-model.number="p.z" @input="manual=true" aria-label="Z值" type="number" class="input min-w-0"/><input v-model.number="p.y" @input="manual=true" aria-label="霍尔电压mV" type="number" class="input min-w-0"/>
        <button @click="pts.splice(i,1);manual=true" title="删除数据点"><Icon name="x" :size="16"/></button>
      </div></div>
      <p v-if="fit.error" role="alert" class="text-rose-600">{{fit.error}}</p>
      <button class="btn-primary w-full" :disabled="!!fit.error||saving" @click="save">保存分析记录</button>
    </section>
    <section class="lg:col-span-2 space-y-4"><h2 class="font-bold">{{model==='multi'?'观测值与预测值':'拟合曲线'}}</h2><Chart :option="fitOption" height="330px"/>
      <div class="flex flex-wrap gap-4 text-sm"><span>R²：{{fmt(fit.r2)}}</span><span>残差标准差：{{fmt(fit.rmse)}} mV</span><span>自由度：{{fit.dof??'—'}}</span><span v-for="(c,i) in fit.coefs||[]">c{{i}}：{{fmt(c)}}</span></div>
      <div v-if="fit.ci95" class="text-sm space-y-1"><p v-for="(ci,i) in fit.ci95">c{{i}}：标准误 {{fmt(fit.standardErrors[i])}}；95% 区间 [{{fmt(ci[0])}}, {{fmt(ci[1])}}]</p><p>条件：线性模型适用，残差独立、同方差且近似正态。</p></div>
      <h2 class="font-bold">残差</h2><Chart :option="resOption" height="220px"/>
    </section>
  </div>
  <section><h2 class="font-bold mb-3">分析历史</h2><div v-for="h in history" :key="h.id" class="flex flex-wrap gap-3 border-b py-3"><span>{{h.title}}</span><span>R²={{fmt(h.metrics?.r2)}}</span><button class="text-brand-700" @click="restore(h)">载入</button><button @click="exportRecord(h)" title="下载分析JSON"><Icon name="download" :size="16"/></button><button @click="remove(h)" title="删除记录"><Icon name="trash" :size="16"/></button></div></section>
</div>
</template>
<script setup>
import {ref,computed,onMounted,watch} from 'vue';import Icon from '../../components/Icon.vue';import Chart from '../../components/Chart.vue';import {api} from '../../api';import calculations from '../../../shared/calculations.cjs';
const briefs=ref([]),pts=ref([]),raw=ref([]),history=ref([]),importId=ref(null),xfield=ref('IS_mA'),zfield=ref('B_T'),model=ref('linear'),cleaned=ref(true),series=ref(''),version=ref(''),manual=ref(false),saving=ref(false);
const seriesKey=g=>`${g.series_id||'legacy'} · ${xfield.value==='IS_mA'?'IM='+Number(g.IM_A).toFixed(3):'IS='+Number(g.IS_mA).toFixed(2)}`;
const eligible=computed(()=>raw.value.filter(g=>!cleaned.value||g.decision!=='exclude'));
const seriesOptions=computed(()=>[...new Set(eligible.value.map(seriesKey))]);
function mapPoints(){if(!raw.value.length){if(importId.value&&!manual.value)pts.value=[];return;}if(!seriesOptions.value.includes(series.value))series.value=seriesOptions.value[0]||'';const rows=eligible.value.filter(g=>model.value==='multi'||seriesKey(g)===series.value);pts.value=rows.map(g=>({x:g[xfield.value],z:g[zfield.value],y:g.normVH_mV??g.VH_mV,measurement_id:g.id}));manual.value=false;}
watch([xfield,zfield,model,cleaned,series],mapPoints);
async function importSession(){raw.value=[];pts.value=[];version.value='';manual.value=false;if(!importId.value){manual.value=true;return;}const selected=importId.value;const d=await api('/workshop/session-points/'+selected);if(importId.value!==selected)return;raw.value=d.groups;version.value=d.version;mapPoints();}
function add(){pts.value.push({x:null,y:null,z:null});manual.value=true;}
const fit=computed(()=>{try{return calculations.fit(pts.value,model.value)}catch(e){return{error:e.message}}});
const fmt=v=>Number.isFinite(v)?Number(v.toPrecision(6)):'未定义';
const fitOption=computed(()=>{const rows=fit.value.rows||[];return{tooltip:{trigger:'axis'},grid:{left:65,right:25,bottom:50},xAxis:{type:'value',name:model.value==='multi'?'观测VH/mV':xfield.value,scale:true},yAxis:{type:'value',name:model.value==='multi'?'预测VH/mV':'VH/mV',scale:true},series:[{type:'scatter',data:rows.map(p=>model.value==='multi'?[p.y,p.yhat]:[p.x,p.y])},...(model.value==='multi'?[]:[{type:'line',showSymbol:false,data:[...rows].sort((a,b)=>a.x-b.x).map(p=>[p.x,p.yhat])}])]};});
const resOption=computed(()=>({tooltip:{},xAxis:{type:'category',data:(fit.value.rows||[]).map((p,i)=>i+1)},yAxis:{type:'value',name:'残差/mV'},series:[{type:'bar',data:(fit.value.rows||[]).map(p=>p.res)}]}));
async function save(){if(fit.value.error||saving.value)return;const title=prompt('分析标题');if(!title)return;saving.value=true;try{await api('/workshop/analysis',{method:'POST',body:{title,model:model.value,dataset:pts.value,provenance:{session_id:importId.value,xfield:xfield.value,zfield:zfield.value,unit:'mV',series:series.value,cleaned:cleaned.value,version:version.value,manual:manual.value}}});await load();}finally{saving.value=false;}}
function restore(h){raw.value=[];const p=typeof h.provenance==='string'?JSON.parse(h.provenance):h.provenance||{};model.value=h.model;xfield.value=p.xfield||'IS_mA';zfield.value=p.zfield||'B_T';importId.value=p.session_id||null;version.value=p.version||'';series.value=p.series||'';cleaned.value=p.cleaned!==false;pts.value=JSON.parse(JSON.stringify(h.dataset));manual.value=true;}
function exportRecord(h){const u=URL.createObjectURL(new Blob([JSON.stringify(h,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=u;a.download='analysis-'+h.id+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(u),1000);}
async function remove(h){if(confirm('删除此记录？')){await api('/workshop/analysis/'+h.id,{method:'DELETE'});await load();}}
async function load(){briefs.value=await api('/workshop/sessions-brief');history.value=await api('/workshop/analysis');}onMounted(load);
</script>
