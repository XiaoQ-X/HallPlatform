// 实验异常代码 → 中文名称 / 中文描述的统一映射，供清洗台、SimLab 等复用
export const ABNORMAL_CODE_CN = {
  // Unity 运行时上报
  EXPERIMENT_INCOMPLETE: '实验未完成',
  CIRCUIT_CROSS_CONNECTED: '回路跨接',
  CIRCUIT_INCOMPLETE: '回路未接完整',
  CIRCUIT_SOURCE_SHORT: '电流源短路',
  LIVE_SWITCH_CHANGE: '带电切换',
  READING_UNSTABLE: '读数未稳定',
  VOLTAGE_OVER_RANGE: '电压超量程',
  WIRING_REJECTED: '接线被拒绝',
  // 后端 workshop 检测
  INCOMPLETE_GROUP: '分组数据不完整',
  INVALID_NUMBER: '存在无效数值',
  DUPLICATE_GROUP: '分组记录重复',
  REPEAT_OUTLIER: '重复测量离群',
  MANUAL_SUSPECT: '人工标记可疑',
};

export function abnormalName(code) {
  return ABNORMAL_CODE_CN[code] || code || '未知异常';
}

// 个别历史记录的 detail 为英文句子，按 code 做中文解释；其余 detail 原样返回
export function abnormalDetail(code, detail) {
  if (code === 'EXPERIMENT_INCOMPLETE' && detail &&
      /complete group|pending directions/i.test(detail)) {
    return '至少需要完成一个完整测量组，且没有悬而未决的测量方向。';
  }
  return detail || '系统检测到异常操作';
}
