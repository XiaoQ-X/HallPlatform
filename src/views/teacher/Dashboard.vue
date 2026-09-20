<template>
  <div class="space-y-5 max-w-7xl">
    <label class="block max-w-xs">班级<select v-model="classId" @change="load" class="input"><option value="">全部任课班级</option><option v-for="c in classes" :value="c.id">{{c.name}}</option></select></label>
    <div class="flex flex-wrap gap-5 text-sm"><span>待核验实验：{{d.unverified||0}}</span><span>未解决清洗记录：{{d.unresolved||0}}</span></div>
    <div v-if="loading" class="space-y-5 animate-pulse">
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4"><div v-for="i in 4" :key="i" class="h-28 rounded-3xl bg-white/70"></div></div>
      <div class="h-72 rounded-3xl bg-white/70"></div>
      <p class="text-sm text-ink-400 text-center">正在加载看板数据…</p>
    </div>
    <!-- KPI -->
    <div v-if="!loading" class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div v-for="k in kpis" :key="k.label" class="card-hover p-5 relative overflow-hidden">
        <div class="w-11 h-11 rounded-2xl flex items-center justify-center" :style="{background:k.bg,color:k.color}"><Icon :name="k.icon"/></div>
        <div class="text-2xl font-bold text-ink-900 mt-3">{{ k.value }}</div>
        <div class="text-xs text-ink-400">{{ k.label }}</div>
      </div>
    </div>

    <!-- 图表 -->
    <div v-if="!loading" class="grid lg:grid-cols-3 gap-5">
      <div class="card p-6 lg:col-span-2">
        <h3 class="font-bold text-ink-900 mb-2">近 7 日实验活跃趋势</h3>
        <Chart :option="trendOpt" height="300px"/>
      </div>
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-2">平均成绩概览</h3>
        <Chart :option="gaugeOpt" height="300px"/>
      </div>
    </div>

    <div v-if="!loading" class="grid lg:grid-cols-2 gap-5">
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-2">常见异常 / 错误统计</h3>
        <Chart :option="abnOpt" height="320px"/>
      </div>
      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-3">教学资源数量</h3>
        <div class="grid grid-cols-2 gap-3">
          <div v-for="(v,k) in d.resourceCount" :key="k" class="p-5 rounded-2xl flex items-center gap-3" :style="{background:resBg[k]}">
            <Icon :name="resIcon[k]" class="text-white"/><div><b class="text-xl text-white">{{ v }}</b><div class="text-xs text-white/80">{{ resLabel[k] }}</div></div>
          </div>
        </div>
        <div class="mt-4 grid grid-cols-2 gap-3 text-sm">
          <div class="p-4 rounded-2xl bg-ink-50">已提交课题评阅率<b class="block text-lg text-ink-900">{{ d.subCount?Math.round(d.gradedCount*100/d.subCount):0 }}% 已评</b></div>
          <div class="p-4 rounded-2xl bg-ink-50">学生完成实验比例<b class="block text-lg text-ink-900">{{ d.completionRate }}%</b></div>
        </div>
      </div>
    </div>

    <!-- 学生进度 -->
    <div v-if="!loading" class="card p-6">
      <h3 class="font-bold text-ink-900 mb-3">学生实验进度</h3>
      <div class="overflow-x-auto"><table class="w-full text-sm">
        <thead><tr class="text-xs text-ink-400">
          <th class="text-left py-2 font-medium">姓名</th><th class="font-medium">班级</th>
          <th class="font-medium">实验会话</th><th class="font-medium">完成</th>
          <th class="font-medium">异常数</th><th class="font-medium">课题提交</th><th class="font-medium">自测综合</th></tr></thead>
        <tbody>
          <tr v-for="s in students" :key="s.id" class="border-t border-ink-50 text-center">
            <td class="text-left py-3 flex items-center gap-2 pl-2"><span>{{ s.avatar }}</span>{{ s.name }}</td>
            <td>{{ s.class_name }}</td><td>{{ s.sessions }}</td>
            <td><span :class="['chip text-[10px]',s.finished?'!bg-brand-100 text-brand-800':'!bg-amber-100 text-amber-800']">{{ s.finished }}</span></td>
            <td :class="s.errors?'text-rose-500 font-medium':''">{{ s.errors }}</td>
            <td>{{ s.subs }}</td><td class="text-brand-700">{{ s.quizBest||'—' }}</td>
          </tr>
        </tbody></table></div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import Chart from '../../components/Chart.vue';
import { api } from '../../api';
import {abnormalName} from '../../abnormal-codes';
const d = ref({ resourceCount:{}, abnormalDist:[], dailySessions:[] });
const students = ref([]);
const classes=ref([]),classId=ref('');
const loading=ref(true);
async function load(){loading.value=true;try{
  d.value=await api('/teacher/dashboard?class_id='+classId.value);
  students.value=await api('/teacher/students?class_id='+classId.value);
}finally{loading.value=false;}}
const kpis = computed(()=>[
  { label:'学生人数', value:d.value.studentCount, icon:'users', bg:'#e6faf5', color:'#078775' },
  { label:'实验会话', value:d.value.sessionCount, icon:'flask', bg:'#eaf2ff', color:'#2563eb' },
  { label:'实验完成率', value:d.value.completionRate+'%', icon:'check', bg:'#ecfdf5', color:'#059669' },
  { label:'课题提交', value:d.value.subCount, icon:'folder', bg:'#fff4e6', color:'#ea580c' }
]);
const resLabel={cases:'应用案例',ideology:'思政素材',questions:'题目',projects:'课题包'};
const resIcon={cases:'book',ideology:'star',questions:'help',projects:'folder'};
const resBg={cases:'linear-gradient(135deg,#14b8a6,#0d9488)',ideology:'linear-gradient(135deg,#f59e0b,#ea580c)',
  questions:'linear-gradient(135deg,#3b82f6,#2563eb)',projects:'linear-gradient(135deg,#ec4899,#db2777)'};

const trendOpt=computed(()=>({
  grid:{left:48,right:20,top:24,bottom:36}, tooltip:{trigger:'axis'},
  xAxis:{type:'category',data:d.value.dailySessions.map(x=>(x.d||'').slice(5)),axisLine:{lineStyle:{color:'#aebccd'}}},
  yAxis:{type:'value',splitLine:{lineStyle:{color:'#eef2f6'}}},
  series:[{type:'line',data:d.value.dailySessions.map(x=>x.c),smooth:true,symbolSize:9,
    lineStyle:{width:3,color:'#0d9488'},itemStyle:{color:'#0d9488'},
    areaStyle:{color:{type:'linear',x:0,y:0,x2:0,y2:1,colorStops:[{offset:0,color:'rgba(13,148,136,.25)'},{offset:1,color:'rgba(13,148,136,0)'}]}}}]
}));
const abnOpt=computed(()=>{
  const rows=d.value.abnormalDist.slice(0,8);
  return { grid:{left:150,right:24,top:10,bottom:30}, tooltip:{},
    xAxis:{type:'value',splitLine:{lineStyle:{color:'#eef2f6'}}},
    yAxis:{type:'category',data:rows.map(r=>abnormalName(r.code)),axisLine:{lineStyle:{color:'#aebccd'}}},
    series:[{type:'bar',data:rows.map(r=>r.count),barWidth:14,
      itemStyle:{borderRadius:[0,8,8,0],
        color:{type:'linear',x:0,y:0,x2:1,y2:0,colorStops:[{offset:0,color:'#fb7185'},{offset:1,color:'#e11d48'}]}}}]};
});
const gaugeOpt=computed(()=>({
  series:[
    {type:'gauge',center:['50%','38%'],radius:'62%',min:0,max:100,
      progress:{show:true,width:12,itemStyle:{color:'#0d9488'}},axisLine:{lineStyle:{width:12,color:[[1,'#eef2f6']]}},
      pointer:{width:4},axisTick:{show:false},splitLine:{show:false},axisLabel:{distance:18,fontSize:9,color:'#aebccd'},
      title:{offsetCenter:[0,'26%'],fontSize:12,color:'#7e92ab'},detail:{offsetCenter:[0,'-4%'],fontSize:20,formatter:'{value}',color:'#0d9488'},
      data:[{value:d.value.quizAvg,name:'自测均分'}]},
    {type:'gauge',center:['50%','86%'],radius:'62%',min:0,max:100,
      progress:{show:true,width:12,itemStyle:{color:'#f59e0b'}},axisLine:{lineStyle:{width:12,color:[[1,'#eef2f6']]}},
      pointer:{width:4},axisTick:{show:false},splitLine:{show:false},axisLabel:{distance:18,fontSize:9,color:'#aebccd'},
      title:{offsetCenter:[0,'26%'],fontSize:12,color:'#7e92ab'},detail:{offsetCenter:[0,'-4%'],fontSize:20,formatter:'{value}',color:'#f59e0b'},
      data:[{value:d.value.projectAvg,name:'课题均分'}]}
]}));

onMounted(async()=>{classes.value=await api('/auth/classes');await load();});
</script>
