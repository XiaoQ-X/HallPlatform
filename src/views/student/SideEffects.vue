<template>
  <div class="max-w-6xl mx-auto">
    <!-- 实验目标 -->
    <section class="card p-6 mb-6">
      <div class="flex items-center gap-2 mb-2">
        <Icon name="gauge" class="text-brand-600" :size="22" />
        <h2 class="text-lg font-bold text-ink-900">实验目标 · 副效应与误差修正</h2>
      </div>
      <p class="text-sm text-ink-600 leading-7">
        认识霍尔电压测量中的 <b>不等位电势 V<sub>0</sub></b> 与 <b>厄廷豪森效应 V<sub>E</sub></b>，
        调节电流、磁场、电极偏移与厄廷豪森参数，观察它们如何改变读数，再通过正反向（四方向对称）测量进行误差修正，分离出理想霍尔电压。
      </p>
      <div class="mt-3 rounded-2xl bg-brand-50/60 px-4 py-3">
        <Formula display expr="V_{measured}=V_H+V_0+V_E" />
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <router-link to="/sim/lab?case=side-effects" class="btn-primary">
          <Icon name="flask" :size="16" /> 进入 Unity 仿真工作台
        </router-link>
        <span class="text-xs text-ink-400">在三维工作台中操作，实验事件与结果会自动保存</span>
      </div>
    </section>

    <!-- 参数输入 + 实时结果 -->
    <div class="grid lg:grid-cols-2 gap-6 mb-6">
      <!-- 参数输入 -->
      <section class="card p-6">
        <h2 class="font-bold text-ink-900 mb-4">参数输入区</h2>
        <div class="space-y-4">
          <div v-for="row in paramRows" :key="row.key">
            <div class="flex items-center justify-between mb-1 text-sm">
              <span class="text-ink-700">{{ row.label }}</span>
              <span class="text-ink-400 text-xs">{{ row.unit }}</span>
            </div>
            <div class="flex items-center gap-3">
              <input type="range" :min="row.min" :max="row.max" :step="row.step" v-model.number="p[row.key]" class="flex-1 accent-brand-600" :aria-label="row.label+'滑块'">
              <input type="number" :min="row.min" :max="row.max" :step="row.step" v-model.number="p[row.key]" class="input w-24 text-right" :aria-label="row.label">
            </div>
          </div>
        </div>

        <!-- 测量方向切换 -->
        <div class="grid grid-cols-2 gap-3 mt-5">
          <div>
            <div class="text-sm text-ink-700 mb-1">电流方向</div>
            <div class="flex gap-2">
              <button class="btn-soft flex-1" :class="curDir===1?'!bg-brand-600 !text-white':''" @click="curDir=1">+ I</button>
              <button class="btn-soft flex-1" :class="curDir===-1?'!bg-brand-600 !text-white':''" @click="curDir=-1">− I</button>
            </div>
          </div>
          <div>
            <div class="text-sm text-ink-700 mb-1">磁场方向</div>
            <div class="flex gap-2">
              <button class="btn-soft flex-1" :class="fldDir===1?'!bg-brand-600 !text-white':''" @click="fldDir=1">+ B</button>
              <button class="btn-soft flex-1" :class="fldDir===-1?'!bg-brand-600 !text-white':''" @click="fldDir=-1">− B</button>
            </div>
          </div>
        </div>

        <!-- 样品参数（参考示例） -->
        <div class="rounded-2xl bg-ink-50 p-4 mt-5">
          <div class="text-xs text-ink-400 mb-2">样品参数（参考示例值，非真实标定，请以实际样品替换）</div>
          <div class="grid grid-cols-2 gap-3">
            <label class="text-xs text-ink-600">霍尔系数 RH (m³/C)
              <input type="number" step="0.0001" v-model.number="p.RH" class="input mt-1">
            </label>
            <label class="text-xs text-ink-600">样品厚度 d (mm)
              <input type="number" step="0.1" min="0" v-model.number="p.d_mm" class="input mt-1">
            </label>
          </div>
        </div>

        <!-- 测量按钮 -->
        <div class="flex flex-wrap gap-2 mt-5">
          <button class="btn-primary" :disabled="!!errorMsg" @click="recordCurrent">
            <Icon name="check" :size="16" /> 记录当前方向
          </button>
          <button class="btn-soft" :disabled="!!errorMsg" @click="autoMeasure">
            <Icon name="layers" :size="16" /> 四方向对称测量
          </button>
          <button class="btn-ghost" @click="clearRecords">
            <Icon name="refresh" :size="16" /> 清空
          </button>
        </div>
      </section>

      <!-- 实时结果 -->
      <section class="card p-6">
        <h2 class="font-bold text-ink-900 mb-1">实时结果区</h2>
        <div class="text-xs text-ink-400 mb-4">
          当前方向：{{ curDir>0?'+':'−' }}I，{{ fldDir>0?'+':'−' }}B（I·B 组合 {{ curDir*fldDir>0?'+':'−' }}）
        </div>

        <p v-if="errorMsg" role="alert" class="rounded-2xl bg-rose-50 text-rose-600 text-sm px-4 py-3 flex items-start gap-2 mb-4">
          <Icon name="alert" :size="18" class="mt-0.5" /> <span>{{ errorMsg }}</span>
        </p>

        <div class="space-y-4" v-if="!errorMsg">
          <div v-for="it in items" :key="it.label">
            <div class="flex items-center justify-between text-sm mb-1">
              <span class="text-ink-700">{{ it.label }}</span>
              <b :style="{ color: it.color }">{{ it.mv }} mV</b>
            </div>
            <div class="h-2 rounded-full bg-ink-100 overflow-hidden">
              <div class="h-2 rounded-full" :style="{ width: it.pct + '%', background: it.color }"></div>
            </div>
          </div>
        </div>

        <div class="divider-soft my-5"></div>
        <details class="text-sm">
          <summary class="cursor-pointer text-brand-700">当前读数的逐步解释</summary>
          <ul class="list-disc pl-5 mt-2 space-y-1 text-ink-600 leading-6">
            <li v-for="(s,i) in current.steps" :key="i">{{ s }}</li>
          </ul>
        </details>
      </section>
    </div>

    <!-- 四方向记录 + 修正结果 -->
    <div class="grid lg:grid-cols-2 gap-6 mb-6">
      <section class="card p-6">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-bold text-ink-900">四方向测量记录</h2>
          <span class="chip-gray">{{ filledCount }} / 4</span>
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div v-for="s in slots" :key="s.key" class="rounded-2xl border p-3" :class="isFinite(recorded[s.key])?'border-brand-200 bg-brand-50/50':'border-ink-100'">
            <div class="text-xs text-ink-400">{{ s.key }}（{{ s.current>0?'+':'−' }}I，{{ s.field>0?'+':'−' }}B）</div>
            <div class="font-bold text-ink-900 mt-1">{{ fmtMv(recorded[s.key]) }} <span class="text-xs font-normal text-ink-400">mV</span></div>
          </div>
        </div>
        <p class="text-xs text-ink-400 mt-3 leading-6">
          提示：可切换方向后逐次“记录当前方向”，或直接“四方向对称测量”。顺序为 V1(+，+)、V2(+，−)、V3(−，−)、V4(−，+)。
        </p>
      </section>

      <section class="card p-6">
        <h2 class="font-bold text-ink-900 mb-3">误差修正结果</h2>
        <div class="rounded-2xl bg-brand-50/60 px-4 py-3 mb-4">
          <Formula display expr="V_H=\frac{V_1-V_2+V_3-V_4}{4}" />
        </div>
        <template v-if="correction && !correction.error">
          <div class="grid grid-cols-3 gap-2 text-center">
            <div class="rounded-2xl bg-rose-50 py-3">
              <div class="text-[11px] text-ink-400">修正值 (VH+VE)</div>
              <b class="text-rose-600 text-sm">{{ fmtMv(correction.correctedHall) }}</b>
            </div>
            <div class="rounded-2xl bg-amber-50 py-3">
              <div class="text-[11px] text-ink-400">残留 VE</div>
              <b class="text-amber-500 text-sm">{{ fmtMv(correction.residual.value) }}</b>
            </div>
            <div class="rounded-2xl bg-brand-50 py-3">
              <div class="text-[11px] text-ink-400">理想 VH</div>
              <b class="text-brand-700 text-sm">{{ fmtMv(correction.idealVH) }}</b>
            </div>
          </div>
          <div class="mt-4">
            <div class="text-xs text-ink-400 mb-2">已被对称法消除：</div>
            <div class="flex flex-wrap gap-2">
              <span class="chip-gray" v-for="(e,i) in correction.eliminated" :key="i">{{ e }}</span>
            </div>
          </div>
          <details class="text-sm mt-4">
            <summary class="cursor-pointer text-brand-700">查看修正过程解释</summary>
            <ol class="list-decimal pl-5 mt-2 space-y-1 text-ink-600 leading-6">
              <li v-for="(s,i) in correction.steps" :key="i">{{ s }}</li>
            </ol>
          </details>
        </template>
        <p v-else role="alert" class="text-rose-600 text-sm">参数无效，暂无法修正，请检查上方输入。</p>
      </section>
    </div>

    <!-- 图表 -->
    <section class="mb-6">
      <h2 class="font-bold text-ink-900 mb-3 flex items-center gap-2">
        <Icon name="chart" class="text-brand-600" :size="20" /> 变量观察图表
      </h2>
      <div class="grid sm:grid-cols-2 gap-6">
        <div class="card p-4">
          <h3 class="text-sm font-semibold text-ink-800 mb-2">VH 随 B 的变化（理想 vs 实测）</h3>
          <Chart :option="chartVH" height="250px" />
        </div>
        <div class="card p-4">
          <h3 class="text-sm font-semibold text-ink-800 mb-2">V0 对读数的影响（整体平移）</h3>
          <Chart :option="chartV0" height="250px" />
        </div>
        <div class="card p-4">
          <h3 class="text-sm font-semibold text-ink-800 mb-2">VE 对读数的影响（随 B 增大）</h3>
          <Chart :option="chartVE" height="250px" />
        </div>
        <div class="card p-4">
          <h3 class="text-sm font-semibold text-ink-800 mb-2">修正前后结果对比</h3>
          <Chart v-if="!correction.error" :option="chartCompare" height="250px" />
          <div v-else class="h-[250px] flex items-center justify-center text-sm text-ink-400">参数无效</div>
        </div>
      </div>
    </section>

    <!-- 实验步骤和提示 -->
    <section class="card p-6 mb-6">
      <h2 class="font-bold text-ink-900 mb-3">实验步骤和提示</h2>
      <ol class="list-decimal pl-5 space-y-2 text-sm text-ink-600 leading-7">
        <li v-for="(g,i) in guide" :key="i">{{ g }}</li>
      </ol>
      <div class="rounded-2xl bg-sky-50 text-sky-700 text-sm px-4 py-3 mt-4 flex items-start gap-2">
        <Icon name="spark" :size="18" class="mt-0.5" />
        <span>注意：VE 与 VH 的换向规律相同（都正比于 I·B），四方向对称法无法消除 VE；只有已知厄廷豪森系数或温差时，才能减去 VE 得到理想 VH。</span>
      </div>
    </section>

    <!-- 实验记录提交 -->
    <section class="card p-6 mb-6">
      <h2 class="font-bold text-ink-900 mb-3">实验记录提交</h2>
      <label class="text-xs text-ink-500 block mb-3">实验结论（请用自己的话总结 V0、VE 的来源与修正方法）
        <textarea v-model="conclusion" rows="3" class="input mt-1" placeholder="例如：V0 来自电极偏移，与电流同向、与磁场无关，可由四方向对称法消除；VE 来自温差，与 VH 换向规律相同而残留，需已知 kE 才能减去。"></textarea>
      </label>
      <div class="flex flex-wrap items-center gap-3">
        <button class="btn-primary" :disabled="saveState==='loading'||!!errorMsg" @click="submit">
          <Icon v-if="saveState==='loading'" name="refresh" :size="16" class="animate-spin" />
          {{ saveState==='loading' ? '提交中…' : '提交实验记录' }}
        </button>
        <span v-if="saveState==='success'" class="status-pill on-emerald">
          <Icon name="check" :size="15" /> {{ saveMsg }}
        </span>
        <span v-if="saveState==='error'" role="alert" class="status-pill !bg-rose-50 !text-rose-600">
          <Icon name="alert" :size="15" /> {{ saveMsg }}
          <button class="underline ml-1" @click="submit">重试</button>
        </span>
      </div>

      <div class="divider-soft my-5"></div>
      <h3 class="text-sm font-semibold text-ink-800 mb-2">我的提交记录</h3>
      <div v-if="!history.length" class="text-sm text-ink-400 py-2">暂无提交记录</div>
      <div v-for="h in history.slice(0,6)" :key="h.id" class="py-2 border-b border-ink-100 text-sm">
        <button class="flex flex-wrap items-center gap-2 sm:gap-3 w-full text-left" @click="toggleHist(h.id)">
          <Icon name="chevronD" :size="15" class="text-ink-400 transition-transform shrink-0" :class="expandedHist===h.id?'rotate-180':''" />
          <span class="font-medium text-ink-800">{{ h.title }}</span>
          <span class="chip-gray">理想 VH {{ Number(h.result?.idealVH_mV).toFixed(3) }} mV</span>
          <span class="text-xs text-ink-400 ml-auto">{{ h.created_at }}</span>
        </button>
        <div v-if="expandedHist===h.id" class="mt-3 ml-5 sm:ml-7 space-y-3">
          <div>
            <div class="text-xs text-ink-400 mb-1">实验结论</div>
            <div class="rounded-xl bg-brand-50/60 p-3 text-ink-700 whitespace-pre-wrap leading-7 text-[13px]">{{ h.conclusion||'（未填写结论）' }}</div>
          </div>
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
            <div v-for="k in ['V1','V2','V3','V4']" :key="k" class="rounded-xl bg-ink-50 py-2">
              <div class="text-[10px] text-ink-400">{{ k }}</div>
              <b class="text-[12px] text-sky-700">{{ histMv(h,k) }}</b>
            </div>
          </div>
          <div class="rounded-xl bg-ink-50 p-3 text-[12px] text-ink-600 leading-6 break-words">
            参数：I {{ h.params?.I_mA }} mA · B {{ h.params?.B_T }} T · r0 {{ h.params?.r0 }} Ω · kE {{ h.params?.kE }} · RH {{ h.params?.RH }} · d {{ h.params?.d_mm }} mm
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import Chart from '../../components/Chart.vue';
import Formula from '../../components/Formula.vue';
import { api } from '../../api';
import { sideEffects, correctByReversal } from '../../../shared/calculations.cjs';
import { useAuth } from '../../stores/auth';

const auth = useAuth();
// 参数集中在 reactive 对象，便于滑块/数字/动态 v-model
const p = reactive({ I_mA: 5, B_T: 0.4, r0: 0.05, kE: 0.005, RH: -2.7e-4, d_mm: 0.5 });
const curDir = ref(1), fldDir = ref(1);

const paramRows = [
  { key: 'I_mA', label: '工作电流 I', min: 0, max: 10, step: 0.1, unit: 'mA' },
  { key: 'B_T', label: '磁感应强度 B', min: 0, max: 1, step: 0.02, unit: 'T' },
  { key: 'r0', label: '电极偏移（不等位电阻 r0）', min: 0, max: 0.5, step: 0.005, unit: 'Ω' },
  { key: 'kE', label: '厄廷豪森系数 kE', min: 0, max: 0.02, step: 0.0005, unit: 'mV/(mA·T)' }
];
// 输入范围校验（与滑块 min/max 对齐），用于空值/非数字/越界的显式提示
const inputChecks = [
  ['I_mA','工作电流 I',0,10],['B_T','磁感应强度 B',0,1],
  ['r0','不等位电阻 r0',0,0.5],['kE','厄廷豪森系数 kE',0,0.02]
];

const baseInput = () => ({ I: p.I_mA / 1000, B: p.B_T, RH: p.RH, d: p.d_mm / 1000, r0: p.r0, ettinghausenCoeff: p.kE });

const current = computed(() => {
  try { return sideEffects({ ...baseInput(), polarity: { current: curDir.value, field: fldDir.value } }); }
  catch (e) { return { error: e.message }; }
});
const inputError = computed(() => {
  for (const [key,label,lo,hi] of inputChecks) {
    const v = p[key];
    if (v===''||v===null||v===undefined||!Number.isFinite(Number(v))) return label+'：请输入有效数字';
    if (v<lo||v>hi) return label+'：数值应在 '+lo+'～'+hi+' 之间';
  }
  if (p.RH===''||p.RH===null||!Number.isFinite(Number(p.RH))) return '霍尔系数 RH：请输入有效数字';
  if (!(p.d_mm>0)) return '样品厚度 d：必须大于 0';
  return '';
});
const errorMsg = computed(() => inputError.value || current.value.error || '');
const fmtMv = v => Number.isFinite(v) ? (v * 1000).toFixed(3) : '—';

// 分项 + 比例条
const items = computed(() => {
  const c = current.value;
  if (!c || c.error) return [];
  const data = [
    { label: '理想霍尔电压 VH', v: c.VH, color: '#0d9488' },
    { label: '不等位电势 V0', v: c.V0, color: '#f59e0b' },
    { label: '厄廷豪森电压 VE', v: c.VE, color: '#e11d48' },
    { label: '实际测量 Vmeasured', v: c.Vmeasured, color: '#0284c7' }
  ];
  const max = Math.max(...data.map(d => Math.abs(d.v)), 1e-12);
  return data.map(d => ({ ...d, mv: (d.v * 1000).toFixed(3), pct: d.v === 0 ? 0 : Math.max(3, Math.round(Math.abs(d.v) / max * 100)) }));
});

// 四方向记录
const slots = [
  { key: 'V1', current: 1, field: 1 }, { key: 'V2', current: 1, field: -1 },
  { key: 'V3', current: -1, field: -1 }, { key: 'V4', current: -1, field: 1 }
];
const recorded = reactive({ V1: null, V2: null, V3: null, V4: null });
const isFinite = Number.isFinite;
const filledCount = computed(() => slots.filter(s => Number.isFinite(recorded[s.key])).length);
function recordCurrent() {
  if (current.value.error) return;
  const s = slots.find(x => x.current === curDir.value && x.field === fldDir.value);
  if (s) recorded[s.key] = current.value.Vmeasured;
}
function autoMeasure() {
  if (current.value.error) return;
  for (const s of slots) {
    const r = sideEffects({ ...baseInput(), polarity: { current: s.current, field: s.field } });
    recorded[s.key] = r.Vmeasured;
  }
}
function clearRecords() { for (const s of slots) recorded[s.key] = null; }

// 修正（物理模式，可分离 VE）
const correction = computed(() => {
  try { return correctByReversal(baseInput()); }
  catch (e) { return { error: e.message }; }
});

// 图表工具
const gridCommon = { left: 8, right: 16, top: 36, bottom: 26, containLabel: true };
const line = (name, color, data) => ({ name, type: 'line', smooth: true, showSymbol: false, color, data, lineStyle: { width: 2.5 } });
function sweepB(fn) { const a = []; for (let i = 0; i <= 20; i++) { const B = i / 20; a.push([+B.toFixed(2), fn(B)]); } return a; }
const toMv = rows => rows.map(([B, v]) => [B, +(v * 1000).toFixed(4)]);
const xB = { type: 'value', name: 'B (T)', min: 0, max: 1, nameTextStyle: { fontSize: 10 }, axisLabel: { fontSize: 10 } };
const yMv = { type: 'value', name: 'mV', nameTextStyle: { fontSize: 10 }, axisLabel: { fontSize: 10 } };

const chartVH = computed(() => {
  const I = p.I_mA / 1000;
  const ideal = sweepB(B => p.RH * I * B / (p.d_mm / 1000));
  const meas = sweepB(B => p.RH * I * B / (p.d_mm / 1000) + p.r0 * I + p.kE * I * B);
  return { grid: gridCommon, tooltip: { trigger: 'axis' }, legend: { top: 2, textStyle: { fontSize: 10 } }, xAxis: xB, yAxis: yMv,
    series: [line('理想 VH', '#0d9488', toMv(ideal)), line('实测读数', '#0284c7', toMv(meas))] };
});
const chartV0 = computed(() => {
  const I = p.I_mA / 1000;
  const no = sweepB(B => p.RH * I * B / (p.d_mm / 1000) + p.kE * I * B);
  const yes = sweepB(B => p.RH * I * B / (p.d_mm / 1000) + p.kE * I * B + p.r0 * I);
  return { grid: gridCommon, tooltip: { trigger: 'axis' }, legend: { top: 2, textStyle: { fontSize: 10 } }, xAxis: xB, yAxis: yMv,
    series: [line('无 V0', '#94a3b8', toMv(no)), line('含 V0', '#f59e0b', toMv(yes))] };
});
const chartVE = computed(() => {
  const I = p.I_mA / 1000;
  const ve = sweepB(B => p.kE * I * B);
  const no = sweepB(B => p.RH * I * B / (p.d_mm / 1000) + p.r0 * I);
  const yes = sweepB(B => p.RH * I * B / (p.d_mm / 1000) + p.r0 * I + p.kE * I * B);
  const veSeries = line('VE 分量', '#e11d48', toMv(ve)); veSeries.yAxisIndex = 1;
  const yVE = { type:'value', name:'VE mV', nameTextStyle:{fontSize:10,color:'#e11d48'}, axisLabel:{fontSize:9,color:'#e11d48'}, splitLine:{show:false} };
  return { grid: { ...gridCommon, right: 50 }, tooltip: { trigger: 'axis' }, legend: { top: 2, textStyle: { fontSize: 10 } }, xAxis: xB, yAxis: [yMv, yVE],
    series: [veSeries, line('无 VE 读数', '#94a3b8', toMv(no)), line('含 VE 读数', '#0d9488', toMv(yes))] };
});
const chartCompare = computed(() => {
  const c = correction.value;
  if (!c || c.error) return {};
  const v1 = +(c.readings[0].Vmeasured * 1000).toFixed(3);
  const corr = +(c.correctedHall * 1000).toFixed(3);
  const ideal = +(c.idealVH * 1000).toFixed(3);
  return { grid: gridCommon, tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: ['单次原始 V1', '四方向修正', '理想 VH'], axisLabel: { fontSize: 10, interval: 0 } },
    yAxis: yMv,
    series: [{ type: 'bar', barWidth: '46%', label: { show: true, position: 'top', fontSize: 10 },
      data: [{ value: v1, itemStyle: { color: '#f59e0b' } }, { value: corr, itemStyle: { color: '#e11d48' } }, { value: ideal, itemStyle: { color: '#0d9488' } }] }] };
});

// 异步提交
const saveState = ref('idle'), saveMsg = ref('');
const conclusion = ref('');
const history = ref([]);
const expandedHist = ref(null);
const toggleHist = id => expandedHist.value = expandedHist.value === id ? null : id;
// 历史读数 readings 以 V(volt) 存储，展示换算 mV
const histMv = (h,k) => Number.isFinite(h.readings?.[k]) ? (h.readings[k]*1000).toFixed(3) : '—';
async function loadHistory() { try { history.value = await api('/sim/side-effects'); } catch { history.value = []; } }
async function submit() {
  if (current.value.error || correction.value.error) return;
  saveState.value = 'loading'; saveMsg.value = '';
  try {
    const c = correction.value;
    const body = {
      title: '副效应与误差修正 · ' + new Date().toLocaleString('zh-CN'),
      conclusion: conclusion.value,
      params: { I_mA: p.I_mA, B_T: p.B_T, RH: p.RH, d_mm: p.d_mm, r0: p.r0, kE: p.kE },
      readings: { ...recorded },
      result: {
        V1_mV: +(c.readings[0].Vmeasured * 1000).toFixed(4),
        correctedHall_mV: +(c.correctedHall * 1000).toFixed(4),
        residualVE_mV: +(c.residual.value * 1000).toFixed(4),
        idealVH_mV: +(c.idealVH * 1000).toFixed(4)
      }
    };
    const r = await api('/sim/side-effects', { method: 'POST', body });
    saveState.value = 'success'; saveMsg.value = '提交成功，记录编号 #' + r.id; loadHistory();
  } catch (e) {
    saveState.value = 'error'; saveMsg.value = '提交失败：' + e.message;
  }
}

const guide = [
  '观察理想 VH：保持 I，缓慢改变 B，VH 应过原点、随 B 线性变化。',
  '引入电极偏移（r0）：读数整体平移，V0 = r0·I，与 B 无关，随电流换向变号。',
  '引入厄廷豪森效应（kE）：VE = kE·I·B，随 B 增大，使曲线斜率改变。',
  '逐方向记录 V1~V4，或使用“四方向对称测量”一键完成。',
  '执行误差修正：对称法消除 V0，但 VE 与 VH 换向规律相同而残留；已知 kE 时减去 VE 得到理想 VH。'
];

onMounted(() => { auth.fetchMe(); loadHistory(); });
</script>
