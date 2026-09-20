<template>
  <div class="max-w-7xl space-y-5">
    <div class="card p-4 flex flex-wrap items-center gap-3">
      <button class="btn-primary" @click="newStudent"><Icon name="plus":size="16"/> 新建学生账号</button>
      <button class="btn-soft" @click="showClass=true"><Icon name="users":size="16"/> 新建班级</button>
      <div class="ml-auto text-sm text-ink-400">共 {{ students.length }} 名学生 · {{ classes.length }} 个班级</div>
    </div>

    <div class="card p-6 overflow-x-auto">
      <table class="w-full text-sm min-w-700">
        <thead><tr class="text-xs text-ink-400">
          <th class="text-left py-2 font-medium">账号</th><th class="text-left font-medium">姓名</th>
          <th class="font-medium">学号</th><th class="font-medium">班级</th>
          <th class="font-medium">操作</th></tr></thead>
        <tbody>
          <tr v-for="s in students" :key="s.id" class="border-t border-ink-50">
            <td class="py-3 text-ink-500">{{ s.username }}</td>
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
        <label class="text-xs text-ink-400">登录账号</label>
        <input v-model="form.username" class="input mb-3" :disabled="!!form.id" placeholder="如 zhangsan"/>
        <label class="text-xs text-ink-400">姓名</label>
        <input v-model="form.name" class="input mb-3"/>
        <div class="grid grid-cols-2 gap-3">
          <div><label class="text-xs text-ink-400">学号</label>
            <input v-model="form.student_no" class="input"/></div>
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
import { ref, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const students = ref([]), classes = ref([]);
const form = ref(null), showClass = ref(false);
const cls = ref({ name:'', code:'' });
async function load(){
  students.value = await api('/teacher/students');
  classes.value = await api('/auth/classes');
}
function newStudent(){ form.value = { username:'', password:'', name:'', student_no:'', class_id:classes.value[0]?.id, avatar:'🧑‍🎓' }; }
function edit(s){ form.value = { ...s }; }
async function save(){
  if(!form.value.class_id)return alert('请先创建并选择班级');
  if(form.value.id) await api('/teacher/students/'+form.value.id,{method:'PUT',body:form.value});
  else await api('/teacher/students',{method:'POST',body:form.value});
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
