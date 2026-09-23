<template><div class="grid lg:grid-cols-2 gap-6 max-w-6xl">
<section class="space-y-4"><h2 class="font-bold">不确定度评定</h2><label class="block">标题<input v-model="title" class="input"/></label>
<div class="grid grid-cols-2 gap-3"><label>测量量<input v-model="quantity" class="input"/></label><label>单位<input v-model="unit" class="input"/></label></div>
<label class="block">仪器与误差限来源<input v-model="instrument" class="input"/></label>
<label class="block">导入实验会话<select v-model="sessionId" @change="importRepeats" class="input"><option :value="null">手工数据</option><option v-for="s in sessions" :value="s.id">#{{s.id}} · {{s.groups}}组</option></select></label>
<label v-if="repeatSets.length" class="block">同条件重复组<select v-model="repeatKey" @change="selectRepeats" class="input"><option v-for="g in repeatSets" :value="g.key">{{g.key}} · {{g.rows.length}}次</option></select></label>
<div class="flex justify-between"><b>重复测量</b><button class="btn-soft" @click="vals.push(null)" title="添加测量"><Icon name="plus" :size="16"/></button></div>
<div v-for="(v,i) in vals" :key="i" class="flex gap-3"><input type="number" v-model.number="vals[i]" class="input" :aria-label="'测量'+(i+1)"/><button @click="vals.splice(i,1)" title="删除测量"><Icon name="x" :size="16"/></button></div>
<div class="grid grid-cols-2 gap-3"><label>误差限 Δ<input v-model.number="delta" type="number" min="0" class="input"/></label><label>分布<select v-model="dist" class="input"><option value="uniform">均匀</option><option value="normal">正态（Δ=3σ）</option><option value="tri">三角</option></select></label></div>
<label class="block">包含因子 k<input v-model.number="kf" type="number" min="0.1" step="0.1" class="input"/></label>
<p v-if="r.error" role="alert" class="text-rose-600">{{r.error}}</p><button class="btn-primary" :disabled="!!r.error||saving" @click="save">保存评定</button>
</section>
<section class="space-y-5"><h2 class="font-bold">评定结果</h2><dl v-if="!r.error" class="grid grid-cols-2 gap-3"><template v-for="(label,key) in labels"><dt>{{label}}</dt><dd>{{fmt(r[key])}} {{key==='n'?'':unit}}</dd></template></dl>
<h2 class="font-bold">历史记录</h2><div v-for="h in history" :key="h.id" class="flex flex-wrap gap-3 py-3 border-b"><span>{{h.title}}</span><span>{{h.inputs.mode==='hall'?'U(R_H)':'U'}}={{fmt(h.inputs.mode==='hall'?h.result?.UR:h.result?.U)}}</span><button class="text-brand-700" @click="restore(h)">载入</button><button @click="remove(h)" title="删除"><Icon name="trash" :size="16"/></button></div>
</section><div class="lg:col-span-2"><HallUncertainty ref="hallPanel" @saved="load"/></div></div></template>
<script setup>
import {ref,computed,onMounted} from 'vue';import Icon from '../../components/Icon.vue';import {api} from '../../api';import calculations from '../../../shared/calculations.cjs';
import HallUncertainty from '../../components/HallUncertainty.vue';
const title=ref(''),quantity=ref('霍尔电压'),unit=ref('mV'),instrument=ref(''),vals=ref([null,null]),delta=ref(0),dist=ref('uniform'),kf=ref(2),history=ref([]),saving=ref(false);
const sessions=ref([]),sessionId=ref(null),repeatSets=ref([]),repeatKey=ref(''),source=ref(null),hallPanel=ref(null);
let importSeq=0;
async function importRepeats(){
  const seq=++importSeq, selected=sessionId.value;
  if(!selected){repeatSets.value=[];repeatKey.value='';source.value=null;vals.value=[];return;}
  const d=await api('/workshop/session-points/'+selected);
  if(seq!==importSeq||sessionId.value!==selected)return;
  const groups=new Map();for(const g of d.groups.filter(g=>g.decision!=='exclude')){const key=`${g.series_id} / IS=${Math.abs(g.IS_mA).toFixed(3)} mA / IM=${Math.abs(g.IM_A).toFixed(4)} A`;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(g);}repeatSets.value=[...groups].map(([key,rows])=>({key,rows}));repeatKey.value=repeatSets.value[0]?.key||'';source.value={session_id:selected,cleaningVersion:d.version};selectRepeats();}
function selectRepeats(){const group=repeatSets.value.find(g=>g.key===repeatKey.value);vals.value=group?group.rows.map(g=>g.normVH_mV??g.VH_mV):[];source.value={...source.value,measurement_ids:group?.rows.map(g=>g.id)||[],originalValues:[...vals.value]};quantity.value='霍尔电压';unit.value='mV';}
const labels={n:'测量次数',mean:'平均值',s:'实验标准偏差',uA:'A类标准不确定度',uB:'B类标准不确定度',uC:'合成标准不确定度',U:'扩展不确定度'};
const inputs=computed(()=>({vals:vals.value,delta:delta.value,dist:dist.value,k:kf.value,quantity:quantity.value,unit:unit.value,instrument:instrument.value,source:source.value}));
const r=computed(()=>{try{return calculations.uncertainty(inputs.value)}catch(e){return{error:e.message}}});const fmt=v=>Number.isFinite(v)?Number(v.toPrecision(6)):'—';
async function save(){if(r.value.error||saving.value)return;if(!title.value||!unit.value||!instrument.value)return alert('请填写标题、单位和仪器依据');saving.value=true;try{await api('/workshop/uncertainty',{method:'POST',body:{title:title.value,inputs:inputs.value}});await load();}finally{saving.value=false;}}
function restore(h){if(h.inputs.mode==='hall'){hallPanel.value.restore(h);return;}title.value=h.title;const p=h.inputs;vals.value=[...p.vals];delta.value=p.delta;dist.value=p.dist;kf.value=p.k;quantity.value=p.quantity||'霍尔电压';unit.value=p.unit||'mV';instrument.value=p.instrument||'';source.value=p.source||null;}
async function remove(h){if(confirm('删除此记录？')){await api('/workshop/uncertainty/'+h.id,{method:'DELETE'});await load();}}async function load(){history.value=await api('/workshop/uncertainty');sessions.value=await api('/workshop/sessions-brief');}onMounted(load);
</script>
