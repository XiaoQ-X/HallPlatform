<template>
  <div class="max-w-7xl">
    <!-- 列表 -->
    <div v-if="!current">
      <div class="flex flex-wrap items-center gap-3 mb-6">
        <div class="relative">
          <input v-model="kw" class="input pl-10 w-64" placeholder="搜索应用案例…"/>
          <Icon name="search" :size="16" class="absolute left-3.5 top-3 text-ink-400"/>
        </div>
        <button v-for="c in cats" :key="c" @click="cat=c"
          :class="['px-4 py-2 rounded-full text-sm transition', cat===c?'bg-brand-600 text-white shadow-soft':'bg-white/80 text-ink-500 border border-ink-100']">{{ c }}</button>
      </div>

      <div class="grid md:grid-cols-2 xl:grid-cols-3 gap-5">
        <div v-for="x in filtered" :key="x.id" class="card-hover p-6 cursor-pointer" @click="open(x)">
          <div class="flex items-start">
            <div class="w-14 h-14 rounded-2xl flex items-center justify-center text-2xl shadow-soft" :style="{background:'linear-gradient(135deg,'+x.color+','+x.color+'cc)'}">{{ x.cover }}</div>
            <span class="ml-auto chip-gray">{{ x.difficulty }}</span>
          </div>
          <div class="font-bold text-ink-900 mt-4 leading-6">{{ x.title }}</div>
          <div class="text-sm text-ink-400 mt-1.5 leading-6 line-clamp-2">{{ x.subtitle }}</div>
          <div class="flex flex-wrap gap-1.5 mt-4">
            <span v-for="t in x.tags.slice(0,3)" :key="t" class="chip-gray text-[11px]">{{ t }}</span>
          </div>
          <div class="mt-4 text-brand-600 text-sm flex items-center gap-1">阅读案例 <Icon name="chevronR":size="15"/></div>
        </div>
      </div>
    </div>

    <!-- 详情 -->
    <div v-else class="animate-fadeUp max-w-5xl mx-auto">
      <button class="btn-ghost mb-5" @click="current=null"><Icon name="chevronL":size="16"/> 返回案例库</button>
      <div class="card overflow-hidden">
        <div class="p-8 text-white relative" :style="{background:'linear-gradient(125deg,'+current.color+',#0a4740)'}">
          <div class="absolute right-8 top-6 text-6xl opacity-30">{{ current.cover }}</div>
          <span class="chip bg-white/20 text-white">{{ current.category }}</span>
          <h2 class="text-2xl font-bold mt-3">{{ current.title }}</h2>
          <p class="text-white/80 mt-2">{{ current.subtitle }}</p>
        </div>
        <div class="p-8 grid lg:grid-cols-3 gap-8">
          <RichText class="lg:col-span-2" :html="current.content"/>
          <div>
            <div class="card p-5 bg-ink-50/60 space-y-3 text-sm">
              <div><div class="text-xs text-ink-400">难度</div><b>{{ current.difficulty }}</b></div>
              <div><div class="text-xs text-ink-400">关键词</div><div class="flex flex-wrap gap-1 mt-1"><span v-for="t in current.tags" :key="t" class="chip-gray text-[11px]">{{ t }}</span></div></div>
              <div><div class="text-xs text-ink-400">阅读量</div><b>{{ current.views }}</b></div>
            </div>
            <button class="btn-primary w-full mt-4" @click="launch"><Icon name="play":size="16"/> 启动对应仿真实验</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter,useRoute } from 'vue-router';
import Icon from '../../components/Icon.vue';
import RichText from '../../components/RichText.vue';
import { api } from '../../api';
const router = useRouter();
const route=useRoute();
const rows = ref([]), current = ref(null), kw = ref(''), cat = ref('全部');
const cats = computed(()=>['全部',...new Set(rows.value.map(x=>x.category))]);
const filtered = computed(()=>rows.value.filter(x=>
  (cat.value==='全部'||x.category===cat.value)&&
  (!kw.value||x.title.includes(kw.value)||x.subtitle.includes(kw.value))));
async function open(x){ current.value = await api('/resources/cases/'+x.id); }
function launch(){
  const c = current.value;
  sessionStorage.setItem('pending_sim_config', JSON.stringify({
    caseRef: 'case-'+c.id, caseTitle: c.title, config: c.sim_config }));
  router.push('/sim/lab');
}
onMounted(async()=>{ rows.value = await api('/resources/cases');if(route.query.id)await open({id:route.query.id}); });
watch(()=>route.query.id,id=>{if(id)open({id});else current.value=null;});
</script>
