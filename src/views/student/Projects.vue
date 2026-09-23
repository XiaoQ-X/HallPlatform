<template>
  <div class="max-w-7xl">
    <div v-if="!current">
      <div class="rounded-4xl p-8 mb-6 text-white shadow-lift relative overflow-hidden"
        style="background:linear-gradient(120deg,#0a4740,#078775 60%,#0284c7)">
        <div class="absolute right-10 top-4 text-7xl opacity-20">🧭</div>
        <h2 class="text-2xl font-bold">自主探究课题包</h2>
        <p class="text-white/75 mt-2 max-w-xl">从工程设计到数据探究，带着真实问题出发，在动手实践中深化对霍尔效应的理解。</p>
      </div>
      <div class="grid md:grid-cols-2 xl:grid-cols-3 gap-5">
        <div v-for="p in rows" :key="p.id" class="card-hover p-6 cursor-pointer" @click="open(p)">
          <div class="w-14 h-14 rounded-2xl flex items-center justify-center text-2xl text-white shadow-soft" :style="{background:'linear-gradient(135deg,'+p.color+','+p.color+'cc)'}">{{ p.cover }}</div>
          <span class="chip-gray mt-4">{{ p.category }}</span>
          <div class="font-bold text-ink-900 mt-2 leading-6">{{ p.title }}</div>
          <div class="text-sm text-ink-400 mt-1 line-clamp-2 leading-6">{{ p.subtitle }}</div>
          <div class="mt-4 text-brand-600 text-sm flex items-center gap-1">查看课题 <Icon name="chevronR":size="15"/></div>
        </div>
      </div>
    </div>

    <div v-else class="animate-fadeUp max-w-4xl mx-auto">
      <button class="btn-ghost mb-5" @click="current=null"><Icon name="chevronL":size="16"/> 返回课题列表</button>
      <div class="card overflow-hidden">
        <div class="p-8 text-white relative" :style="{background:'linear-gradient(125deg,'+current.color+',#0a4740)'}">
          <div class="absolute right-8 top-6 text-6xl opacity-30">{{ current.cover }}</div>
          <span class="chip bg-white/20 text-white">{{ current.category }}</span>
          <h2 class="text-2xl font-bold mt-3">{{ current.title }}</h2>
          <p class="text-white/80 mt-1.5">{{ current.subtitle }}</p>
        </div>
        <div class="p-8 space-y-7">
          <RichText :html="current.guide"/>

          <div>
            <h3 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="check" class="text-brand-600"/> 任务清单</h3>
            <div class="space-y-2">
              <label v-for="(t,i) in current.tasks" :key="i" class="flex items-center gap-3 p-3 rounded-2xl hover:bg-ink-50 cursor-pointer">
                <input type="checkbox" v-model="t.done" @change="saveProgress" class="w-5 h-5 accent-brand-600"/>
                <span :class="['text-sm',t.done?'line-clamp-0 text-ink-400 line-through':'text-ink-700']">{{ t.t }}</span>
                <span v-if="!t.r" class="ml-auto chip-gray text-[10px]">选做</span>
              </label>
            </div>
          </div>

          <div v-if="current.attachments?.length">
            <h3 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="download" class="text-brand-600"/> 资料附件</h3>
            <div v-for="(a,i) in current.attachments" :key="i" class="flex items-center gap-3 p-3 rounded-2xl bg-ink-50 mb-2">
              <ProtectedFile v-if="a.url" :src="a.url" :name="a.name||a.n"/><span v-else>{{a.n}}（待教师补充文件）</span>
            </div>
          </div>

          <!-- 提交区 -->
          <div class="rounded-3xl border border-ink-100 p-6 bg-ink-50/50">
            <h3 class="font-bold text-ink-900 mb-3">{{ current.mine?'更新我的提交':'提交探究成果' }}</h3>
            <textarea v-model="content" :disabled="current.mine?.grade!=null" class="input min-h-32 py-3" placeholder="记录你的探究过程、数据分析与结论…"></textarea>
            <div class="mt-3 flex flex-wrap gap-3 items-center">
              <label class="btn-ghost cursor-pointer">
                <Icon name="upload":size="16"/> 上传报告/视频
                <input type="file" class="hidden" :disabled="current.mine?.grade!=null||uploading" @change="onFile"/>
              </label>
              <span v-if="upProg" class="text-xs text-ink-400">{{ Math.round(upProg*100) }}%</span>
            </div>
            <div v-if="files.length" class="mt-3 space-y-1.5">
              <div v-for="(f,i) in files" :key="i" class="flex items-center gap-2 text-sm bg-white rounded-2xl px-3 py-2">
                <Icon name="folder" class="text-brand-600" :size="15"/>{{ f.name }}
                <button :disabled="current.mine?.grade!=null" class="ml-auto text-ink-400 hover:text-rose-500" @click="files.splice(i,1)"><Icon name="x":size="14"/></button>
              </div>
            </div>
            <button class="btn-primary w-full mt-4" :disabled="current.mine?.grade!=null||uploading||saving" @click="submit">{{current.mine?.grade!=null?'已评分，待教师允许重交':uploading?'附件上传中':saving?'提交中':'提交课题成果'}}</button>
            <p v-if="current.mine?.grade==null&&current.mine?.feedback" class="mt-3 text-amber-700">退回意见：{{current.mine.feedback}}</p>
            <details v-if="history.length" class="mt-3"><summary>历史提交与评分</summary><div v-for="h in history" :key="h.id" class="border-b py-3"><b>v{{h.version}} · {{h.payload.grade==null?'未评':h.payload.grade+'分'}}</b><p>{{h.payload.feedback}}</p><p class="whitespace-pre-wrap">{{h.payload.content}}</p><ProtectedFile v-for="f in JSON.parse(h.payload.files||'[]')" :src="f.url" :name="f.name"/></div></details>
            <div v-if="current.mine?.grade!=null" class="mt-4 rounded-2xl bg-brand-50 p-4 text-sm">
              <b class="text-brand-700">成绩：{{ current.mine.grade }} 分</b>
              <p class="text-ink-600 mt-1">{{ current.mine.feedback||'暂无教师反馈' }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue';
import Icon from '../../components/Icon.vue';
import RichText from '../../components/RichText.vue';
import ProtectedFile from '../../components/ProtectedFile.vue';
import {useRoute} from 'vue-router';
import { api, upload } from '../../api';
const rows = ref([]), current = ref(null);
const content = ref(''), files = ref([]), upProg = ref(0);
const history=ref([]),uploading=ref(false),saving=ref(false);
const route=useRoute();
async function open(p){ current.value = await api('/resources/projects/'+p.id);
  current.value.tasks.forEach((t,i)=>t.done=!!current.value.progress?.[i]);
  content.value = current.value.mine?.content||'';
  files.value = current.value.mine?current.value.mine.files||[]:[];history.value=current.value.mine?await api('/resources/submissions/'+current.value.mine.id+'/versions'):[]; }
async function saveProgress(){await api('/resources/projects/'+current.value.id+'/progress',{method:'PUT',body:{progress:current.value.tasks.map(t=>!!t.done)}});}
async function onFile(e){ const f=e.target.files[0];if(!f)return;
  const projectId = current.value?.id;
  uploading.value=true;try{
    const r = await upload(f, p=>upProg.value=p);
    // 上传期间用户可能已经返回列表或打开了另一个课题，不能把附件挂到新课题。
    if (current.value?.id !== projectId) return;
    files.value.push(r);
  }finally{upProg.value=0;uploading.value=false;} }
async function submit(){
  if(saving.value||uploading.value)return;saving.value=true;try{await saveProgress();await api('/resources/projects/'+current.value.id+'/submit',
    {method:'POST',body:{content:content.value,files:files.value}});
  await open({id:current.value.id});
  alert('提交成功，等待教师评阅');}finally{saving.value=false;}
}
onMounted(async()=>{ rows.value = await api('/resources/projects');if(route.query.id)await open({id:route.query.id}); });
watch(()=>route.query.id,id=>{if(id)open({id});else current.value=null;});
</script>
