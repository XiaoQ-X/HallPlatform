<template>
  <div v-if="show" class="mb-4 flex items-start gap-3 rounded-2xl bg-amber-50 border border-amber-200 text-amber-800 px-4 py-3 text-xs leading-5">
    <Icon name="alert" :size="16" class="mt-0.5 shrink-0 text-amber-500"/>
    <div class="flex-1">
      当前通过 <b>HTTP</b> 访问，浏览器已限制<b>摄像头、离线缓存</b>等功能（实验与数据处理等核心功能不受影响）；完成 HTTPS 部署（需备案）后将自动恢复。
    </div>
    <button class="text-amber-500 hover:text-amber-700 shrink-elf text-base leading-none" aria-label="关闭提示" @click="close">✕</button>
  </div>
</template>
<script setup>
import { ref } from 'vue';
import Icon from '../components/Icon.vue';
const show = ref(!window.isSecureContext && sessionStorage.getItem('insecure_notice_closed') !== '1');
function close(){ try{sessionStorage.setItem('insecure_notice_closed','1');}catch{} show.value = false; }
</script>
