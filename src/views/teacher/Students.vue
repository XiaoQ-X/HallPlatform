<template>
  <div class="max-w-7xl space-y-5">
    <div class="card p-4 flex flex-wrap items-center gap-3">
      <button class="btn-primary" @click="newStudent"><Icon name="plus":size="16"/> 新建学生账号</button>
      <button class="btn-soft" @click="showClass=true"><Icon name="users":size="16"/> 新建班级</button>
      <div class="ml-auto text-sm text-ink-400">共 {{ students.length }} 名学生 · {{ classes.length }} 个班级</div>
    </div>

    <div class="card p-6 overflow-x-auto">
      <div v-if="loading" class="space-y-3 py-1">
        <div v-for="i in 8" :key="i" class="skeleton h-7" :style="{width:55+(i%4)*11+'%'}"></div>
      </div>
      <table v-else class="w-full text-sm min-w-700">
        <thead><tr class="text-xs text-ink-400">
          <th class="text-left py-2 font-medium">账号</th><th class="text-left font-medium">姓名</th>
          <th class="font-medium">学号</th><th class="font-medium">班级</th>
          <th class="font-medium">操作</th></tr></thead>
        <tbody>
          <tr v-for="s in students" :key="s.id" class="border-t border-ink-50">
            <td class="py-3 text-ink-500" :title="s.account_label?'登录账号保持不变':''">{{ s.account_label||s.username }}</td>
            <td class="text-left flex items-center gap-2"><span>{{ s.avatar }}</span>{{ s.name }}</td>
            <td class="text-center">{{ s.student_no||'—' }}</td>
            <td class="text-center">{{ s.class_name||'未分班' }}</td>
            <td class="text-center">
              <button class="text-brand-600 text-xs mr-3" @click="edit(s)">编辑</button>
              <button class="text-amber-600 text-xs" @click="reset(s)">重置密码</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 学生编辑/新建 -->
    <div v-if="form" class="modal-mask" @click.self="form=null">
      <div class="modal p-6">
        <h3 class="font-bold text-lg mb-4">{{ form.id?'编辑学生':'新建学生账号' }}</h3>
        <template v-if="!form.id || !anonymousPresentation">
          <label class="text-xs text-ink-400">登录账号</label>
          <input v-model="form.username" class="input mb-3" :disabled="!!form.id" placeholder="如 zhangsan"/>
        </template>
        <div v-else class="mb-3 rounded-xl bg-ink-50 px-3 py-2 text-xs text-ink-500">
          登录账号：学生账号（匿名展示，账号保持不变）
        </div>
        <p v-if="anonymousPresentation" id="student-alias-note" class="text-xs text-ink-500 leading-6 mb-3">{{ form.id ? '姓名和学号是固定的演示匿名标识，原登录账号保持有效。' : '保存后系统自动分配“同学N”和学号“N”，无需填写真实姓名或学号。请设置用于登录的账号和密码。' }}</p>
        <label for="student-name" class="text-xs text-ink-400">{{ anonymousPresentation ? '匿名姓名' : '姓名' }}</label>
        <input id="student-name" v-model="form.name" :readonly="anonymousPresentation" :placeholder="anonymousPresentation && !form.id ? '保存后自动分配，如 同学1' : ''" :aria-describedby="anonymousPresentation ? 'student-alias-note' : undefined" class="input mb-3" :class="anonymousPresentation ? 'bg-ink-50' : ''"/>
        <div class="grid grid-cols-2 gap-3">
          <div><label for="student-number" class="text-xs text-ink-400">{{ anonymousPresentation ? '匿名学号' : '学号' }}</label>
            <input id="student-number" v-model="form.student_no" :readonly="anonymousPresentation" :placeholder="anonymousPresentation && !form.id ? '保存后自动分配' : ''" :aria-describedby="anonymousPresentation ? 'student-alias-note' : undefined" class="input" :class="anonymousPresentation ? 'bg-ink-50' : ''"/></div>
          <div><label class="text-xs text-ink-400">班级</label>
            <select v-model="form.class_id" class="input">
              <option :value="null" disabled>请选择任课班级</option>
              <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option></select></div>
        </div>
        <label class="text-xs text-ink-400 mt-3 block">头像</label>
        <input v-model="form.avatar" class="input mb-3" placeholder="emoji"/>
        <label v-if="!form.id" class="text-xs text-ink-400 block">初始密码</label>
        <input v-if="!form.id" v-model="form.password" type="password" minlength="6" autocomplete="new-password" class="input mb-3" placeholder="至少6位初始密码"/>
        <div class="flex gap-3 mt-2"><button class="btn-ghost flex-1" @click="form=null">取消</button>
          <button class="btn-primary flex-1" @click="save">保存</button></div>
      </div>
    </div>

    <!-- 新建班级 -->
    <div v-if="showClass" class="modal-mask" @click.self="showClass=false">
      <div class="modal p-6">
        <h3 class="font-bold text-lg mb-4">新建班级</h3>
        <label class="text-xs text-ink-400">班级名称</label>
        <input v-model="cls.name" class="input mb-3" placeholder="如 物理2302班"/>
        <label class="text-xs text-ink-400">班级代码</label>
        <input v-model="cls.code" class="input mb-4" placeholder="如 PHY2302"/>
        <div class="flex gap-3"><button class="btn-ghost flex-1" @click="showClass=false">取消</button>
          <button class="btn-primary flex-1" @click="saveClass">创建</button></div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
import { useAuth } from '../../stores/auth';
const auth = useAuth();
const students = ref([]), classes = ref([]), loading=ref(true);
const form = ref(null), showClass = ref(false);
const cls = ref({ name:'', code:'' });
// 演示匿名化开启时，列表会提供 account_label；编辑表单不得回显真实登录账号。
const anonymousPresentation = computed(() => !!auth.user?.identity_anonymized || students.value.some(s => s.account_label));
async function load(){loading.value=true;try{
  students.value = await api('/teacher/students');
  classes.value = await api('/auth/classes');
}finally{loading.value=false;}}
function newStudent(){ form.value = { username:'', password:'', name:'', student_no:'', class_id:classes.value[0]?.id, avatar:'🧑' }; }
function edit(s){
  const next = { ...s };
  if (anonymousPresentation.value) delete next.username;
  form.value = next;
}
async function save(){
  if(!form.value.class_id)return alert('请先创建并选择班级');
  const body = { ...form.value };
  if (anonymousPresentation.value) { delete body.name; delete body.student_no; }
  if(form.value.id) await api('/teacher/students/'+form.value.id,{method:'PUT',body});
  else await api('/teacher/students',{method:'POST',body});
  form.value=null; load();
}
async function reset(s){
  const password = prompt('为「'+s.name+'」设置新密码：');
  if(!password)return;
  await api('/teacher/students/'+s.id+'/reset-password',{method:'POST',body:{password}});
  alert('密码已重置');
}
async function saveClass(){
  if(!cls.value.name)return alert('请填写班级名称');
  await api('/teacher/classes',{method:'POST',body:cls.value});
  showClass.value=false; cls.value={name:'',code:''}; load();
}
onMounted(load);
</script>
