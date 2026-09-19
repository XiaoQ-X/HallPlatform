<template><span class="formula" :class="display&&'formula-block'" ref="el"></span></template>
<script setup>
import { ref, watch, onMounted } from 'vue';
import katex from 'katex';
import 'katex/dist/katex.min.css';
const props = defineProps({ expr: String, display: Boolean });
const el = ref();
function render(){
  if(!el.value) return;
  el.value.innerHTML = katex.renderToString(props.expr || '',
    { displayMode: props.display, throwOnError: false });
}
onMounted(render);
watch(()=>props.expr, render);
</script>
<style>
.formula-block { display:block; margin:.2rem 0; overflow-x:auto; }
.formula .katex { font-size:1.02em; }
</style>