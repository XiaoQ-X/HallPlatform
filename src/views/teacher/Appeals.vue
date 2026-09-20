<template>
  <div class="max-w-4xl space-y-5">
    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-1 flex items-center gap-2"><Icon name="chat" class="text-brand-600"/> 学生申诉处理</h3>
      <p class="text-xs text-ink-400">请在核实原始评价/成绩后给出公正、明确的复核结论。</p>
    </div>

    <div v-for="a in rows" :key="a.id" class="card p-6">
      <div class="flex items-center gap-3">
        <span class="chip-gray text-[11px]">{{ a.ref_type==='review'?'同伴评价':'成绩' }} #{{ a.ref_id }}</span>
        <b class="text-sm text-ink-800">{{ a.student_name }}</b>
        <span :class="['ml-auto chip text-[11px]',a.status==='已回复'?'!bg-brand-100 text-brand-800':'!bg-amber-100 text-amber-800']">{{ a.status }}</span>
      </div>
      <p class="text-sm text-ink-600 mt-3 p-4 rounded-2xl bg-ink-50 leading-7">{{ a.content }}</p>
      <div v-if="a.reply" class="mt-3 p-4 rounded-2xl bg-brand-50 text-sm text-brand-900 leading-7">
        <b>复核结论：</b>{{ a.reply }}<div class="text-[11px] text-brand-600 mt-1">{{ a.replied_at }}</div></div>
      <template v-else>
        <details class="mt-3"><summary>原始证据快照</summary><pre class="text-xs whitespace-pre-wrap break-words">{{JSON.stringify(a.snapshot,null,2)}}</pre></details>
        <select v-model="decisions[a.id]" class="input mt-3"><option value="uphold">维持原评价 / 回复</option><option v-if="a.ref_type==='review'" value="void">撤销该评价并重新计算草稿成绩</option></select>
        <textarea v-model="drafts[a.id]" class="input min-h-20 mt-3" placeholder="填写复核结论与处理结果…"></textarea>
        <button class="btn-primary w-full mt-3" @click="reply(a)"><Icon name="check":size="16"/> 提交复核结论</button>
      </template>
    </div>

    <div v-if="!rows.length" class="card p-12 text-center text-sm text-ink-400">暂无待处理申诉</div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const rows = ref([]), drafts = ref({});
const decisions=ref({});
async function reply(a){
  const text=(drafts.value[a.id]||'').trim();
  if(!text)return alert('请填写复核结论');
  await api('/peer/appeals/'+a.id+'/reply',{method:'POST',body:{reply:text,decision:decisions.value[a.id]||'uphold'}});
  load();
}
async function load(){ rows.value = await api('/peer/appeals'); }
onMounted(load);
</script>
