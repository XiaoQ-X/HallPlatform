<template>
  <div class="max-w-7xl space-y-5">
    <!-- 工具栏 -->
    <div class="card p-4">
      <div class="flex flex-wrap items-center gap-2">
        <button v-for="t in tabs" :key="t.k" @click="active=t.k"
          :class="['px-3.5 py-1.5 rounded-full text-xs font-medium transition', active===t.k?'bg-brand-600 text-white shadow-soft':'bg-ink-50 text-ink-500']">
          {{ t.label }}<span class="ml-1 opacity-70">{{ countOf(t.k) }}</span>
        </button>
      </div>
      <div class="text-sm text-ink-400 mt-3">共 {{ rows.length }} 名学生 · 已评分 {{ countOf('graded') }} · 已提交 {{ countOf('submitted') }}</div>
    </div>

    <!-- 无任何学生：可操作空状态 -->
    <div v-if="loadError" class="card p-10 text-center">
      <div class="text-4xl mb-3">⚠️</div>
      <div class="font-bold text-ink-800 mb-2">实验进度加载失败</div>
      <p class="text-sm text-rose-600 mb-5">{{ loadError }}</p>
      <button class="btn-primary" @click="load">重新加载</button>
    </div>
    <div v-else-if="loading" class="card p-10 text-center text-sm text-ink-400">正在加载学生实验进度…</div>
    <div v-else-if="!rows.length" class="card p-10 text-center">
      <div class="text-5xl mb-3">🧑‍🏫</div>
      <div class="font-bold text-ink-800 mb-1">还没有可管理的学生</div>
      <p class="text-sm text-ink-400 mb-5">请先在「学生管理」中创建班级与学生账号，再查看副效应实验进度。</p>
      <router-link to="/teacher/students" class="btn-primary inline-flex"><Icon name="users" :size="16" /> 前往学生管理</router-link>
    </div>

    <!-- 当前状态下无数据 -->
    <div v-else-if="!filtered.length" class="card p-10 text-center">
      <div class="text-4xl mb-3">🗂️</div>
      <div class="font-bold text-ink-800 mb-1">「{{ activeLabel }}」状态暂无学生</div>
      <p class="text-sm text-ink-400 mb-4">可切换其他状态查看，或向学生推送实验任务。</p>
      <div class="flex justify-center gap-2">
        <button class="btn-soft" @click="active='all'">查看全部</button>
        <router-link to="/teacher/pushes" class="btn-ghost">推送实验</router-link>
      </div>
    </div>

    <!-- 学生卡片网格 -->
    <div v-else class="grid sm:grid-cols-2 xl:grid-cols-3 gap-5">
      <div v-for="s in filtered" :key="s.id" class="card p-5 flex flex-col">
        <div class="flex items-center gap-3">
          <div class="w-11 h-11 rounded-2xl bg-brand-50 flex items-center justify-center text-xl">{{ s.avatar }}</div>
          <div class="min-w-0">
            <div class="font-bold text-ink-900 truncate">{{ s.name }}</div>
            <div class="text-[11px] text-ink-400 truncate">{{ s.student_no||'无学号' }} · {{ s.class_name||'未分班' }}</div>
          </div>
          <span class="ml-auto text-[11px] rounded-full px-2.5 py-1 font-medium" :class="STATUS[s.status].cls">{{ STATUS[s.status].label }}</span>
        </div>

        <div class="grid grid-cols-4 gap-2 text-center mt-4">
          <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">理想VH</div><b class="text-[12px] text-brand-700">{{ f(s.key?.idealVH_mV) }}</b></div>
          <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">V0</div><b class="text-[12px] text-amber-600">{{ f(s.key?.V0_mV) }}</b></div>
          <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">VE</div><b class="text-[12px] text-rose-600">{{ f(s.key?.VE_mV) }}</b></div>
          <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">修正值</div><b class="text-[12px] text-sky-700">{{ f(s.key?.corrected_mV) }}</b></div>
        </div>

        <div class="flex items-center gap-2 mt-3 text-[11px]">
          <span :class="['status-pill', s.reversal?'on-emerald':'']"><Icon name="check" :size="12"/> 正反向{{ s.reversal?'已完成':'未完成' }}</span>
          <span v-if="s.grade" class="ml-auto text-amber-600 font-bold">成绩 {{ Math.round(s.grade.total) }}</span>
        </div>
        <div class="text-[11px] text-ink-400 mt-2">最后实验：{{ s.lastTime||'—' }}</div>

        <router-link :to="'/teacher/side-effects/'+s.id" class="btn-soft w-full mt-3 text-xs justify-center">
          <Icon name="layers" :size="14" /> 查看记录详情
        </router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';

const rows = ref([]);
const loading = ref(true), loadError = ref('');
const active = ref('all');
const STATUS = {
  not_started: { label: '未开始', cls: 'bg-ink-100 text-ink-500' },
  in_progress: { label: '进行中', cls: 'bg-sky-100 text-sky-700' },
  completed: { label: '已完成', cls: 'bg-emerald-100 text-emerald-700' },
  submitted: { label: '已提交', cls: 'bg-brand-100 text-brand-700' },
  graded: { label: '已评分', cls: 'bg-amber-100 text-amber-700' }
};
const tabs = [
  { k: 'all', label: '全部' },
  { k: 'not_started', label: '未开始' },
  { k: 'in_progress', label: '进行中' },
  { k: 'completed', label: '已完成' },
  { k: 'submitted', label: '已提交' },
  { k: 'graded', label: '已评分' }
];
const activeLabel = computed(()=> tabs.find(t=>t.k===active.value)?.label || '');
const countOf = k => k==='all' ? rows.value.length : rows.value.filter(s=>s.status===k).length;
const filtered = computed(()=> active.value==='all' ? rows.value : rows.value.filter(s=>s.status===active.value));
const f = v => Number.isFinite(v) ? Number(v).toFixed(2) : '—';

async function load(){
  loading.value=true; loadError.value='';
  try{ rows.value = await api('/teacher/side-effects'); }
  catch(e){ rows.value=[]; loadError.value=e.message||'请求失败，请稍后重试'; }
  finally{ loading.value=false; }
}
onMounted(load);
</script>
