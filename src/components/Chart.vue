<template><div ref="el" :style="{width:'100%',height:height}"></div></template>
<script setup>
import { ref, onMounted, watch, onBeforeUnmount } from 'vue';
import * as echarts from 'echarts';
const props = defineProps({ option: Object, height: { type: String, default: '320px' } });
const el = ref(null); let chart;
function render(){ if(!chart)chart=echarts.init(el.value); chart.setOption(props.option, true); }
onMounted(render);
watch(()=>props.option, render, { deep: true });
const ro = new ResizeObserver(()=>chart?.resize());
onMounted(()=>ro.observe(el.value));
onBeforeUnmount(()=>{ ro.disconnect(); chart?.dispose(); });
</script>