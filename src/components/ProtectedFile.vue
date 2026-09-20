<template><video v-if="video&&url" :src="url" controls class="w-full max-h-72"/><button v-else-if="!video" class="btn-soft" @click="download">{{name||'下载附件'}}</button><span v-if="error" role="alert" class="text-rose-600">{{error}}</span></template>
<script setup>
import {ref,watch,onBeforeUnmount} from 'vue';import {getToken} from '../api';
const props=defineProps({src:String,name:String,video:Boolean});const url=ref(''),error=ref('');let controller;
async function load(){controller?.abort();controller=new AbortController();if(url.value)URL.revokeObjectURL(url.value);url.value='';error.value='';if(!props.src?.startsWith('/api/files/')){error.value='旧附件需要教师确认后重新上传';return;}try{const r=await fetch(props.src,{headers:{Authorization:'Bearer '+getToken()},signal:controller.signal});if(!r.ok)throw Error('附件无法访问');url.value=URL.createObjectURL(await r.blob());}catch(e){if(e.name!=='AbortError')error.value=e.message;}}
async function download(){if(!url.value)await load();if(url.value){const a=document.createElement('a');a.href=url.value;a.download=props.name||'附件';a.click();}}
watch(()=>props.src,()=>{controller?.abort();if(url.value)URL.revokeObjectURL(url.value);url.value='';error.value='';if(props.video)load();},{immediate:true});onBeforeUnmount(()=>{controller?.abort();if(url.value)URL.revokeObjectURL(url.value);});
</script>
