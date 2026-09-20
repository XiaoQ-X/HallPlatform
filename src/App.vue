<template>
  <div v-if="error" role="alert" class="fixed bottom-4 left-4 right-4 z-[100] bg-rose-50 text-rose-900 border border-rose-300 p-4 rounded-lg flex gap-3"><span class="flex-1">{{error}}</span><button @click="clear" aria-label="关闭错误提示">×</button></div>
  <router-view v-slot="{ Component }">
    <transition name="page" mode="out-in">
      <component :is="Component" />
    </transition>
  </router-view>
</template>
<script setup>
import {ref,onMounted,onBeforeUnmount} from 'vue';
const error=ref('');let dismissTimer=null;
const show=e=>{error.value=e.detail;clearTimeout(dismissTimer);dismissTimer=setTimeout(()=>{error.value='';dismissTimer=null;},6000);};
const clear=()=>{error.value='';clearTimeout(dismissTimer);dismissTimer=null;};
onMounted(()=>window.addEventListener('hall-error',show));
onBeforeUnmount(()=>{window.removeEventListener('hall-error',show);clearTimeout(dismissTimer);});
</script>
