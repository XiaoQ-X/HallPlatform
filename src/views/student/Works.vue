<template>
  <div class="max-w-7xl space-y-5">
    <!-- Tab -->
    <div class="card p-3 flex flex-wrap gap-2">
      <button v-for="t in tabs" :key="t.k" @click="tab=t.k"
        :class="['px-5 py-2.5 rounded-2xl text-sm font-medium transition flex items-center gap-2',
          tab===t.k?'bg-gradient-to-r from-brand-500 to-brand-700 text-white shadow-soft':'text-ink-500 hover:bg-ink-50']">
        <Icon :name="t.icon":size="16"/> {{ t.label }}</button>
      <button class="ml-auto btn-primary" @click="showPublish=true"><Icon name="plus":size="16"/> 发布我的作品</button>
    </div>

    <!-- 待评作品 -->
    <div v-if="tab==='todo'">
      <div class="grid md:grid-cols-2 xl:grid-cols-3 gap-5">
        <div v-for="x in items" :key="x.id" class="card-hover p-6">
          <div class="flex items-center gap-3">
            <div class="w-11 h-11 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center"><Icon :name="x.item_type==='project'?'folder':'flask'"/></div>
            <div class="text-xs text-ink-400">{{ x.student_name }}</div>
            <span v-if="x.reviewed" class="ml-auto chip !bg-brand-100 text-brand-800 text-[10px]">已评</span>
          </div>
          <div class="font-bold text-ink-900 mt-3 leading-6">{{ x.title }}</div>
          <div class="text-sm text-ink-400 mt-1 line-clamp-2 leading-6">{{ x.summary }}</div>
          <button class="btn-primary w-full mt-4" @click="openItem(x)">{{ x.reviewed?'查看/修改评价':'开始评价' }}</button>
        </div>
      </div>
      <div v-if="!items.length" class="card p-12 text-center text-sm text-ink-400">暂无可评价的同班作品</div>
    </div>

    <!-- 我的作品 -->
    <div v-if="tab==='mine'" class="space-y-5">
      <div v-for="x in myItems" :key="x.id" class="card p-6">
        <div class="flex items-start gap-4">
          <div class="w-12 h-12 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center"><Icon name="award"/></div>
          <div class="flex-1">
            <div class="font-bold text-ink-900">{{ x.title }}</div>
            <div class="text-sm text-ink-400 mt-1">{{ x.summary }}</div>
            <ProtectedFile v-if="x.recording" :src="x.recording" video/>
          </div>
          <span class="chip">{{ x.reviews.length }} 条评价</span>
        </div>
        <div v-if="x.reviews.length" class="mt-4 space-y-3">
          <div v-for="rv in x.reviews" :key="rv.id" class="p-4 rounded-2xl bg-ink-50/70">
            <div class="flex items-center text-sm"><b>{{ rv.reviewer_name }}</b>
              <span class="ml-auto font-bold text-brand-700">{{rv.voided?'已撤销（不计分）':totalOf(rv)+' 分'}}</span></div>
            <p class="text-sm text-ink-600 mt-1.5">{{ rv.comment }}</p>
            <button class="text-xs text-rose-500 mt-2" @click="openAppeal(x,rv)">对评价有异议？发起申诉</button>
          </div>
        </div>
      </div>
      <div v-if="!myItems.length" class="card p-12 text-center text-sm text-ink-400">你还没有发布作品</div>
    </div>

    <!-- 我发出的 -->
    <div v-if="tab==='gave'" class="card p-6">
      <div v-for="rv in gave" :key="rv.id" class="flex items-center gap-3 py-3 border-b border-ink-50 last:border-0">
        <Icon name="check" class="text-brand-600"/><span class="text-sm">{{ rv.item_title }}</span>
        <span class="text-xs text-ink-400">作者：{{ rv.owner_name }}</span>
        <span class="ml-auto text-sm text-ink-400">{{ rv.created_at }}</span>
      </div>
      <div v-if="!gave.length" class="text-center text-sm text-ink-400 py-8">还没有发出评价</div>
    </div>

    <!-- 评价模态 -->
    <div v-if="current" class="modal-mask" @click.self="current=null">
      <div class="modal p-6 w-full max-w-2xl">
        <h3 class="font-bold text-lg text-ink-900">{{ current.title }}</h3>
        <div class="text-xs text-ink-400 mt-1">{{ current.student_name }} · {{ current.created_at }}</div>
        <ProtectedFile v-if="current.recording" :src="current.recording" video/>
        <ProtectedFile v-for="f in current.files||[]" :src="f.url" :name="f.name"/>
        <p class="text-sm text-ink-600 mt-3 leading-7 bg-ink-50 rounded-2xl p-4">{{ current.summary }}</p>
        <details v-if="current.evidence?.content" open class="mt-3"><summary>课题报告证据 · v{{current.evidence.version}}</summary><p class="whitespace-pre-wrap break-words py-3">{{current.evidence.content}}</p><ProtectedFile v-for="f in current.evidence.files||[]" :src="f.url" :name="f.name"/></details>
        <details v-if="current.evidence?.measurements" class="mt-3"><summary>实验原始测量证据</summary><pre class="text-xs whitespace-pre-wrap break-words">{{JSON.stringify(current.evidence.measurements,null,2)}}</pre></details>

        <div class="mt-4 flex items-center gap-2">
          <span class="text-sm text-ink-600">评分量表：</span>
          <select v-model="rubricId" class="input flex-1" :disabled="!!current.rubric_id" @change="resetScores">
            <option v-for="r in rubrics.filter(r=>r.target_type===current.item_type)" :key="r.id" :value="r.id">{{ r.title }}</option></select>
        </div>

        <div v-if="rubric" class="mt-4 space-y-4">
          <div v-for="d in rubric.dimensions" :key="d.name">
            <div class="flex items-center text-sm">
              <span class="text-ink-700">{{ d.name }}</span>
              <span class="ml-auto font-bold text-brand-700">{{ scores[d.name]||0 }}/{{ d.max }}</span>
            </div>
            <input type="range" :min="0" :max="d.max" v-model.number="scores[d.name]" class="w-full accent-brand-600 mt-1.5"/>
            <div class="text-[11px] text-ink-400">{{ d.desc }}</div>
          </div>
        </div>
        <label class="text-sm text-ink-600 mt-4 block">文字反馈（请给出具体、建设性的意见）</label>
        <textarea v-model="comment" class="input min-h-24 mt-1.5" placeholder="优点… 建议…"></textarea>
        <div class="flex gap-3 mt-5"><button class="btn-ghost flex-1" @click="current=null">取消</button>
          <button class="btn-primary flex-1" @click="submitReview">提交评价</button></div>
      </div>
    </div>

    <!-- 发布模态 -->
    <div v-if="showPublish" class="modal-mask" @click.self="showPublish=false">
      <div class="modal p-6">
        <h3 class="font-bold text-lg text-ink-900 mb-4">发布作品</h3>
        <label class="text-xs text-ink-400">作品类型</label>
        <select v-model="pub.item_type" class="input mb-3"><option value="experiment">实验操作</option><option value="project">课题成果</option></select>
        <label class="block">评价量表<select v-model="pub.rubric_id" class="input"><option v-for="r in rubrics.filter(r=>r.target_type===pub.item_type)" :value="r.id">{{r.title}}</option></select></label>
        <label v-if="pub.item_type==='experiment'" class="block">实验会话<select v-model="pub.session_id" class="input"><option v-for="s in sessions.filter(s=>s.finished&&!s.demo)" :value="s.id">#{{s.id}} · {{s.started_at}}</option></select></label>
        <label v-else class="block">课题成果<select v-model="pub.submission_id" class="input"><option v-for="s in submissions" :value="s.id">{{s.project_title}} · v{{s.version}}</option></select></label>
        <label class="text-xs text-ink-400">标题</label><input v-model="pub.title" class="input mb-3" placeholder="如：霍尔效应四方向测量实验"/>
        <label class="text-xs text-ink-400">摘要</label><textarea v-model="pub.summary" class="input min-h-20 mb-3" placeholder="简要描述你的实验过程与结果…"></textarea>
        <label class="btn-ghost cursor-pointer w-full justify-center"><Icon name="video":size="16"/> 上传操作录屏
          <input type="file" accept="video/*" class="hidden" @change="onVideo"/></label>
        <span v-if="pub.recording" class="text-xs text-brand-600 mt-2 block">录屏已上传：{{ pub.recording }}</span>
        <div class="flex gap-3 mt-5"><button class="btn-ghost flex-1" @click="showPublish=false">取消</button>
          <button class="btn-primary flex-1" :disabled="publishing||uploading" @click="doPublish">发布</button></div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted,watch } from 'vue';
import Icon from '../../components/Icon.vue';
import { api, upload } from '../../api';
import ProtectedFile from '../../components/ProtectedFile.vue';
const tab = ref('todo');
const tabs = [ {k:'todo',label:'待评作品',icon:'users'},{k:'mine',label:'我的作品',icon:'award'},{k:'gave',label:'我发出的评价',icon:'check'} ];
const items = ref([]), myItems = ref([]), gave = ref([]), rubrics = ref([]);
const current = ref(null), rubricId = ref(null), scores = ref({}), comment = ref('');
const showPublish = ref(false);
const pub = ref({ item_type:'experiment', title:'', summary:'', recording:'' });
const sessions=ref([]),submissions=ref([]);
const publishing=ref(false),uploading=ref(false);
watch(()=>pub.value.item_type,()=>{pub.value.rubric_id=rubrics.value.find(r=>r.target_type===pub.value.item_type)?.id;pub.value.session_id=null;pub.value.submission_id=null;});
function resetScores(){scores.value=Object.fromEntries((rubric.value?.dimensions||[]).map(d=>[d.name,0]));}

const rubric = computed(()=> rubrics.value.find(r=>r.id===rubricId.value));
async function openItem(x){ current.value = await api('/peer/items/'+x.id);
  rubricId.value=current.value.rubric_id||rubrics.value.find(r=>r.target_type===current.value.item_type)?.id;resetScores();comment.value='';
  const myrv = current.value.reviews.find(r=>r.mine);
  if(myrv){ scores.value=myrv.scores; comment.value=myrv.comment; rubricId.value=myrv.rubric_id||rubricId.value; }
}
function totalOf(rv){ const r=rubrics.value.find(x=>x.id===rv.rubric_id);
  if(!r)return ''; return r.dimensions.reduce((a,d)=>a+(rv.scores[d.name]||0),0); }
async function submitReview(){
  await api('/peer/items/'+current.value.id+'/review',{method:'POST',
    body:{rubric_id:rubricId.value,scores:scores.value,comment:comment.value}});
  current.value=null; load();
}
async function onVideo(e){if(!e.target.files[0]||uploading.value)return;uploading.value=true;const target=pub.value;try{const r=await upload(e.target.files[0]);if(pub.value===target)pub.value.recording=r.url;}finally{uploading.value=false;}}
async function doPublish(){
  if(publishing.value||uploading.value)return;
  if(!pub.value.title)return alert('请填写标题');
  publishing.value=true;try{const result=await api('/peer/items',{method:'POST',body:pub.value});
  if(result.reused)alert('该版本已发布，已保留原作品及其评价。');
  showPublish.value=false;pub.value={item_type:'experiment',title:'',summary:'',recording:'',rubric_id:rubrics.value.find(r=>r.target_type==='experiment')?.id};load();
  }finally{publishing.value=false;}
}
function openAppeal(x,rv){ location.hash=''; window.sessionStorage.setItem('appeal_ref',JSON.stringify({ref_type:'review',ref_id:rv.id})); location.href='/peer/appeals'; }
async function load(){
  items.value=await api('/peer/items');
  myItems.value=await api('/peer/my-items');
  gave.value=await api('/peer/my-reviews');
  rubrics.value=await api('/peer/rubrics');
  sessions.value=await api('/sim/sessions');submissions.value=await api('/resources/my-submissions');
  if(!pub.value.rubric_id)pub.value.rubric_id=rubrics.value.find(r=>r.target_type===pub.value.item_type)?.id;
}
onMounted(load);
</script>
