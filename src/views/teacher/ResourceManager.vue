<template>
  <div class="space-y-5 max-w-7xl">
    <div class="card p-3 flex flex-wrap gap-2">
      <button v-for="t in tabs" :key="t.k" @click="switchTab(t.k)"
        :class="['px-4 py-2.5 rounded-2xl text-sm font-medium flex items-center gap-2',
          tab===t.k?'bg-gradient-to-r from-brand-500 to-brand-700 text-white shadow-soft':'text-ink-500 hover:bg-ink-50']">
        <Icon :name="t.icon":size="16"/>{{ t.label }}</button>
      <button class="ml-auto btn-primary" @click="newItem"><Icon name="plus":size="16"/> 新建</button>
    </div>

    <!-- 案例 / 思政 / 课题 列表 -->
    <div v-if="['cases','ideology','projects'].includes(tab)" class="card p-6">
      <div class="space-y-2.5">
        <div v-for="x in lists[tab]" :key="x.id" class="flex items-center gap-3 p-4 rounded-2xl hover:bg-ink-50">
          <div class="w-10 h-10 rounded-2xl flex items-center justify-center text-white text-lg" :style="{background:x.color}">{{ x.cover }}</div>
          <div class="text-sm"><b class="text-ink-800">{{ x.title }}</b>
            <div class="text-xs text-ink-400">{{ x.subtitle }}</div></div>
          <span :class="['ml-auto chip text-[10px]',x.published?'!bg-brand-100 text-brand-800':'!bg-ink-100 text-ink-500']">{{ x.published?'已发布':'未发布' }}</span>
          <button class="text-ink-400 hover:text-brand-600" @click="edit(x)"><Icon name="edit":size="16"/></button>
          <button class="text-ink-400 hover:text-rose-500" @click="remove(x)"><Icon name="trash":size="16"/></button>
        </div>
      </div>
    </div>

    <!-- 题目列表 -->
    <div v-if="tab==='questions'" class="card p-6">
      <div class="space-y-2.5">
        <div v-for="x in lists.questions" :key="x.id" class="flex items-center gap-3 p-4 rounded-2xl hover:bg-ink-50">
          <span class="chip-gray text-[10px] w-16 justify-center">{{ typeName[x.type] }}</span>
          <span class="text-sm text-ink-800 line-clamp-1">{{ x.stem }}</span>
          <span class="ml-auto text-xs text-brand-700 font-medium">{{ x.score }}分</span>
          <button class="text-ink-400 hover:text-brand-600" @click="edit(x)"><Icon name="edit":size="16"/></button>
          <button class="text-ink-400 hover:text-rose-500" @click="remove(x)"><Icon name="trash":size="16"/></button>
        </div>
      </div>
    </div>

    <!-- 试卷列表 -->
    <div v-if="tab==='quizzes'" class="card p-6 space-y-3">
      <div v-for="x in lists.quizzes" :key="x.id" class="flex items-center gap-3 p-4 rounded-2xl bg-ink-50">
        <Icon name="edit" class="text-brand-600"/><b class="text-sm">{{ x.title }}</b>
        <span class="text-xs text-ink-400">{{ x.question_ids.length }}题 · {{ x.time_minutes }}分钟</span>
        <button class="ml-auto text-ink-400 hover:text-rose-500" @click="remove(x)"><Icon name="trash":size="16"/></button>
      </div>
      <button class="btn-soft w-full" @click="editQuiz"><Icon name="plus":size="15"/> 新建试卷（选题组卷）</button>
    </div>

    <!-- 编辑模态：案例/思政/课题 -->
    <div v-if="form && ['cases','ideology','projects'].includes(tab)" class="modal-mask" @click.self="form=null">
      <div class="modal p-6 max-w-2xl">
        <h3 class="font-bold text-lg mb-4">{{ form.id?'编辑':'新建' }}{{ tabLabel() }}</h3>
        <div class="grid grid-cols-2 gap-3">
          <div><label class="text-xs text-ink-400">标题</label><input v-model="form.title" class="input"/></div>
          <div><label class="text-xs text-ink-400">副标题</label><input v-model="form.subtitle" class="input"/></div>
          <div><label class="text-xs text-ink-400">图标(emoji)</label><input v-model="form.cover" class="input"/></div>
          <div><label class="text-xs text-ink-400">主题色</label><input type="color" v-model="form.color" class="input h-11"/></div>
          <div><label class="text-xs text-ink-400">分类</label><input v-model="form.category" class="input"/></div>
          <div v-if="tab==='cases'"><label class="text-xs text-ink-400">难度</label>
            <select v-model="form.difficulty" class="input"><option>入门</option><option>进阶</option><option>挑战</option><option>拓展</option></select></div>
          <div v-if="tab==='ideology'"><label class="text-xs text-ink-400">来源</label><input v-model="form.source" class="input"/></div>
        </div>
        <label class="text-xs text-ink-400 mt-3 block">{{ tab==='projects'?'探究指南（支持简单HTML）':'正文内容（支持简单HTML标签：h3/p/ul/li/b）' }}</label>
        <textarea v-if="tab==='projects'" v-model="form.guide" class="input min-h-40 font-mono text-xs mt-1"></textarea>
        <textarea v-else v-model="form.content" class="input min-h-40 font-mono text-xs mt-1"></textarea>
        <div v-if="tab==='projects'">
          <label class="text-xs text-ink-400 mt-2 block">任务清单（每行一条，* 表示选做）</label>
          <textarea v-model="tasksText" class="input min-h-20 mt-1"></textarea>
          <label class="text-xs text-ink-400 mt-2 block">资料附件（每行一个名称）</label>
          <textarea v-model="attText" class="input min-h-16 mt-1"></textarea>
        </div>
        <label class="flex items-center gap-2 text-sm mt-3"><input type="checkbox" v-model="form.published" class="w-4 h-4 accent-brand-600"/> 立即发布</label>
        <div class="flex gap-3 mt-5"><button class="btn-ghost flex-1" @click="form=null">取消</button>
          <button class="btn-primary flex-1" @click="save">保存</button></div>
      </div>
    </div>

    <!-- 题目编辑模态 -->
    <div v-if="qform && tab==='questions'" class="modal-mask" @click.self="qform=null">
      <div class="modal p-6 max-w-2xl">
        <h3 class="font-bold text-lg mb-4">题目编辑</h3>
        <label class="text-xs text-ink-400">题型</label>
        <select v-model="qform.type" class="input mb-3">
          <option value="single">单选题</option><option value="multiple">多选题</option>
          <option value="judge">判断题</option><option value="fill">填空题</option><option value="essay">简答题</option></select>
        <label class="text-xs text-ink-400">题干</label><textarea v-model="qform.stem" class="input min-h-20 mb-3"></textarea>
        <template v-if="['single','multiple'].includes(qform.type)">
          <label class="text-xs text-ink-400">选项</label>
          <div v-for="(o,i) in qform.options" :key="i" class="flex gap-2 mb-2">
            <span class="leading-10 text-xs w-6 text-center">{{ String.fromCharCode(65+i) }}</span>
            <input v-model="qform.options[i]" class="input"/>
            <button class="text-ink-400" @click="qform.options.splice(i,1)"><Icon name="x"/></button></div>
          <button class="btn-soft text-xs mb-3" @click="qform.options.push('')"><Icon name="plus":size="13"/> 添加选项</button>
        </template>
        <label class="text-xs text-ink-400">正确答案（判断填 对/错；多选连写如 ABC；填空多个答案用 | 分隔）</label>
        <input v-model="qform.answer" class="input mb-3"/>
        <label class="text-xs text-ink-400">解析</label><textarea v-model="qform.analysis" class="input min-h-16 mb-3"></textarea>
        <label class="text-xs text-ink-400">分值</label><input type="number" v-model.number="qform.score" class="input mb-3"/>
        <div class="flex gap-3"><button class="btn-ghost flex-1" @click="qform=null">取消</button>
          <button class="btn-primary flex-1" @click="saveQuestion">保存</button></div>
      </div>
    </div>

    <!-- 组卷模态 -->
    <div v-if="quizForm" class="modal-mask" @click.self="quizForm=null">
      <div class="modal p-6 max-w-2xl">
        <h3 class="font-bold text-lg mb-4">新建试卷</h3>
        <input v-model="quizForm.title" class="input mb-3" placeholder="试卷标题"/>
        <textarea v-model="quizForm.description" class="input mb-3" placeholder="说明"></textarea>
        <input type="number" v-model.number="quizForm.time_minutes" class="input mb-3" placeholder="时长(分钟)"/>
        <label class="text-xs text-ink-400">勾选题目</label>
        <div class="max-h-56 overflow-y-auto mt-1 space-y-1">
          <label v-for="q in lists.questions" :key="q.id" class="flex gap-2 p-2.5 rounded-xl hover:bg-ink-50 text-sm">
            <input type="checkbox" class="accent-brand-600" :value="q.id" v-model="quizForm.sel"/>{{ q.stem }}</label>
        </div>
        <div class="flex gap-3 mt-4"><button class="btn-ghost flex-1" @click="quizForm=null">取消</button>
          <button class="btn-primary flex-1" @click="saveQuiz">组卷</button></div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const tabs=[ {k:'cases',label:'应用案例',icon:'book'},{k:'ideology',label:'思政素材',icon:'star'},
  {k:'questions',label:'题目',icon:'help'},{k:'quizzes',label:'试卷',icon:'edit'},{k:'projects',label:'课题包',icon:'folder'}];
const tab=ref('cases');
const lists=ref({cases:[],ideology:[],questions:[],quizzes:[],projects:[]});
const form=ref(null),qform=ref(null),quizForm=ref(null),tasksText=ref(''),attText=ref('');
const typeName={single:'单选',multiple:'多选',judge:'判断',fill:'填空',essay:'简答'};
const tabLabel=()=>tabs.find(t=>t.k===tab.value)?.label;
async function load(){ for(const k of ['cases','ideology','questions','quizzes','projects']){
  lists.value[k]=await api('/resources/'+(k==='cases'?'cases':k==='ideology'?'ideology':k==='questions'?'questions':k==='quizzes'?'quizzes':'projects')); }}
function switchTab(k){ tab.value=k; }
function defaults(){
  if(tab.value==='cases')return {cover:'📘',color:'#0EA5E9',difficulty:'入门',tags:[],published:true};
  if(tab.value==='ideology')return {cover:'🌟',color:'#0284C7',source:'课程组',published:true};
  return {cover:'📁',color:'#10B981',published:true};
}
function newItem(){
  if(tab.value==='questions'){ qform.value={type:'single',options:['',''],score:2}; return; }
  if(tab.value==='quizzes'){ editQuiz(); return; }
  form.value=defaults(); tasksText.value=''; attText.value='';
}
function edit(x){
  if(tab.value==='questions'){ qform.value={...x,options:x.options||[]}; return; }
  form.value={...x,published:!!x.published};
  if(tab.value==='projects') tasksText.value=x.tasks.map(t=>(!t.r?'*':'')+t.t).join('\n'),
    attText.value=(x.attachments||[]).map(a=>a.n).join('\n');
}
async function save(){
  if(tab.value==='projects'){ form.value.tasks=tasksText.split('\n').filter(Boolean)
    .map(l=>({t:l.replace(/^\*/,''),r:!l.startsWith('*')}));
    form.value.attachments=attText.split('\n').filter(Boolean).map(l=>({n:l})); }
  const url='/resources/'+tab.value;
  if(form.value.id) await api(url+'/'+form.value.id,{method:'PUT',body:form.value});
  else await api(url,{method:'POST',body:form.value});
  form.value=null; load();
}
async function saveQuestion(){
  const url='/resources/questions';
  if(qform.value.id) await api(url+'/'+qform.value.id,{method:'PUT',body:qform.value});
  else await api(url,{method:'POST',body:qform.value});
  qform.value=null; load();
}
function editQuiz(){ quizForm.value={title:'',description:'',time_minutes:20,sel:[]}; }
async function saveQuiz(){
  await api('/resources/quizzes',{method:'POST',body:{...quizForm.value,
    question_ids:quizForm.value.sel,published:true}});
  quizForm.value=null; load();
}
async function remove(x){
  if(!confirm('确认删除？'))return;
  await api('/resources/'+tab.value+'/'+x.id,{method:'DELETE'}); load();
}
onMounted(load);
</script>