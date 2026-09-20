<template>
  <div class="space-y-5 max-w-[1500px]">
    <!-- 状态与工具栏 -->
    <div class="card p-4">
      <div class="flex flex-wrap items-center gap-3">
        <div class="flex items-center gap-3 pl-1">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-700 text-white flex items-center justify-center shadow-glow"><Icon name="flask"/></div>
          <div>
            <div class="font-bold text-ink-900">{{ simTitle }}</div>
            <div class="text-xs text-ink-400">会话 #{{ psid || '未建立' }}<template v-if="pending.caseTitle"> · 源自「{{ pending.caseTitle }}」</template></div>
          </div>
        </div>
        <div v-if="!isStandalone" class="flex flex-wrap items-center gap-2 ml-2">
          <span :class="['status-pill',state.powered?'on':'']"><Icon :name="state.powered?'check':'power'":size="13"/>{{ state.powered?'已通电':'未通电' }}</span>
          <span :class="['status-pill',state.circuitValid?'on-emerald':'']"><Icon name="check":size="13"/> 回路{{ state.circuitValid?'正常':'未就绪' }}</span>
          <span :class="['status-pill',state.stable?'on-sky':'']"><Icon name="gauge":size="13"/> {{ state.stable?'读数稳定':'波动中' }}</span>
        </div>
        <div class="ml-auto flex gap-2">
          <button class="btn-ghost" @click="restart"><Icon name="refresh":size="16"/> 重新开始</button>
          <button class="btn-soft" :disabled="!initialized" @click="finish"><Icon name="check":size="16"/> 结束实验</button>
          <button class="btn-primary" @click="fullscreen"><Icon name="cap":size="16"/> 全屏</button>
        </div>
      </div>

      <p class="text-sm mt-2" role="status">{{saveStatus}} <span class="text-rose-600">{{error}}</span></p>
      <!-- 进度（主仿真四方向测量） -->
      <div v-if="!isStandalone" class="mt-4 flex items-center gap-3 px-1">
        <Icon name="chart":size="15" class="text-brand-500"/>
        <div class="flex-1 h-2 rounded-full bg-ink-100 overflow-hidden">
          <div class="h-full rounded-full bg-gradient-to-r from-brand-400 to-brand-600 transition-all"
            :style="{width: progressPct+'%'}"></div>
        </div>
        <span class="text-xs text-ink-500 w-36">完整组 {{ groups.length }} · 本组方向 {{ doneDir }}/4</span>
      </div>

      <!-- 案例探究任务 -->
      <div v-if="pending.taskGoal" class="mt-3 rounded-2xl bg-brand-50/70 p-3.5 flex gap-3">
        <Icon name="flag" :size="17" class="text-brand-600 mt-0.5 shrink-0"/>
        <div class="text-xs leading-6">
          <b class="text-brand-800">{{ pending.taskTitle || '探究任务' }}</b>
          <span class="text-ink-600 block" v-html="taskHtml"></span></div>
      </div>
    </div>

    <div class="grid xl:grid-cols-3 gap-5">
      <!-- 仿真画面 -->
      <div :class="isStandalone?'xl:col-span-3':'xl:col-span-2'">
        <div ref="simWrap" class="rounded-[1.8rem] overflow-hidden shadow-lift border-4 border-white bg-[#e6eceb] relative" style="height:calc(100vh - 210px);min-height:560px">
          <button v-if="isFullscreen" class="absolute top-2 right-3 z-30 px-3 py-1.5 rounded-lg bg-black/60 text-white text-xs hover:bg-black/80" @click="exitFullscreen">退出全屏 (Esc)</button>
          <div class="absolute top-0 inset-x-0 h-10 glass z-20 flex items-center px-4 gap-2 rounded-b-xl">
            <span class="w-3 h-3 rounded-full bg-rose-400"></span><span class="w-3 h-3 rounded-full bg-amber-400"></span><span class="w-3 h-3 rounded-full bg-emerald-400"></span>
            <span class="ml-2 text-xs text-ink-500">hall-sim · Unity WebGL</span>
            <span v-if="loading" class="ml-auto text-xs text-brand-600 flex items-center gap-2"><span class="w-3 h-3 rounded-full border-2 border-brand-300 border-t-brand-600 animate-spin"></span>实验加载中…</span>
          </div>
          <iframe ref="frame" :src="frameSrc" class="w-full h-full" frameborder="0" allow="fullscreen"></iframe>
          <div v-if="loadFailed" class="absolute inset-0 z-40 bg-[#e6eceb] flex flex-col items-center justify-center gap-4 p-6 text-center">
            <div class="w-14 h-14 rounded-2xl bg-rose-100 text-rose-600 flex items-center justify-center"><Icon name="alert" :size="26"/></div>
            <div class="text-sm text-ink-700 max-w-sm leading-6">{{ failMsg }}</div>
            <button class="btn-primary" @click="reloadFrame"><Icon name="refresh" :size="16"/> 重新加载实验</button>
          </div>
        </div>
      </div>

      <!-- 侧栏（主仿真接线/测量辅助） -->
      <div v-if="!isStandalone" class="space-y-5">
        <div class="card p-5">
          <div class="flex items-center gap-2 mb-3">
            <button v-for="t in tabs" :key="t.k" @click="tab=t.k"
              :class="['px-3.5 py-1.5 rounded-full text-xs font-medium transition', tab===t.k?'bg-brand-600 text-white shadow-soft':'bg-ink-50 text-ink-500']">{{ t.label }}</button>
          </div>

          <!-- 数据 -->
          <div v-if="tab==='data'" class="space-y-3 max-h-[420px] overflow-y-auto">
            <div v-if="groups.length" class="text-xs text-ink-400 mb-1">已合成完整组（霍尔电压）</div>
            <div v-for="g in groups" :key="g.groupId" class="p-3 rounded-2xl bg-brand-50/70">
              <div class="flex items-center text-sm">
                <b class="text-brand-800">{{ g.groupId }}</b>
                <span class="ml-auto text-brand-700 font-bold">{{ g.VH_mV?.toFixed(2) }} mV</span>
              </div>
              <div class="text-[11px] text-ink-400 mt-1">IS {{ g.IS_mA }} mA · IM {{ g.IM_A }} A · B {{ g.B_T?.toFixed(3) }} T</div>
            </div>
            <div v-if="lastRaw" class="p-3 rounded-2xl border border-dashed border-ink-200 text-xs text-ink-500">
              最近单方向读数（槽位{{ lastRaw.slot }}）：<b class="text-ink-700">{{ lastRaw.rawVoltage_mV?.toFixed(2) }} mV</b>
            </div>
            <div v-if="!groups.length && !lastRaw" class="text-center text-sm text-ink-400 py-10">
              按提示接线、开机、测量后<br/>数据将实时显示在这里
            </div>
          </div>

          <!-- 事件 -->
          <div v-if="tab==='log'" class="space-y-2 max-h-[420px] overflow-y-auto">
            <div v-for="e in events.slice(-30).reverse()" :key="e.eventId||Math.random()" class="flex gap-2.5 text-xs">
              <div :class="['mt-1 w-2 h-2 rounded-full shrink-0', e.type==='OnAbnormalEvent'?'bg-rose-500':e.type==='OnMeasureData'?'bg-brand-500':'bg-ink-300']"></div>
              <div>
                <div class="text-ink-700 font-medium">{{ e.type.replace('On','') }} <span v-if="e.code" class="text-ink-400">· {{ e.code }}</span></div>
                <div class="text-[10px] text-ink-400">{{ e.timestamp }}</div>
              </div>
            </div>
            <div v-if="!events.length" class="text-center text-sm text-ink-400 py-10">暂无事件</div>
          </div>

          <!-- 帮助 -->
          <div v-if="tab==='help'" class="text-sm text-ink-600 space-y-2.5 leading-6">
            <div class="flex gap-2"><b class="text-brand-600">1</b> 断电连接六根外线：IS±、V±、IM±</div>
            <div class="flex gap-2"><b class="text-brand-600">2</b> 检查回路，电流设定归零后 POWER 开机</div>
            <div class="flex gap-2"><b class="text-brand-600">3</b> 设置 IS、IM，等待读数稳定后记录</div>
            <div class="flex gap-2"><b class="text-brand-600">4</b> 断电换向，完成四个方向测量</div>
            <div class="flex gap-2"><b class="text-brand-600">5</b> 完成 VH-IS、VH-IM 扫描后结束实验</div>
          </div>
        </div>

        <!-- 异常提醒 -->
        <div v-if="abnormals.length" class="card p-5 border-rose-200">
          <div class="flex items-center gap-2 text-rose-600 font-bold text-sm mb-2"><Icon name="alert":size="17"/> 异常操作记录（{{ abnormals.length }}）</div>
          <div class="space-y-1.5 max-h-40 overflow-y-auto">
            <div v-for="(a,i) in abnormals" :key="i" class="text-xs text-ink-600 flex gap-2">
              <Icon name="alert":size="13" class="text-rose-400 mt-0.5"/> {{ abnormalName(a.code) }}<span v-if="a.detail" class="text-ink-400">·{{ abnormalDetail(a.code,a.detail) }}</span>
            </div>
          </div>
          <router-link to="/workshop/cleaning" class="btn-soft w-full mt-3 text-xs">前往数据清洗台处理 →</router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, computed } from 'vue';
import { useRoute } from 'vue-router';
import katex from 'katex';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
import {createOutbox} from '../../sim-outbox';
import {abnormalName,abnormalDetail} from '../../abnormal-codes';

const route = useRoute();
const frame = ref(null);
const simWrap = ref(null);
const isFullscreen = ref(false);
const psid = ref(null), loading = ref(true);
const state = ref({});
const events = ref([]);
const measurements = ref([]);
const abnormals = ref([]);
const tab = ref('data');
const tabs = [ {k:'data',label:'测量数据'},{k:'log',label:'事件流'},{k:'help',label:'操作指引'} ];
let outbox=null, inited=false, accepting=false, timer=null, loadTimer=null;
const seenEvents = new Set();
const initialized=ref(false),saveStatus=ref('未建立会话'),error=ref('');
const loadFailed=ref(false),failMsg=ref('');
const pending = ref({ caseRef:'', caseTitle:'', taskTitle:'', taskGoal:'', cfg:null });
function loadPending(){
  try{
    const raw = sessionStorage.getItem('pending_sim_config');
    if(!raw) return;
    const p = JSON.parse(raw);
    pending.value.caseRef = p.caseRef || '';
    pending.value.caseTitle = p.caseTitle || '';
    pending.value.cfg = p.config || null;
    if(p.config){ pending.value.taskTitle=p.config.taskTitle||''; pending.value.taskGoal=p.config.taskGoal||''; }
    sessionStorage.removeItem('pending_sim_config');
  }catch{}
}

const caseId = computed(()=> route.query.case ? 'case-'+String(route.query.case) : (pending.value.caseRef || 'hall-basic'));
const CASE_FRAMES = {
  'hall-basic': ['/sim/index.html','霍尔效应基础实验'],
  'case-microscopic': ['/sim/case-microscopic/index.html','微观机理演示'],
  'case-side-effects': ['/sim/case-side-effects/index.html','副效应与误差修正仿真']
};
const STANDALONE = new Set(['case-microscopic']);
const isStandalone = computed(()=> STANDALONE.has(caseId.value));
const frameSrc = computed(()=> CASE_FRAMES[caseId.value]?.[0] || '/sim/index.html');
const simTitle = computed(()=> CASE_FRAMES[caseId.value]?.[1] || '霍尔效应仿真实验');
const groups = computed(()=> measurements.value.filter(m=>m.groupComplete));
const lastRaw = computed(()=> measurements.value[measurements.value.length-1]);
const doneDir = computed(()=>{const last=lastRaw.value;if(!last)return 0;return new Set(measurements.value.filter(m=>m.groupId===last.groupId).map(m=>m.slot)).size;});
const progressPct = computed(()=>doneDir.value/4*100);
const taskHtml = computed(()=>{
  const t = pending.value.taskGoal;
  if(!t) return '';
  const esc = t.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
  return esc.replace(/\$([^$]+?)\$/g, (m,tex)=>katex.renderToString(tex,{throwOnError:false}));
});

function send(method, payload) {
  frame.value.contentWindow.postMessage(
    { source:'hall-platform', requestId: Math.random().toString(36).slice(2), method, payload }, location.origin);
}
async function init() {
  const c = pending.value.cfg;
  accepting=true;
  send('InitExperiment', { schemaVersion:1, caseId:caseId.value,
    material: c?.material || 'n-silicon',
    thickness_mm: c?.thickness_mm ?? 0.5,
    maxIs_mA: c?.maxIs_mA ?? 10,
    maxIm_A: c?.maxIm_A ?? 1 });
}
async function onMessage(e) {
  if (e.origin !== location.origin||e.source!==frame.value?.contentWindow||!e.data) return;
  const d = e.data;
  if(d.source==='hall-host'&&d.error){
    const m=String(d.error);
    error.value=m.includes('not ready')?'实验尚未就绪，请稍候再试。'
      :m.includes('Unknown method')?'实验指令不被支持。':m;return;}
  if (d.source === 'hall-host' && d.ready) {
    if (!inited) {
      inited = true;
      try{const me=await api('/me');outbox=createOutbox(me.id,s=>saveStatus.value=s);await outbox.flush();await newSession();clearInterval(timer);timer=setInterval(()=>{outbox.flush().catch(()=>{});if(accepting)send('RequestSnapshot');},3000);}catch(e){error.value=e.message;inited=false;}
    }
  }
  if (d.source === 'hall-unity') {
    const ev = d.event;
    if(!ev||!accepting||ev.isDemo||ev.type==='OnReady')return;
    if(ev.type==='OnSnapshot'){if(ev.state)state.value=ev.state;return;}
    if(!ev.eventId||seenEvents.has(ev.eventId))return;
    seenEvents.add(ev.eventId);
    events.value.push(ev);
    if(ev.code==='InitExperiment'&&ev.success){initialized.value=true;loading.value=false;loadFailed.value=false;clearTimeout(loadTimer);}
    if(ev.code==='INVALID_INIT'){error.value=ev.detail;accepting=false;initialized.value=false;return;}
    try{outbox.enqueue(psid.value,ev);}catch(e){error.value='本地保存失败，请暂停实验：'+e.message;accepting=false;return;}
    if (ev.state) state.value = ev.state;
    if (ev.type === 'OnMeasureData' && ev.measurement) {
      const m = ev.measurement;
      // 一组四方向齐时，前端按对称法合成霍尔电压，与后端/报告口径一致
      if (m.groupComplete) {
        const raw = new Map(measurements.value
          .filter(x => x.groupId === m.groupId)
          .map(x => [x.slot, x.rawVoltage_mV]));
        raw.set(m.slot, m.rawVoltage_mV);
        if (raw.size === 4) {
          const vh = (raw.get(1)-raw.get(2)+raw.get(3)-raw.get(4))/4;
          m.VH_mV = vh;
          m.normalizedVH_mV = vh * (m.voltageDirection||1);
        }
      }
      measurements.value.push(m);
    }
    if (ev.type === 'OnAbnormalEvent') abnormals.value.push(ev);
    if (ev.type === 'OnExperimentComplete') {initialized.value=false;await outbox.flush().catch(()=>{});}
  }
}
async function newSession(){
  accepting=false;initialized.value=false;loading.value=true;loadFailed.value=false;armLoadTimer();
  const r = await api('/sim/start', { method:'POST', body:{ case_id: caseId.value }});
  psid.value = r.platformSessionId;
  pending.value.cfg=r.config;
  seenEvents.clear();
  state.value={}; events.value=[]; measurements.value=[]; abnormals.value=[];
  init();
}
async function restart(){if(!outbox)return;if(!confirm('结束当前操作并新建实验？已保存数据会保留。'))return;try{await outbox.flush();if(psid.value&&!state.value.finished)await api('/sim/sessions/'+psid.value+'/abandon',{method:'POST'});await newSession();}catch(e){error.value=e.message;}}
function finish() { if(initialized.value)send('FinishFromWeb'); }
function fullscreen() { simWrap.value?.requestFullscreen?.(); }
function exitFullscreen() { if (document.fullscreenElement) document.exitFullscreen?.(); }
function onFsChange() { isFullscreen.value = !!document.fullscreenElement; }

function hasWebGL(){try{const c=document.createElement('canvas');return !!(window.WebGLRenderingContext&&(c.getContext('webgl2')||c.getContext('webgl')));}catch{return false;}}
function armLoadTimer(){clearTimeout(loadTimer);loadTimer=setTimeout(()=>{if(loading.value){loading.value=false;failMsg.value='实验加载超时：70 秒内未能完成初始化。请检查网络连接后重新加载，或使用支持 WebGL 的新版 Chrome/Edge。';loadFailed.value=true;}},70000);}
function reloadFrame(){
  loadFailed.value=false;failMsg.value='';loading.value=true;accepting=false;inited=false;
  try{if(frame.value)frame.value.src=frameSrc.value+(frameSrc.value.includes('?')?'&':'?')+'t='+Date.now();}catch{}
  armLoadTimer();
}
onMounted(()=>{ loadPending(); if(!hasWebGL()&&frameSrc.value==='/sim/index.html'){loading.value=false;failMsg.value='当前浏览器不支持 WebGL，无法加载三维实验；请使用新版 Chrome/Edge 并开启硬件加速。';loadFailed.value=true;} window.addEventListener('message', onMessage); document.addEventListener('fullscreenchange', onFsChange); });
onBeforeUnmount(()=>{clearInterval(timer);clearTimeout(loadTimer);outbox?.flush().catch(()=>{});window.removeEventListener('message',onMessage);document.removeEventListener('fullscreenchange', onFsChange);});
</script>
