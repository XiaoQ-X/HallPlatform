<template>
  <div class="modal-mask" @click.self="$emit('close')">
    <div class="modal p-6 w-[26rem]">
      <h3 class="font-bold text-lg mb-4">个人资料</h3>

      <!-- 头像 -->
      <div class="flex items-center gap-4 mb-5">
        <div class="w-16 h-16 rounded-full bg-gradient-to-br from-brand-100 to-brand-300 flex items-center justify-center text-3xl shadow-soft">{{ form.avatar }}</div>
        <div class="flex-1">
          <div class="text-xs text-ink-400 mb-2">选择头像</div>
          <div class="flex flex-wrap gap-1.5">
            <button v-for="a in avatars" :key="a" @click="form.avatar=a"
              :class="['w-9 h-9 rounded-xl text-lg flex items-center justify-center transition',
                form.avatar===a?'bg-brand-100 ring-2 ring-brand-400':'bg-ink-50 hover:bg-ink-100']">{{ a }}</button>
          </div>
        </div>
      </div>

      <label for="profile-name" class="text-xs text-ink-400">{{ user?.identity_anonymized ? '匿名名称' : '姓名' }}</label>
      <input id="profile-name" v-model="form.name" :readonly="!!user?.identity_anonymized" :aria-describedby="user?.identity_anonymized ? 'profile-identity-note' : undefined" class="input" :class="user?.identity_anonymized ? 'bg-ink-50' : 'mb-4'"/>
      <p v-if="user?.identity_anonymized" id="profile-identity-note" class="text-xs text-ink-500 leading-6 mt-2 mb-4">名称中的编号是系统分配的演示匿名标识，不可修改。原登录账号和密码保持有效。</p>

      <div class="rounded-2xl bg-ink-50 p-4">
        <div class="text-xs font-medium text-ink-500 mb-3">修改密码（不修改可留空）</div>
        <input v-model="oldPassword" type="password" class="input mb-2" placeholder="当前密码"/>
        <input v-model="newPassword" type="password" minlength="6" class="input" placeholder="新密码（至少 6 位）"/>
      </div>

      <div v-if="error" class="text-xs text-rose-500 mt-3">{{ error }}</div>
      <div class="flex gap-3 mt-5">
        <button class="btn-ghost flex-1" @click="$emit('close')">取消</button>
        <button class="btn-primary flex-1" @click="save">保存</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref, watch } from 'vue';
import { api, setToken } from '../api';
import { useAuth } from '../stores/auth';
const props = defineProps({ user: Object });
const emit = defineEmits(['close','saved']);
const auth = useAuth();
const avatars = ['🧑‍🎓','👨‍🎓','👩‍🎓','🧑‍🔬','👨‍🔬','👩‍🔬','🧑‍🏫','👨‍🏫','👩‍🏫','🦊','🐼','🐱','🌟','🚀','⚛️','🔬'];
const form = reactive({ name:'', avatar:'' });
const oldPassword = ref(''), newPassword = ref(''), error = ref('');
watch(()=>props.user, u => { if(u){ form.name=u.name||''; form.avatar=u.avatar||'🧑‍🎓'; } }, { immediate:true });
async function save(){
  error.value='';
  try {
    const body = { avatar:form.avatar };
    if (!props.user?.identity_anonymized) body.name = form.name;
    if(newPassword.value){ body.oldPassword=oldPassword.value; body.newPassword=newPassword.value; }
    const r = await api('/auth/me', { method:'PUT', body });
    if(r.token){setToken(r.token);auth.token=r.token;}
    auth.user = r.user;
    emit('saved', r.user); emit('close');
  } catch(e){ error.value = e.message; }
}
</script>
