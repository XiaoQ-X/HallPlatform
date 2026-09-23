<template>
  <div class="min-h-screen flex">
    <!-- 移动端遮罩 -->
    <div v-if="mobileOpen" class="fixed inset-0 bg-black/40 z-30 lg:hidden" @click="mobileOpen=false"></div>
    <!-- 侧边导航（移动端抽屉，桌面固定） -->
    <aside v-if="!standaloneSimulation" :class="['glass fixed lg:sticky top-0 h-screen w-64 flex flex-col p-4 rounded-r-[2rem] z-40 transition-all duration-300',
      mobileOpen?'translate-x-0':'-translate-x-full lg:translate-x-0', collapsed?'lg:w-24':'']">
      <div class="flex items-center gap-3 px-2 py-2">
        <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-700 flex items-center justify-center text-white shadow-glow">
          <Icon name="flask" :size="22" />
        </div>
        <div v-if="!collapsed">
          <div class="font-bold text-ink-900 leading-tight">霍尔效应</div>
          <div class="text-xs text-ink-400">仿真教学平台</div>
        </div>
      </div>

      <div class="overflow-y-auto flex-1 mt-3 pr-1 space-y-4" @click="mobileOpen=false">
        <div>
          <router-link to="/" class="nav-item" :class="isActive('/')" active-class="">
            <Icon name="home" /> <span v-if="!collapsed">学习首页</span>
          </router-link>
          <router-link to="/grades" class="nav-item"><Icon name="award"/><span v-if="!collapsed">我的成绩</span></router-link>
        </div>

        <div v-for="g in groups" :key="g.title">
          <div v-if="!collapsed" class="px-4 mb-1 text-[11px] font-semibold text-ink-400 tracking-widest">{{ g.title }}</div>
          <router-link v-for="m in g.items" :key="m.to" :to="m.to" class="nav-item" :class="isActive(m.to)">
            <Icon :name="m.icon" /> <span v-if="!collapsed">{{ m.label }}</span>
            <span v-if="!collapsed && m.badge" class="ml-auto chip">{{ m.badge }}</span>
          </router-link>
        </div>
      </div>

      <div class="mt-2 pt-3 border-t border-ink-100">
        <router-link v-if="auth.isTeacher" to="/teacher" class="nav-item">
          <Icon name="shield" /> <span v-if="!collapsed">教师管理端</span>
        </router-link>
        <div class="nav-item hidden lg:flex" @click="toggleCollapse">
          <Icon :name="collapsed?'chevronR':'chevronL'" /> <span v-if="!collapsed">收起导航</span>
        </div>
      </div>
    </aside>

    <!-- 主区域 -->
    <div class="flex-1 min-w-0 flex flex-col">
      <header v-if="!standaloneSimulation" class="sticky top-0 z-30 px-4 lg:px-6 py-3 glass flex items-center gap-3 lg:gap-4 rounded-b-[1.6rem]">
        <button class="lg:hidden w-10 h-10 rounded-2xl bg-white/80 border border-ink-100 flex items-center justify-center text-ink-700 shrink-0" @click="mobileOpen=true" aria-label="打开导航">
          <Icon name="menu" :size="20" />
        </button>
        <h1 class="text-lg font-bold text-ink-900 truncate">{{ title }}</h1>
        <div class="ml-auto flex items-center gap-3">
          <!-- 通知 -->
          <div class="relative">
            <button aria-label="通知中心" title="通知中心" class="w-11 h-11 rounded-2xl bg-white/80 border border-ink-100 flex items-center justify-center text-ink-600 hover:text-brand-600 relative" @click="showNoti=!showNoti">
              <Icon name="bell" :size="19" />
              <span v-if="unread" class="absolute -top-0.5 -right-0.5 min-w-5 h-5 px-1 rounded-full bg-rose-500 text-white text-[10px] flex items-center justify-center">{{ unread }}</span>
            </button>
            <div v-if="showNoti" class="absolute right-0 mt-2 w-[min(20rem,calc(100vw-2rem))] card p-3 z-50">
              <div class="flex items-center px-1 pb-2">
                <b class="text-ink-900">通知中心</b>
                <button class="ml-auto text-xs text-brand-600" @click="readAll">全部已读</button>
              </div>
              <div class="space-y-1 max-h-80 overflow-y-auto">
                <div v-for="n in notis" :key="n.id" class="p-3 rounded-2xl hover:bg-ink-50 cursor-pointer" @click="openNoti(n)">
                  <div class="flex gap-2">
                    <span :class="['mt-1.5 w-2 h-2 rounded-full shrink-0', n.read?'bg-ink-200':'bg-brand-500']"></span>
                    <div>
                      <div class="text-sm font-medium text-ink-800">{{ n.title }}</div>
                      <div class="text-xs text-ink-400 mt-0.5">{{ n.content }}</div>
                    </div>
                  </div>
                </div>
                <div v-if="!notis.length" class="text-center text-sm text-ink-400 py-8">暂无通知</div>
                <button v-if="notis.length>=50" class="btn-soft" @click="loadOlder">更早通知</button>
              </div>
            </div>
          </div>
          <!-- 用户 -->
          <div class="flex items-center gap-3 pl-3 py-1.5 pr-4 rounded-full bg-white/80 border border-ink-100 cursor-pointer hover:border-brand-300" @click="showProfile=true">
            <div class="w-9 h-9 rounded-full bg-gradient-to-br from-brand-100 to-brand-300 flex items-center justify-center text-lg">{{ auth.user?.avatar }}</div>
            <div class="leading-tight">
              <div class="text-sm font-semibold text-ink-800">{{ auth.user?.name }}</div>
              <div class="text-[11px] text-ink-400">{{ auth.user?.class_name || '学生' }}</div>
            </div>
            <button aria-label="退出登录" title="退出登录" class="text-ink-400 hover:text-rose-500 ml-1" @click.stop="auth.logout"><Icon name="logout" :size="18" /></button>
          </div>
        </div>
      </header>

      <main :class="[standaloneSimulation ? 'p-0' : 'p-4 lg:p-6', 'flex-1']">
        <InsecureContextNotice v-if="!standaloneSimulation"/>
        <router-view v-slot="{ Component }">
          <Suspense>
            <component :is="Component" />
            <template #fallback>
              <div class="card min-h-48 flex items-center justify-center text-sm text-ink-400">正在加载页面…</div>
            </template>
          </Suspense>
        </router-view>
      </main>
    </div>
    <ProfileModal v-if="showProfile" :user="auth.user" @close="showProfile=false"/>
  </div>
</template>

<script setup>
import { ref, computed, onMounted,onBeforeUnmount } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Icon from '../components/Icon.vue';
import ProfileModal from '../components/ProfileModal.vue';
import InsecureContextNotice from '../components/InsecureContextNotice.vue';
import { useAuth } from '../stores/auth';
import { api } from '../api';

const auth = useAuth();
const route = useRoute(), router = useRouter();
const collapsed = ref(false), showProfile = ref(false), mobileOpen = ref(false);
const standaloneSimulation = computed(() => route.path === '/sim/lab' && route.query.case === 'microscopic');
const toggleCollapse = () => collapsed.value = !collapsed.value;

const groups = [
  { title: '仿真实验', items: [
    { to: '/sim/lab', label: '霍尔效应基础实验', icon: 'flask', badge: '核心' },
    { to: '/sim/lab?case=microscopic', label: '微观机理演示', icon: 'layers' },
    { to: '/sim/side-effects', label: '副效应与误差修正', icon: 'gauge' }
  ]},
  { title: '教学资源', items: [
    { to: '/resources/cases', label: '应用案例库', icon: 'book' },
    { to: '/resources/ideology', label: '思政素材库', icon: 'star' },
    { to: '/resources/quiz', label: '思考题 / 自测题', icon: 'help' },
    { to: '/resources/projects', label: '自主探究课题包', icon: 'folder' },
    { to: '/resources/downloads', label: '离线工具（可选）', icon: 'download' }
  ]},
  { title: '数据处理工坊', items: [
    { to: '/workshop/cleaning', label: '数据清洗台', icon: 'filter' },
    { to: '/workshop/uncertainty', label: '不确定度计算器', icon: 'calculator' },
    { to: '/workshop/analysis', label: '多元分析工具', icon: 'chart' }
  ]},
  { title: '同伴互评', items: [
    { to: '/peer/works', label: '作品与互评', icon: 'users' },
    { to: '/peer/appeals', label: '反馈与申诉', icon: 'message' }
  ]}
];

const titleMap = {
  '/': '学习首页', '/sim/lab': '霍尔效应基础实验', '/sim/side-effects': '副效应与误差修正', '/grades':'我的成绩',
  '/resources/cases': '应用案例库', '/resources/downloads': '离线工具（可选）', '/resources/ideology': '思政素材库',
  '/resources/quiz': '思考题 / 自测题', '/resources/projects': '自主探究课题包',
  '/workshop/cleaning': '数据清洗台', '/workshop/uncertainty': '不确定度计算器',
  '/workshop/analysis': '多元分析工具', '/peer/works': '同伴互评', '/peer/appeals': '反馈与申诉'
};
const title = computed(() => {
  if (route.path === '/sim/lab' && route.query.case === 'microscopic') return '微观机理演示';
  if (route.path === '/sim/lab' && route.query.case === 'side-effects') return '副效应与误差修正';
  return titleMap[route.path] || '霍尔效应平台';
});
const isActive = p => {
  const [path, q] = p.split('?');
  if (route.path !== path) return '';
  if (q) {
    const params = new URLSearchParams(q);
    for (const [k, v] of params) if (String(route.query[k]) !== v) return '';
  } else if (route.query.case) {
    return '';
  }
  return 'active';
};

const notis = ref([]), showNoti = ref(false);
const unread=ref(0);let notiTimer;
async function loadNoti(){notis.value=await api('/notifications');unread.value=(await api('/notifications-count')).unread;}
async function loadOlder(){const older=await api('/notifications?before='+notis.value.at(-1).id);notis.value.push(...older);}
async function readAll() { await api('/notifications/read-all', { method: 'PUT' }); loadNoti(); }
async function openNoti(n) {
  await api('/notifications/' + n.id + '/read', { method: 'PUT' });
  await loadNoti();showNoti.value=false;
  if (n.link) router.push(n.link);
}
onMounted(async()=>{await auth.fetchMe();await loadNoti();notiTimer=setInterval(()=>loadNoti().catch(()=>{}),60000);});onBeforeUnmount(()=>clearInterval(notiTimer));
</script>
