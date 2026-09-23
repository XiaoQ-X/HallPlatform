<template>
  <section class="card p-4 sm:p-5" aria-labelledby="algorithm-flow-title">
    <div class="flex flex-wrap items-start justify-between gap-3 mb-4">
      <div>
        <div class="flex items-center gap-2">
          <Icon name="layers" :size="18" class="text-brand-600" />
          <h2 id="algorithm-flow-title" class="font-bold text-ink-900">算法处理流程</h2>
        </div>
        <p class="text-xs text-ink-400 mt-1">当前霍尔效应副效应页面的输入、计算、修正与显示链路</p>
      </div>
      <span class="chip-gray text-[11px]">数据流向 →</span>
    </div>

    <div class="flex flex-col lg:flex-row lg:items-stretch gap-2.5 lg:gap-2">
      <template v-for="(step, index) in steps" :key="step.title">
        <div :class="['flex-1 min-w-0 rounded-2xl border px-3.5 py-3', step.panel]">
          <div class="flex items-start gap-2.5">
            <span :class="['shrink-0 w-7 h-7 rounded-xl flex items-center justify-center text-xs font-bold text-white', step.badge]">
              {{ String(index + 1).padStart(2, '0') }}
            </span>
            <div class="min-w-0">
              <div class="text-sm font-semibold text-ink-900 leading-6">{{ step.title }}</div>
              <div class="text-[11px] text-ink-600 leading-5 mt-0.5">{{ step.detail }}</div>
            </div>
          </div>
          <div v-if="step.formula" class="mt-2.5 rounded-xl bg-white/70 px-2.5 py-1.5 text-xs font-mono text-ink-700">
            {{ step.formula }}
          </div>
        </div>
        <div v-if="index < steps.length - 1" class="flex items-center justify-center lg:w-5 text-brand-400" aria-hidden="true">
          <Icon name="chevronR" :size="18" class="rotate-90 lg:rotate-0" />
        </div>
      </template>
    </div>
  </section>
</template>

<script setup>
import Icon from './Icon.vue';

const steps = [
  {
    title: '输入实验参数',
    detail: '输入工作电流 I、磁感应强度 B，以及 RH、d、r0、kE、kN、kRL',
    panel: 'border-sky-200 bg-sky-50/70',
    badge: 'bg-sky-500'
  },
  {
    title: '核心算法计算',
    detail: '按当前页面直接输入的 B 计算理想霍尔电压 VH',
    formula: 'VH = RH · I · B / d',
    panel: 'border-emerald-200 bg-emerald-50/70',
    badge: 'bg-emerald-500'
  },
  {
    title: '叠加误差模型',
    detail: '叠加四种横向磁效应，并单列接触偏移 V0',
    formula: 'V⊥ = VH + VE + VN + VRL；V0 = r0 · I',
    panel: 'border-amber-200 bg-amber-50/80',
    badge: 'bg-amber-500'
  },
  {
    title: '输出与修正',
    detail: '四方向消除 V0、VN、VRL；已知 kE 时再分离 VE，得到理想 VH',
    formula: 'Vcorr = (V1−V2+V3−V4)/4 = VH + VE',
    panel: 'border-violet-200 bg-violet-50/70',
    badge: 'bg-violet-500'
  }
];
</script>
