<template>
  <div class="max-w-7xl space-y-5">
    <div class="grid lg:grid-cols-2 gap-5">
      <!-- 输入 -->
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-4 flex items-center gap-2"><Icon name="calculator" class="text-brand-600"/> 测量数据输入</h3>
        <label class="text-sm font-medium text-ink-700">计算标题</label>
        <input v-model="title" class="input mb-4" placeholder="如：霍尔电压VH不确定度评定"/>

        <div class="flex items-center mb-2">
          <b class="text-sm text-ink-800">A 类：重复测量值</b>
          <button class="ml-auto btn-soft text-xs" @click="vals.push(null)"><Icon name="plus":size="14"/> 添加</button>
        </div>
        <div class="space-y-2 max-h-52 overflow-y-auto pr-1">
          <div v-for="(v,i) in vals" :key="i" class="flex gap-2">
            <span class="w-8 text-xs text-ink-400 leading-10 text-right">x{{i+1}}</span>
            <input v-model.number="vals[i]" type="number" step="0.01" class="input"/>
            <button class="text-ink-400 hover:text-rose-500" @click="vals.splice(i,1)"><Icon name="x":size="17"/></button>
          </div>
        </div>

        <div class="divider-soft my-5"></div>
        <b class="text-sm text-ink-800 block mb-3">B 类：仪器误差</b>
        <div class="grid grid-cols-2 gap-3">
          <div><label class="text-xs text-ink-400">误差限 Δ</label>
            <input v-model.number="delta" type="number" step="0.01" class="input"/></div>
          <div><label class="text-xs text-ink-400">分布</label>
            <select v-model="dist" class="input"><option value="uniform">均匀分布（√3）</option>
              <option value="normal">正态分布（3）</option><option value="tri">三角分布（√6）</option></select></div>
        </div>
        <div class="mt-3"><label class="text-xs text-ink-400">包含因子 k（扩展不确定度）</label>
          <input v-model.number="kf" type="number" step="0.1" class="input"/></div>

        <div class="flex gap-3 mt-5">
          <button class="btn-ghost flex-1" @click="demo">填入示例</button>
          <button class="btn-primary flex-1" @click="save">保存评定记录</button>
        </div>
      </div>

      <!-- 结果 -->
      <div class="space-y-5">
        <div class="card p-6">
          <h3 class="font-bold text-ink-900 mb-4">评定结果</h3>
          <div class="grid grid-cols-2 gap-3">
            <div class="rounded-2xl bg-ink-50 p-4"><div class="text-xs text-ink-400">平均值 <Formula expr="\bar{x}"/></div><b class="text-xl text-ink-900">{{ fmt(r.mean) }}</b></div>
            <div class="rounded-2xl bg-ink-50 p-4"><div class="text-xs text-ink-400">测量次数 <Formula expr="n"/></div><b class="text-xl text-ink-900">{{ r.n }}</b></div>
            <div class="rounded-2xl bg-brand-50/70 p-4"><div class="text-xs text-brand-700">A类 <Formula expr="u_A=\dfrac{s}{\sqrt n}"/></div><b class="text-xl text-brand-800">{{ fmt(r.uA) }}</b></div>
            <div class="rounded-2xl bg-sky-50 p-4"><div class="text-xs text-sky-700">B类 <Formula expr="u_B=\dfrac{\Delta}{C}"/></div><b class="text-xl text-sky-800">{{ fmt(r.uB) }}</b></div>
            <div class="rounded-2xl bg-amber-50 p-4"><div class="text-xs text-amber-700">合成 <Formula expr="u_C"/></div><b class="text-xl text-amber-800">{{ fmt(r.uC) }}</b></div>
            <div class="rounded-2xl text-white p-4" style="background:linear-gradient(135deg,#0d9488,#0284c7)"><div class="text-white/70 text-xs">扩展 <Formula expr="U=k\,u_C"/></div><b class="text-xl">{{ fmt(r.U) }}</b></div>
          </div>
          <div class="mt-4 text-xs text-ink-400 leading-6" v-if="r.n>1">
            实验标准偏差 <Formula expr="s"/>；最终可报告为
            <Formula expr="x=(\bar{x}\pm U)\text{ 单位}"/>，包含因子 k={{ kf }}。
          </div>
        </div>

        <div class="card p-6">
          <h3 class="font-bold text-ink-900 mb-3">历史评定记录</h3>
          <div class="space-y-2 max-h-56 overflow-y-auto">
            <div v-for="h in history" :key="h.id" class="flex items-center gap-3 p-3 rounded-2xl hover:bg-ink-50">
              <Icon name="clock" class="text-brand-500" :size="15"/>
              <span class="text-sm text-ink-700">{{ h.title }}</span>
              <span class="ml-auto text-sm font-bold text-brand-700">U={{ fmt(h.result?.U) }}</span>
              <button class="text-ink-400 hover:text-rose-500" @click="remove(h)"><Icon name="trash":size="14"/></button>
            </div>
            <div v-if="!history.length" class="text-center text-sm text-ink-400 py-6">暂无记录</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import Icon from '../../components/Icon.vue';
import Formula from '../../components/Formula.vue';
import { api } from '../../api';
import { onMounted } from 'vue';
const title = ref(''), vals = ref([]), delta = ref(0), dist = ref('uniform'), kf = ref(2);
const history = ref([]);
const C = { uniform: Math.sqrt(3), normal: 3, tri: Math.sqrt(6) };

const r = computed(()=>{
  const xs = vals.value.filter(v=>v!=null&&!isNaN(v));
  const n = xs.length;
  let mean=0, s=0, uA=0;
  if(n){ mean = xs.reduce((a,b)=>a+b,0)/n; }
  if(n>1){ s = Math.sqrt(xs.reduce((a,b)=>a+(b-mean)**2,0)/(n-1)); uA=s/Math.sqrt(n); }
  const uB = delta.value? delta.value/C[dist.value] : 0;
  const uC = Math.sqrt(uA**2+uB**2);
  return { n, mean, s, uA, uB, uC, U: kf.value*uC };
});
const fmt = v => v==null||isNaN(v)?'—':Math.round(v*1000)/1000;
function demo(){ title.value='霍尔电压VH不确定度评定'; vals.value=[47.6,48.1,47.9,48.3,47.8]; delta.value=0.5; dist.value='uniform'; kf.value=2; }
async function save(){
  if(!title.value) return alert('请填写标题');
  await api('/workshop/uncertainty',{method:'POST',body:{title:title.value,
    inputs:{vals:vals.value,delta:delta.value,dist:dist.value,k:kf.value},result:r.value}});
  load();
}
async function remove(h){ await api('/workshop/uncertainty/'+h.id,{method:'DELETE'}); load(); }
async function load(){ history.value = await api('/workshop/uncertainty'); }
onMounted(load);
</script>