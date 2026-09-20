<template>
  <div class="max-w-5xl space-y-5">
    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-4 flex items-center gap-2"><Icon name="send" class="text-brand-600"/> 新建资源推送</h3>
      <div class="grid md:grid-cols-2 gap-3">
        <div><label class="text-xs text-ink-400">推送对象类型</label>
          <select v-model="form.target_type" class="input"><option value="class">整个班级</option><option value="student">指定学生</option></select></div>
        <div><label class="text-xs text-ink-400">选择对象</label>
          <select v-if="form.target_type==='class'" v-model.number="form.target_id" class="input">
            <option v-for="c in classes" :value="c.id">{{ c.name }}</option></select>
          <select v-else v-model.number="form.target_id" class="input">
            <option v-for="s in students" :value="s.id">{{ s.name }}</option></select></div>
      </div>
      <label class="text-xs text-ink-400 mt-3 block">推送标题</label>
      <input v-model="form.title" class="input mt-1"/>
      <label class="text-xs text-ink-400 mt-3 block">消息内容</label>
      <textarea v-model="form.message" class="input min-h-20 mt-1"></textarea>
      <div class="grid md:grid-cols-2 gap-3 mt-3">
        <div><label class="text-xs text-ink-400">关联资源类型</label>
          <select v-model="form.resource_type" class="input">
            <option value="">不关联</option><option value="case">应用案例</option>
            <option value="ideology">思政素材</option><option value="quiz">自测卷</option>
            <option value="project">课题包</option></select></div>
        <div><label class="text-xs text-ink-400">资源</label>
          <select v-if="form.resource_type" v-model.number="form.resource_id" class="input">
            <option v-for="r in resOptions" :key="r.id" :value="r.id">{{ r.title }}</option></select>
          <input v-else class="input" disabled placeholder="选择类型后可关联"/></div>
      </div>
      <button class="btn-primary w-full mt-5" :disabled="sending" @click="send"><Icon name="send":size="16"/> 立即推送</button>
    </div>

    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-3">推送历史</h3>
      <div class="space-y-2.5">
        <div v-for="p in history" :key="p.id" class="flex items-center gap-3 p-4 rounded-2xl bg-ink-50">
          <Icon name="send" class="text-brand-600"/><b class="text-sm">{{ p.title }}</b>
          <span class="text-xs text-ink-400">{{ p.target_name||'学生#'+p.target_id }}</span>
          <span class="ml-auto text-xs text-ink-400">{{ p.created_at }}</span>
        </div>
        <div v-if="!history.length" class="text-center text-sm text-ink-400 py-6">暂无推送</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const form=ref({target_type:'class',target_id:null,title:'',message:'',resource_type:'',resource_id:null});
const classes=ref([]),students=ref([]),history=ref([]),resOptions=ref([]);
const sending=ref(false);
watch(()=>form.value.target_type,()=>form.value.target_id=null);
watch(()=>form.value.resource_type, async t=>{
  resOptions.value=[]; form.value.resource_id=null;
  const map={case:'/resources/cases',ideology:'/resources/ideology',quiz:'/resources/quizzes',project:'/resources/projects'};
  if(map[t]){const rows=await api(map[t]);if(form.value.resource_type===t)resOptions.value=rows.filter(r=>r.published&&!r.archived);}
});
async function send(){
  if(sending.value)return;
  if(!form.value.title||!form.value.target_id)return alert('请完善推送信息');
  sending.value=true;try{
  await api('/teacher/pushes',{method:'POST',body:form.value});
  form.value={target_type:'class',target_id:null,title:'',message:'',resource_type:'',resource_id:null};
  await load();}finally{sending.value=false;}
}
async function load(){
  classes.value=await api('/auth/classes');
  students.value=await api('/teacher/students');
  history.value=await api('/teacher/pushes');
}
onMounted(load);
</script>
