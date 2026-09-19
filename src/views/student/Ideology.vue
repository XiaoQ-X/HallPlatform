<template>
  <div class="max-w-7xl">
    <div v-if="!current">
      <div class="rounded-4xl p-8 mb-6 text-white relative overflow-hidden shadow-lift"
        style="background:linear-gradient(120deg,#0a4740,#078775 60%,#0284c7)">
        <div class="absolute right-10 top-4 text-7xl opacity-20">🌟</div>
        <h2 class="text-2xl font-bold">思政素材库</h2>
        <p class="text-white/75 mt-2 max-w-xl">从霍尔的执着到中国半导体人的家国情怀，让科学精神与使命担当融入每一次实验。</p>
      </div>

      <div class="flex flex-wrap gap-2 mb-6">
        <button v-for="c in cats" :key="c" @click="cat=c"
          :class="['px-4 py-2 rounded-full text-sm transition', cat===c?'bg-brand-600 text-white shadow-soft':'bg-white/80 text-ink-500 border border-ink-100']">{{ c }}</button>
      </div>

      <div class="grid md:grid-cols-2 gap-5">
        <div v-for="x in filtered" :key="x.id" class="card-hover p-6 cursor-pointer flex gap-5" @click="open(x)">
          <div class="w-16 h-16 rounded-3xl flex items-center justify-center text-3xl shrink-0 shadow-soft" :style="{background:'linear-gradient(140deg,'+x.color+','+x.color+'bb)'}">{{ x.cover }}</div>
          <div class="min-w-0">
            <span class="chip-gray text-[11px]">{{ x.category }}</span>
            <div class="font-bold text-ink-900 mt-2 leading-6">{{ x.title }}</div>
            <div class="text-sm text-ink-400 mt-1 line-clamp-2 leading-6">{{ x.subtitle }}</div>
          </div>
        </div>
      </div>
    </div>

    <div v-else class="animate-fadeUp max-w-3xl mx-auto">
      <button class="btn-ghost mb-5" @click="current=null"><Icon name="chevronL":size="16"/> 返回素材库</button>
      <div class="card p-8 md:p-10">
        <div class="flex items-center gap-4">
          <div class="w-16 h-16 rounded-3xl flex items-center justify-center text-3xl shadow-soft text-white" :style="{background:current.color}">{{ current.cover }}</div>
          <div>
            <span class="chip-gray text-[11px]">{{ current.category }} · {{ current.source }}</span>
            <h2 class="text-xl font-bold text-ink-900 mt-1.5">{{ current.title }}</h2>
          </div>
        </div>
        <RichText class="mt-6" :html="current.content"/>
        <div class="divider-soft my-7"></div>
        <div class="rounded-3xl bg-brand-50/70 p-5 text-sm text-brand-800 leading-7">
          <b class="flex items-center gap-2"><Icon name="spark":size="16"/> 感悟与思考</b>
          <p class="mt-1.5 text-ink-600">结合你的仿真实验经历，谈谈这段素材带给你的启发。可将思考写入课题报告，或在同伴互评中与同学交流。</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import RichText from '../../components/RichText.vue';
import { api } from '../../api';
const rows = ref([]), current = ref(null), cat = ref('全部');
const cats = computed(()=>['全部',...new Set(rows.value.map(x=>x.category))]);
const filtered = computed(()=>rows.value.filter(x=>cat.value==='全部'||x.category===cat.value));
async function open(x){ current.value = await api('/resources/ideology/'+x.id); }
onMounted(async()=>{ rows.value = await api('/resources/ideology'); });
</script>