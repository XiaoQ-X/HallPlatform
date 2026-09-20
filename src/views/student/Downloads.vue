<template>
  <div class="max-w-5xl">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-ink-900">软件下载中心</h1>
      <p class="text-sm text-ink-400 mt-1">配套桌面软件，供离线环境使用；在线功能以本平台为准。</p>
    </div>

    <div class="grid md:grid-cols-2 gap-5">
      <div class="card p-6 flex flex-col" v-for="s in softwares" :key="s.file">
        <div class="flex items-center gap-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-700 text-white flex items-center justify-center shadow-glow">
            <Icon :name="s.icon" :size="22"/>
          </div>
          <div>
            <div class="font-bold text-ink-900">{{ s.name }}</div>
            <div class="text-xs text-ink-400">v{{ s.ver }} · {{ s.os }} · {{ s.size }}</div>
          </div>
        </div>
        <p class="text-sm text-ink-600 leading-7 mt-4">{{ s.desc }}</p>
        <ul class="text-xs text-ink-500 mt-3 space-y-1.5 flex-1">
          <li v-for="p in s.points" :key="p" class="flex gap-2"><Icon name="check" :size="14" class="text-brand-600 mt-0.5"/>{{ p }}</li>
        </ul>
        <a :href="'/downloads/' + s.file" :download="s.file" class="btn-primary mt-5">
          <Icon name="download" :size="16"/> 下载安装程序
        </a>
      </div>
    </div>

    <div class="card p-6 mt-6">
      <div class="font-bold text-ink-900 flex items-center gap-2"><Icon name="spark" :size="18" class="text-brand-600"/> 数据互通说明</div>
      <ul class="text-sm text-ink-600 leading-7 mt-3 list-disc pl-5 space-y-1">
        <li>数据工作台与平台工坊使用统一的 <b>7 列 CSV</b>：Timestamp、Operation_Type、Is_Stable、Work_Current_Is(mA)、Excitation_Current_Im(A)、Measured_Voltage_Vh(mV)、Is_Valid_Record。</li>
        <li>工作台生成的分析报告（PDF / 文本 / 图）可在「自主探究课题包」中作为成果提交，参与互评与教师评阅。</li>
        <li>桌面软件不联网、不采集数据；如与平台结果不一致，以平台在线计算口径为准并核对输入。</li>
      </ul>
    </div>
  </div>
</template>

<script setup>
import Icon from '../../components/Icon.vue';
const softwares = [
  {
    name: '霍尔效应数据工作台', ver: '1.1.0', size: '45.3 MB', os: 'Windows 10/11',
    icon: 'calculator', file: 'hall-data-workbench-1.1.0-setup.exe',
    desc: '离线处理霍尔实验数据：7 列 CSV 清洗、四方向换向合成、霍尔系数与载流子浓度、MAD 稳健离群、非线性诊断、A/B 类不确定度报告。',
    points: ['统一 7 列 CSV 导入导出', '分析报告可作为课题成果上传']
  },
  {
    name: '霍尔效应微观实验室', ver: '1.0.0', size: '21.7 MB', os: 'Windows 10/11',
    icon: 'layers', file: 'hall-microscopic-lab-1.0.0-setup.exe',
    desc: '在三维晶体中观察电子在电场、磁场中的运动及霍尔电压的建立过程，是平台「微观机理演示」的桌面版本。',
    points: ['含自由相机与阶段引导', '无需联网即可运行']
  }
];
</script>
