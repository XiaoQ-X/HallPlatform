<template><div class="max-w-4xl space-y-5"><h2 class="font-bold">已发布成绩</h2>
  <div v-if="loading" class="space-y-4">
    <div class="skeleton h-7 w-1/3"></div>
    <div class="skeleton h-28 w-full"></div>
    <div class="skeleton h-28 w-full"></div>
  </div>
  <p v-else-if="!rows.length">教师尚未发布成绩</p>
  <section v-for="g in rows" :key="g.id" class="border-b py-4 space-y-3"><div class="flex justify-between"><b>总分 {{g.payload.total}}</b><span>{{g.created_at}} · 版本 #{{g.id}}</span></div><table class="w-full"><tbody><tr v-for="(c,k) in g.payload.comp"><th class="text-left font-normal py-2">{{labels[k]}}</th><td>{{c.score}}</td><td>{{c.weight*100}}%</td><td>{{c.overridden?'教师调整':''}}</td></tr></tbody></table><p class="text-sm">{{g.payload.reason}}</p><p class="text-sm text-amber-700" v-for="p in g.payload.pending">{{p}}</p><details><summary>评分口径</summary>{{g.payload.policy}}</details><button class="btn-soft" @click="appeal(g)">成绩申诉</button></section></div></template>
<script setup>
import {ref,onMounted} from 'vue';import {useRouter} from 'vue-router';import {api} from '../../api';const rows=ref([]),loading=ref(true),router=useRouter(),labels={sim:'仿真实验',quiz:'自测题',project:'课题',peer:'同伴互评'};function appeal(g){sessionStorage.setItem('appeal_ref',JSON.stringify({ref_type:'grade',ref_id:g.id}));router.push('/peer/appeals');}onMounted(async()=>{try{rows.value=await api('/grades');}finally{loading.value=false;}});
</script>
