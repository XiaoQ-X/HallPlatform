<template><div class="rich" ref="el"></div></template>
<script setup>
import { ref, watch, onMounted } from 'vue';
import katex from 'katex';
const props = defineProps({ html: String });
const el = ref();
function render(){
  if(!el.value) return;
  let src = props.html || '';
  const blocks = [];
  src = src.replace(/\$\$([\s\S]+?)\$\$|\$([^$]+?)\$/g, (m, d, i) => {
    blocks.push(katex.renderToString(d || i, { displayMode: !!d, throwOnError: false }));
    return `@@K${blocks.length-1}@@`;
  });
  el.value.innerHTML = src;
  el.value.innerHTML = el.value.innerHTML.replace(/@@K(\d+)@@/g, (m, k) => blocks[+k] || m);
}
onMounted(render);
watch(()=>props.html, render);
</script>