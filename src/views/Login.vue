<template>
  <div class="min-h-screen flex">
    <!-- 品牌区 -->
    <div class="hidden lg:flex w-[46%] relative overflow-hidden flex-col justify-between p-12 text-white"
      style="background:linear-gradient(155deg,#0a4740,#078775 45%,#0284c7)">
      <div class="flex items-center gap-3">
        <div class="w-12 h-12 rounded-2xl bg-white/15 flex items-center justify-center"><Icon name="flask" :size="24" /></div>
        <b class="text-lg">霍尔效应仿真教学平台</b>
      </div>
      <div class="relative">
        <div class="w-40 h-40 rounded-full border-[14px] border-white/15 absolute -top-24 -left-8 animate-floaty"></div>
        <h2 class="text-4xl font-bold leading-snow">让看不见的<br/>电磁世界<br/><span class="text-brand-100">触手可及</span></h2>
        <p class="mt-4 text-white/80 max-w-sm leading-7">虚拟仿真 · 数据探究 · 同伴协作，构建完整的霍尔效应实验学习闭环。</p>
      </div>
      <div class="flex gap-6 text-sm text-white/70">
        <span class="flex items-center gap-2"><Icon name="check" :size="16"/> 免安装浏览器实验</span>
        <span class="flex items-center gap-2"><Icon name="check" :size="16"/> 全过程数据留痕</span>
      </div>
    </div>

    <!-- 表单区 -->
    <div class="flex-1 flex items-center justify-center p-6">
      <div class="w-full max-w-md animate-fadeUp">
        <div class="lg:hidden flex items-center justify-center gap-3 mb-8">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-700 flex items-center justify-center text-white"><Icon name="flask" /></div>
          <b class="text-lg text-ink-900">霍尔效应仿真教学平台</b>
        </div>
        <h3 class="text-2xl font-bold text-ink-900">{{ mode==='login'?'欢迎回来':'创建账号' }}</h3>
        <p class="text-ink-400 text-sm mt-1 mb-7">{{ mode==='login'?'登录后开启你的实验学习':'注册后加入课程班级' }}</p>

        <div class="space-y-4">
          <div>
            <label class="text-sm text-ink-600 mb-1.5 block">账号</label>
            <input v-model="form.username" class="input" placeholder="请输入账号" />
          </div>
          <div v-if="mode==='register'">
            <label class="text-sm text-ink-600 mb-1.5 block">姓名</label>
            <input v-model="form.name" class="input" placeholder="请输入真实姓名" />
          </div>
          <div v-if="mode==='register'" class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-sm text-ink-600 mb-1.5 block">班级</label>
              <select v-model="form.class_id" class="input">
                <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
            </div>
            <div>
              <label class="text-sm text-ink-600 mb-1.5 block">学号</label>
              <input v-model="form.student_no" class="input" placeholder="选填" />
            </div>
          </div>
          <div>
            <label class="text-sm text-ink-600 mb-1.5 block">密码</label>
            <input v-model="form.password" type="password" class="input" placeholder="请输入密码" @keyup.enter="submit" />
          </div>
        </div>

        <div v-if="error" class="mt-4 text-sm text-rose-500 bg-rose-50 rounded-2xl px-4 py-2.5">{{ error }}</div>

        <button class="btn-primary w-full mt-6 py-3" @click="submit">
          {{ mode==='login'?'登 录':'注 册' }}
        </button>

        <div class="mt-5 text-center text-sm text-ink-400">
          {{ mode==='login'?'还没有账号？':'已有账号？' }}
          <button class="text-brand-600 font-medium" @click="mode=mode==='login'?'register':'login';error=''">
            {{ mode==='login'?'立即注册':'去登录' }}
          </button>
        </div>

        <div class="mt-8 card p-4 text-sm text-ink-500 flex flex-wrap gap-x-6 gap-y-1.5 justify-center">
          <span>教师 <b class="text-brand-700">teacher / 123456</b></span>
          <span>学生 <b class="text-brand-700">student / 123456</b></span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted } from 'vue';
import Icon from '../components/Icon.vue';
import { useAuth } from '../stores/auth';
import { api } from '../api';
const auth = useAuth();
const mode = ref('login');
const form = reactive({ username:'', password:'', name:'', class_id:null, student_no:'' });
const classes = ref([]);
const error = ref('');
onMounted(async()=>{ classes.value = await api('/auth/classes');
  if(classes.value.length) form.class_id = classes.value[0].id; });
async function submit() {
  error.value = '';
  try {
    if (mode.value === 'login') {
      await auth.login(form.username, form.password);
    } else {
      if (!form.name) throw new Error('请填写真实姓名');
      const r = await api('/auth/register', { method:'POST', body:{ ...form } });
      auth.token = r.token; localStorage.setItem('hall_token', r.token);
    }
    const me = await auth.fetchMe();
    location.href = auth.isTeacher ? '/teacher' : '/';
  } catch (e) { error.value = e.message; }
}
</script>