# 霍尔效应仿真 Web 通信协议 v1

适用交付：2.3.0-web.1。对象名固定为 `WebBridge`。Unity 2022.3.62f3c1，内置管线。此文描述实际实现；先前的参数清单是迁移前的事实快照。

## 1. 接入与生命周期

部署整个 WebGL 目录，打开 `index.html` 为独立实验，`handoff.html` 为开发者联调页。必须通过 HTTP(S)，不能双击 file://。WebGL 首次加载结束后发送 `OnReady`；宿主脚本绑定 Unity 实例后发出 `hall-ready` DOM 事件。只有 `HallHost.ready === true` 时才能发命令。

直接调用：

```js
window.addEventListener('hall-ready', () => {
  HallHost.send('InitExperiment', {
    schemaVersion: 1, caseId: 'hall-basic', material: 'n-silicon',
    thickness_mm: 0.5, maxIs_mA: 10, maxIm_A: 1
  });
});
window.addEventListener('hall-event', ({detail}) => console.log(detail));
// 也可使用 window.OnMeasureData = payload => { ... } 等命名回调。
```

同源 iframe 接入（校验来源窗口和 origin）：

```js
const frame = document.querySelector('iframe');
window.addEventListener('message', event => {
  if (event.origin !== location.origin || event.source !== frame.contentWindow) return;
  if (event.data.source === 'hall-host' && event.data.ready) {
    frame.contentWindow.postMessage({source:'hall-platform', requestId:'init-1',
      method:'InitExperiment', payload:{schemaVersion:1,caseId:'hall-basic',
      material:'n-silicon',thickness_mm:0.5,maxIs_mA:10,maxIm_A:1}}, location.origin);
  }
  if (event.data.source === 'hall-unity') consume(event.data.event);
});
```

`hall-host` 的 `dispatched:true` 只代表命令已传入 Unity，不代表业务成功。业务结果以 `OnCommandResult`、状态/测量事件及 `OnAbnormalEvent` 为准。当前命令应串行使用，尚无逐命令端到端 requestId 关联。

## 2. 网页调用 Unity

| 方法 | 参数 | 行为 |
|---|---|---|
| InitExperiment | JSON 字符串，见上例，全部字段显式传入 | 初始化新会话和案例范围；有通电、数据或演示时拒绝，须先 ResetExperiment |
| SetParameter | `{"name":"IS_mA","value":3}` | 参数控制，范围见下表 |
| GotoStep | `{"step":5}` | 只跳转面板，不自动接线，不跳过互锁，不宣称步骤已经完成 |
| ResetExperiment | 无参数 | 结束演示，关闭输出，清空接线和全部测量，新建 sessionId；保留案例范围 |
| FinishFromWeb | 无参数 | 至少一组完整测量且没有未完成方向时结束，关闭输出，锁定手动操作，回传结果 |
| RequestSnapshot | 无参数 | 回传当前完整组、状态、事件记录；不结束会话 |
| RecordMeasurement | 无参数 | 等价于学生点击记录当前方向，受同样的稳定性检查约束 |
| ConnectTerminals | `{"a":0,"b":6}` | 等价于两次点击外部端子，用于网页控制或联调，不绕过通电互锁 |

底层等价调用：`unityInstance.SendMessage('WebBridge', 'SetParameter', JSON.stringify({...}))`。GotoStep 统一为 JSON 字符串，不接受裸 int。

### 参数

| name | value 范围 | 单位/语义 |
|---|---|---|
| IS_mA | 0..maxIs_mA | mA，幅值；案例上限 ≤10 |
| IM_A | 0..maxIm_A | A，幅值；案例上限 ≤1 |
| isSwitch | −1/0/+1 | IS 刀闸反向/断开/正向 |
| imSwitch | −1/0/+1 | IM 刀闸反向/断开/正向 |
| measurementSwitch | −1/0/+1 | Vσ/断开/VH |
| power | 0/1 | 关闭/开启输出；开启要求电流设定为零且回路有效 |
| stageX、stageY | −1..+1 | 归一化机构位置；不改变物理模型磁场 |
| sweep | 0/1 | VH-IS / VH-IM；有未完成组时拒绝改变 |

初始两电流为零、输出关闭、刀闸 +1、样品位置零。电流案例上限最小分别为 0.15 mA、0.05 A。材料只接受 `n-silicon`，厚度只接受 0.5 mm；暂不支持自定义样品，不会静默忽略不支持的配置。

步骤：1 认识仪器、2 接线、3 检查、4 控制、5 采样、6 结束。`step` 是宿主选择的教学步骤号；没有用切换页面冒充学生完成步骤。判断完成条件应读取 state 与操作证据。

端子编号：0 IS+、1 IS−、2 V+、3 V−、4 IM+、5 IM− 为测试仪端；6..11 为对应实验箱端。标准线为 `(0,6)..(5,11)`。

## 3. 事件公共结构

```json
{
  "schemaVersion":1,
  "type":"OnMeasureData",
  "eventId":"<sessionId>:18",
  "sessionId":"<generated-id>",
  "caseId":"hall-basic",
  "timestamp":"2026-09-19T04:30:00.0000000Z",
  "elapsedMs":12340,
  "step":5,
  "code":"record",
  "detail":"",
  "success":true,
  "isDemo":false,
  "maxIs_mA":10,
  "maxIm_A":1,
  "state":{
    "powered":true,"circuitValid":true,"stable":true,"finished":false,
    "pendingDirections":1,"completeGroups":0,
    "IS_mA":3,"IM_A":0.4,"B_T":0.32,"rawVoltage_mV":-0.4
  }
}
```

数值仅为字段示例，不是给定电流条件的标定值。timestamp 为 UTC ISO 8601，elapsedMs 为从该会话开始的单调时钟毫秒数。用 eventId 去重。`isDemo:true` 的事件不能进入学生成绩；示例宿主不保存演示事件。

| type | 时机/字段 |
|---|---|
| OnReady | 仿真与桥接准备就绪 |
| OnCommandResult | 成功执行初始化或参数命令；code 为方法名 |
| OnMeasureData | 成功记录任一方向，附 measurement |
| OnOperationLog | 已接入的关键操作；code 为操作类别，detail 为参数/描述，success 表示操作是否被接受，不代表教学评分 |
| OnStepState | 导航、重置，以及回路有效性/通电/稳定/方向数/组数/结束状态发生改变 |
| OnAbnormalEvent | 操作拒绝或回路异常，code 为稳定代码，success=false |
| OnSnapshot | 完整组与事件历史快照，附 rows、history |
| OnExperimentComplete | 完成结果，附 rows、history、耗时、最终 state |

### measurement

| 字段 | 含义 |
|---|---|
| groupId | 四方向组 ID；重复记录同一槽位覆盖其读数，事件仍逐次保留 |
| timestamp | 该次采样 UTC 时间 |
| slot | 1..4，对应 V1..V4 |
| isDirection、imDirection、voltageDirection | 各为 +1/−1，明确方向，避免含糊的 `++` 字符串 |
| IS_mA、IM_A | 采样时实际电流幅值 |
| isSetpoint_mA、imSetpoint_A | 本组电流设定幅值 |
| B_T | 采样时有符号模型磁场 |
| rawVoltage_mV | 单方向仪表原始总电压 |
| groupComplete | 是否刚完成四方向组 |
| VH_mV、normalizedVH_mV | 仅 groupComplete=true 时有效；否则忽略默认零值 |
| row | 仅 groupComplete=true 时含完整组 |

槽位按 `(IS方向, IM方向)`：V1=(+,+)，V2=(+,−)，V3=(−,−)，V4=(−,+)。`VH=(V1−V2+V3−V4)/4`，归一值 `VH * voltageDirection`。

### rows 和 history

rows 使用现有 ExperimentRow 字段：id、seriesId、label、voltageDirection、primaryCurrent（IS mA）、secondaryCurrent（IM A）、v1..v4、vh（mV）、b1..b4（T）、is1..is4（mA）、im1..im4（A）。方向电流均保存幅值；方向由槽位决定。属性 `NormalizedHall` 不会被 JsonUtility 序列化，接收方用 `vh * voltageDirection` 计算。

history 为 **JSON 字符串数组**，使用 `history.map(JSON.parse)` 得到同一会话的非演示事件；不包含快照，避免递归膨胀。结果没有 Unity 评分，操作历史仅供 Web 端评价。会话 ID 由仿真生成，学生身份由宿主关联，不在 Unity 中保存登录凭据。

## 4. 异常代码

| code | 含义 |
|---|---|
| INVALID_INIT | JSON/版本/案例/材料/厚度/范围非法，或当前会话不允许初始化 |
| INVALID_PARAMETER | 网页参数名、值或业务条件不合法 |
| PARAMETER_OUT_OF_RANGE | 直接操作仪器超出案例电流范围 |
| INVALID_STEP / INVALID_CABLE | 步骤/端子参数非法 |
| CIRCUIT_SOURCE_SHORT | 电流源短路 |
| CIRCUIT_CROSS_CONNECTED | 三路电路跨接 |
| CIRCUIT_METER_SHORT | 电压输入短接 |
| CIRCUIT_INCOMPLETE | 开机时回路不完整 |
| POWER_ON_NONZERO_CURRENT | 开机时设定电流不为零 |
| LIVE_WIRING_CHANGE / LIVE_SWITCH_CHANGE | 带电改线/换向 |
| WIRING_REJECTED / CABLE_NOT_FOUND | 接线操作被拒绝/要删除的线不存在 |
| CIRCUIT_NOT_READY / HALL_MODE_REQUIRED | 采样回路未就绪/不是 VH 模式 |
| READING_UNSTABLE | 电流或磁场不稳定 |
| VOLTAGE_OVER_RANGE / CURRENT_TOO_LOW | 无效或超量程电压/采样电流过小 |
| GROUP_CONDITION_CHANGED | 四方向组内条件不一致 |
| EXPERIMENT_INCOMPLETE | 没有完整组或仍有未完成方向，拒绝结束 |
| SESSION_FINISHED | 已结束，需要重置后继续 |
| DEMO_ACTIVE | 演示期间拒绝网页修改学生实验 |

非预期接线可以保留，允许学生检查并纠错；异常代码并不保证一个动作只有一个事件，例如带电网页接线会经过两个端子点击。

## 5. 持久化与安全边界

WebGL 不再把有效数据“写入本地 CSV 成功”当作交付结果；数据保留内存并上报。示例 `hall-host.js` 将非演示事件尽力存入浏览器 localStorage，并支持 JSON 下载。缓存不等于数据库提交成功，不保证私密模式/配额不足/清理缓存后的保留，也不自动恢复 Unity 的接线和未完成实验。宿主应及时写入后端，明确保存结果和重试。

只选一种事件入口入库（DOM、命名回调或 iframe），否则会收到重复副本。iframe 限同源，并校验 source/origin；需要跨域时双方另定允许列表。所有仿真结果都来自学生客户端，不应作为可信成绩直接采信；成绩规则、鉴权、服务端校验和异常分析属于 Web 平台。

刷新会创建新会话；重置会清空当前 Unity 数据但浏览器示例保留已收到的事件。完成不自动关闭浏览器。用户应在退出前下载或确认平台保存。
