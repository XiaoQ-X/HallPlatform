<template>
  <div class="max-w-5xl space-y-5">
    <div class="rounded-4xl p-8 text-white shadow-lift relative overflow-hidden"
      style="background:linear-gradient(120deg,#0a4740,#078775 60%,#0284c7)">
      <div class="absolute right-10 top-4 text-7xl opacity-20">⚖️</div>
      <h2 class="text-2xl font-bold">反馈与申诉</h2>
      <p class="text-white/75 mt-2">对评价或成绩有异议？理性说明，教师会认真复核并回复。</p>
    </div>

    <div class="card p-6" v-if="!isTeacher">
      <h3 class="font-bold text-ink-900 mb-3">发起申诉</h3>
      <textarea v-model="content" class="input min-h-24" placeholder="请说明申诉对象、具体理由与依据…"></textarea>
      <button class="btn-primary mt-3" @click="submit"><Icon name="send":size="16"/> 提交申诉</button>
    </div>

    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-4">{{ isTeacher?'全部申诉':'我的申诉记录' }}</h3>
      <div class="space-y-3">
        <div v-for="a in appeals" :key="a.id" class="p-5 rounded-2xl" :class="a.status==='已回复'?'bg-brand-50/50':'bg-amber-50/60'">
          <div class="flex items-center gap-3">
            <span v-if="isTeacher" class="text-sm text-ink-500">{{ a.student_name }}</span>
            <span class="chip-gray text-[11px]">{{ a.ref_type }} #{{ a.ref_id }}</span>
            <span :class="['ml-auto chip text-[11px]',a.status==='已回复'?'!bg-brand-100 text-brand-800':'!bg-amber-100 text-amber-800']">{{ a.status }}</span>
          </div>
          <p class="text-sm text-ink-700 mt-3 leading-7">{{ a.content }}</p>
          <div v-if="a.reply" class="mt-3 p-4 rounded-2xl bg-white text-sm text-ink-600 leading-7">
            <b class="text-brand-700">教师回复：</b>{{ a.reply }}<div class="text-[11px] text-ink-400 mt-1">{{ a.replied_at }}</div>
          </div>
          <div v-if="isTeacher && a.status!=='已回复'" class="mt-3 flex gap-2">
            <input v-model="replies[a.id]" class="input flex-1" placeholder="输入复核意见与处理结果…"/>
            <button class="btn-primary" @click="reply(a)">回复</button>
          </div>
          <div class="text-[11px] text-ink-400 mt-2">{{ a.created_at }}</div>
        </div>
        <div v-if="!appeals.length" class="text-center text-sm text-ink-400 py-8">暂无申诉</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
import { useAuth } from '../../stores/auth';
const auth = useAuth();
const isTeacher = ref(false);
const appeals = ref([]), content = ref(''), replies = ref({});
let pre = null;
try { pre = JSON.parse(sessionStorage.getItem('appeal_ref')); sessionStorage.removeItem('appeal_ref'); } catch {}
async function submit(){
  if(!content.value)return alert('请填写申诉内容');
  await api('/peer/appeals',{method:'POST',body:{ref_type:pre?.ref_type||'general',ref_id:pre?.ref_id||0,content:content.value}});
  content.value=''; pre=null; await load();
}
async function reply(a){
  if(!replies.value[a.id])return;
  await api('/peer/appeals/'+a.id+'/reply',{method:'POST',body:{reply:replies.value[a.id]}});
  load();
}
async function load(){ appeals.value=await api('/peer/appeals'); }
onMounted(async()=>{ await auth.fetchMe(); isTeacher.value = auth.user?.role==='teacher'; load(); });
</script>
