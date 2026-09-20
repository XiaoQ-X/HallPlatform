<template>
  <div class="min-h-screen flex">
    <aside class="sticky top-0 h-screen w-64 flex flex-col p-4 rounded-r-[2rem] z-40 glass-dark shadow-lift">
      <div class="flex items-center gap-3 px-2 py-2">
        <div class="w-11 h-11 rounded-2xl bg-white/15 flex items-center justify-center"><Icon name="shield" :size="22" /></div>
        <div>
          <div class="font-bold leading-tight">教师管理端</div>
          <div class="text-xs text-brand-100/70">霍尔效应教学平台</div>
        </div>
      </div>
      <div class="flex-1 mt-4 space-y-1.5">
        <router-link to="/teacher" class="nav-item-dark" :class="isActive('/teacher')"><Icon name="gauge" /> 数据看板</router-link>
        <router-link to="/teacher/resources" class="nav-item-dark" :class="isActive('/teacher/resources')"><Icon name="layers" /> 资源内容管理</router-link>
        <router-link to="/teacher/students" class="nav-item-dark" :class="isActive('/teacher/students')"><Icon name="users" /> 学生管理</router-link>
        <router-link to="/teacher/pushes" class="nav-item-dark" :class="isActive('/teacher/pushes')"><Icon name="send" /> 资源推送管理</router-link>
        <router-link to="/teacher/grades" class="nav-item-dark" :class="isActive('/teacher/grades')"><Icon name="award" /> 成绩汇总</router-link>
        <router-link to="/teacher/review" class="nav-item-dark"><Icon name="check"/>教学评阅</router-link>
        <router-link to="/teacher/rubrics" class="nav-item-dark"><Icon name="layers"/>量表管理</router-link>
        <router-link to="/teacher/appeals" class="nav-item-dark" :class="isActive('/teacher/appeals')"><Icon name="chat" /> 申诉处理</router-link>
      </div>
      <div class="pt-3 border-t border-white/10 space-y-1.5">
        <div class="nav-item-dark" @click="auth.logout()"><Icon name="logout" /> 退出登录</div>
      </div>
    </aside>

    <div class="flex-1 min-w-0 flex flex-col">
      <header class="sticky top-0 z-30 px-6 py-3 glass flex items-center gap-4 rounded-b-[1.6rem]">
        <h1 class="text-lg font-bold text-ink-900">{{ title }}</h1>
        <div class="ml-auto flex items-center gap-3 pl-3 py-1.5 pr-4 rounded-full bg-white/80 border border-ink-100 cursor-pointer hover:border-brand-300" @click="showProfile=true">
          <div class="w-9 h-9 rounded-full bg-gradient-to-br from-brand-100 to-brand-300 flex items-center justify-center text-lg">{{ auth.user?.avatar }}</div>
          <div class="leading-tight">
            <div class="text-sm font-semibold text-ink-800">{{ auth.user?.name }}</div>
            <div class="text-[11px] text-ink-400">任课教师</div>
          </div>
        </div>
      </header>
      <main class="p-6 flex-1"><router-view /></main>
    </div>
    <ProfileModal v-if="showProfile" :user="auth.user" @close="showProfile=false"/>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import Icon from '../components/Icon.vue';
import ProfileModal from '../components/ProfileModal.vue';
import { useAuth } from '../stores/auth';
const auth = useAuth();
const route = useRoute();
const showProfile = ref(false);
const isActive = p => route.path === p ? 'active-dark' : '';
const title = computed(() => ({
  '/teacher':'数据看板','/teacher/resources':'资源内容管理',
  '/teacher/students':'学生管理',
  '/teacher/pushes':'资源推送管理','/teacher/grades':'成绩汇总',
  '/teacher/appeals':'申诉处理','/teacher/review':'教学评阅','/teacher/rubrics':'评价量表'
}[route.path]));
onMounted(()=>auth.fetchMe());
</script>

<style scoped>
.nav-item-dark {
  display:flex; align-items:center; gap:.75rem; padding:.7rem 1rem; border-radius:1rem;
  color:#cdeee7; font-size:.875rem; font-weight:500; transition:.2s; cursor:pointer;
}
.nav-item-dark:hover { background: rgba(255,255,255,.12); color:#fff; }
.nav-item-dark.active-dark { background: rgba(255,255,255,.22); color:#fff; box-shadow: inset 0 0 0 1px rgba(255,255,255,.18); }
</style>
