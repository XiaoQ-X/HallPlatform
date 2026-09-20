<template>
  <div class="max-w-7xl space-y-5">
    <div class="grid lg:grid-cols-3 gap-5">
      <!-- 会话选择 -->
      <div class="card p-5">
        <h3 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="filter" class="text-brand-600"/> 选择实验会话</h3>
        <div class="space-y-2 max-h-[480px] overflow-y-auto">
          <button v-for="s in briefs" :key="s.id" @click="pick(s)"
            :class="['w-full text-left p-4 rounded-2xl border transition', sel?.id===s.id?'border-brand-400 bg-brand-50/60':'border-ink-100 hover:bg-ink-50']">
            <div class="flex items-center text-sm font-medium text-ink-800">会话 #{{ s.id }}
              <span :class="['ml-auto chip text-[10px]',s.finished?'!bg-brand-100 text-brand-800':'!bg-amber-100 text-amber-800']">{{ s.finished?'已完成':'进行中' }}</span>
            </div>
            <div class="text-xs text-ink-400 mt-1.5">{{ s.started_at }}</div>
            <div class="flex gap-3 text-xs mt-2">
              <span>完整组 <b>{{ s.groups }}</b></span>
              <span class="text-rose-500">异常 <b>{{ s.errors }}</b></span>
            </div>
          </button>
          <div v-if="!briefs.length" class="text-center text-sm text-ink-400 py-10">暂无实验会话</div>
        </div>
      </div>

      <!-- 异常点与登记 -->
      <div class="lg:col-span-2 space-y-5">
        <div class="card p-6" v-if="sel">
          <h3 class="font-bold text-ink-900 mb-4">异常检测点（{{ points.length }}）</h3>
          <div v-if="points.length" class="space-y-3">
            <div v-for="(a,i) in points" :key="i" class="p-4 rounded-2xl bg-rose-50/60 flex items-start gap-3">
              <div class="w-9 h-9 rounded-xl bg-rose-100 text-rose-600 flex items-center justify-center shrink-0"><Icon name="alert":size="16"/></div>
              <div class="flex-1 text-sm">
                <b class="text-ink-800">{{ a.code }}</b>
                <div class="text-xs text-ink-400 mt-0.5">{{ a.occurred_at }} · {{ a.detail||'系统检测到异常操作' }}</div>
              </div>
              <button class="btn-soft text-xs" @click="openForm(a)">登记成因</button>
            </div>
          </div>
          <div v-else class="text-center text-sm text-ink-400 py-8">未记录操作异常；数据质量仍需检查</div>

          <div class="divider-soft my-5"></div>
          <h3 class="font-bold text-ink-900 mb-3">完整组数据巡检</h3>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead><tr class="text-ink-400 text-xs">
                <th class="text-left font-medium py-2 px-2">组</th><th class="font-medium">IS/mA</th><th class="font-medium">IM/A</th>
                <th class="font-medium">B/T</th><th class="font-medium">VH/mV</th><th></th></tr></thead>
              <tbody>
                <tr v-for="g in groupList" :key="g.group_id" class="border-t border-ink-50">
                  <td class="py-2.5 px-2 font-medium">{{ g.group_id }}</td>
                  <td class="text-center">{{ g.IS_mA }}</td><td class="text-center">{{ g.IM_A }}</td>
                  <td class="text-center">{{ Number(g.B_T).toFixed(3) }}</td>
                  <td class="text-center text-brand-700 font-medium">{{ Number(g.VH_mV).toFixed(2) }}</td>
                  <td><button class="text-xs text-brand-600" @click="openManual(g)">标记可疑</button></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <div class="card p-10 text-center text-ink-400 text-sm" v-else>
          <Icon name="broom":size="34" class="mx-auto text-brand-300 mb-3"/> 请先在左侧选择一个实验会话
        </div>
      </div>
    </div>

    <!-- 清洗记录台账 -->
    <div class="card p-6">
      <h3 class="font-bold text-ink-900 mb-4">清洗记录台账（{{ records.length }}）</h3>
      <div class="space-y-2.5">
        <div v-for="r in records" :key="r.id" class="flex flex-wrap items-center gap-3 p-4 rounded-2xl" :class="r.resolved?'bg-brand-50/50':'bg-amber-50/60'">
          <Icon :name="r.resolved?'check':'clock'" :size="16" :class="r.resolved?'text-brand-600':'text-amber-600'"/>
          <span class="text-sm font-medium text-ink-800">{{ r.abnormal_code }}</span>
          <span class="text-xs text-ink-400">{{ r.point_label }}</span>
          <span class="text-sm text-ink-600">成因：{{ r.cause }}</span>
          <span class="text-sm text-ink-500">处理：{{ r.action }}</span>
          <span :class="['ml-auto chip text-[11px]',r.resolved?'!bg-brand-100 text-brand-800':'!bg-amber-100 text-amber-800']">{{ r.resolved?'已解决':'待跟进' }}</span>
          <button class="text-ink-400 hover:text-rose-500" @click="del(r)"><Icon name="trash":size="15"/></button>
          <button class="text-brand-700" @click="editRecord(r)">编辑</button>
        </div>
        <div v-if="!records.length" class="text-center text-sm text-ink-400 py-6">还没有清洗记录</div>
      </div>
    </div>

    <!-- 登记表单模态 -->
    <div v-if="form" class="modal-mask" @click.self="form=null">
      <div class="modal p-6">
        <h3 class="font-bold text-ink-900 mb-4">异常成因登记</h3>
        <label class="text-xs text-ink-400">异常代码</label>
        <input v-model="form.abnormal_code" class="input mb-3"/>
        <label class="text-xs text-ink-400">异常成因</label>
        <select v-model="form.cause" class="input mb-3">
          <option v-for="c in causes" :key="c">{{ c }}</option>
        </select>
        <label class="text-xs text-ink-400">处理措施</label>
        <textarea v-model="form.action" class="input min-h-24 mb-3" placeholder="如：重新接线、重新稳定后采样…"></textarea>
        <label class="flex items-center gap-2 text-sm mb-4"><input type="checkbox" v-model="form.resolved" class="w-4 h-4 accent-brand-600"/> 已解决</label>
        <label v-if="form.measurement_id" class="block mb-4">数据决策<select v-model="form.decision" class="input"><option value="keep">保留</option><option value="exclude">从有效数据集排除</option></select></label>
        <div class="flex gap-3"><button class="btn-ghost flex-1" @click="form=null">取消</button>
          <button class="btn-primary flex-1" @click="save">保存记录</button></div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const briefs = ref([]), sel = ref(null), points = ref([]), groupList = ref([]);
const records = ref([]), form = ref(null);
const causes = ['接线错误（线序/松动）','带电操作违反规程','电流设置不当','读数未稳定即记录','组内条件不一致','环境/设备干扰','系统误报','其他（在措施中说明）'];
async function pick(s){ sel.value=s; const d=await api('/workshop/session-points/'+s.id);
  points.value=d.abnormals; groupList.value=d.groups; }
function openForm(a){form.value={session_id:sel.value.id,abnormal_code:a.code,point_label:a.detail||a.code,cause:causes[0],action:'',resolved:false,label:'会话#'+sel.value.id,decision:'keep'};}
function openManual(g){form.value={session_id:sel.value.id,measurement_id:g.id,decision:g.decision||'keep',abnormal_code:'MANUAL_SUSPECT',point_label:g.group_id,cause:causes[7],action:'',resolved:false,label:'会话#'+sel.value.id};}
function editRecord(r){form.value={...r,resolved:!!r.resolved};}
async function save(){
  await api('/workshop/cleaning'+(form.value.id?'/'+form.value.id:''),{method:form.value.id?'PUT':'POST',body:form.value});
  form.value=null; await load();if(sel.value)await pick(sel.value);
}
async function del(r){ await api('/workshop/cleaning/'+r.id,{method:'DELETE'}); await load();if(sel.value)await pick(sel.value); }
async function load(){ briefs.value=await api('/workshop/sessions-brief');
  records.value=await api('/workshop/cleaning'); }
onMounted(load);
</script>
