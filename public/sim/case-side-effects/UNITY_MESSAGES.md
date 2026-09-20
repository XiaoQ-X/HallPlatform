# 副效应与误差修正・Unity WebGL 接入消息格式

本目录 `public/sim/case-side-effects/` 是**独立案例目录**，不覆盖基础实验（`public/sim/index.html` 与 `public/sim/Build/`）。

当前 `index.html` 是**占位入口**：它复用平台通信层 `../hall-host.js`，用一个模拟设备走通完整事件流，**不包含、也不伪造 Unity 构建产物**。真实 Unity WebGL Build 就绪后，按本文档实现消息并替换占位面板，通信链路、事件格式与服务器保存逻辑保持不变。



***

## 1. 通信链路（与基础实验完全一致）



```
平台(SimLab.vue)  --postMessage(source:'hall-platform')-->  hall-host.js  --SendMessage('WebBridge', method, json)-->  Unity

Unity             --window.dispatchEvent(CustomEvent 'hall-event')-->        hall-host.js  --postMessage(source:'hall-unity')--> 平台
```



* Unity 侧 jslib 触发：`window.dispatchEvent(new CustomEvent('hall-event', { detail: event }))`

* 平台命令目标对象固定名为 `WebBridge`，方法名为下列入站命令，参数为 JSON 字符串。

* 事件持久化复用平台现有机制：平台把事件经 `src/sim-outbox.js` 批量 `POST /api/sim/events`，**无需另建日志系统**。



***

## 2. 入站命令（平台 → Unity）



| 方法                  | 用途       | 关键 payload                  |
| ------------------- | -------- | --------------------------- |
| `InitExperiment`    | 初始化案例    | 见下                          |
| `SetParameter`      | 调整参数     | `{name, value}`             |
| `RecordMeasurement` | 记录当前方向读数 | 无                           |
| `RequestSnapshot`   | 请求当前状态快照 | 无（回 `OnSnapshot`）           |
| `ResetExperiment`   | 清空重测     | 无                           |
| `FinishFromWeb`     | 结束并保存    | 无（回 `OnExperimentComplete`） |
| `GotoStep`          | 跳转步骤（可选） | 整数 1..6                     |
| `ConnectTerminals`  | 接线（可选）   | `{a, b}`，0..11              |

### InitExperiment payload



```
{

&#x20; "schemaVersion": 1,

&#x20; "caseId": "case-side-effects",

&#x20; "material": "n-silicon",

&#x20; "thickness\_mm": 0.5,

&#x20; "maxIs\_mA": 10,

&#x20; "maxIm\_A": 1

}
```

> 平台基础校验：
>
> `schemaVersion=1`
>
> 、
>
> `material=n-silicon`
>
> 、
>
> `thickness_mm=0.5`
>
> 、
>
> `maxIs_mA≤10`
>
> 、
>
> `maxIm_A≤1`
>
> 。不满足时回 
>
> `OnAbnormalEvent`
>
> ，
>
> `code="INVALID_INIT"`
>
> 。

### SetParameter 的 name/value



| name                                          | 单位        | 范围      | 说明                 |
| --------------------------------------------- | --------- | ------- | ------------------ |
| `IS_mA`                                       | mA        | 0..10   | 工作电流               |
| `IM_A`                                        | A         | 0..1    | 励磁电流（决定 B）         |
| `r0`                                          | Ω         | 0..0.5  | **新增**：电极偏移（不等位电阻） |
| `kE`                                          | mV/(mA·T) | 0..0.02 | **新增**：厄廷豪森系数（温差）  |
| `power`                                       | 0/1       | —       | 开关机                |
| `isSwitch` / `imSwitch` / `measurementSwitch` | 0/1       | —       | 电流 / 磁场 / 测量方向开关   |
| `sweep` / `stageX` / `stageY`                 | —         | —       | 扫描与平台（基础实验沿用）      |

> 基础实验的 
>
> `WebBridge.SetParameter`
>
>  目前只识别 
>
> `IS_mA/IM_A/开关/扫描`
>
> ；副效应构建需
>
> **新增对&#x20;**
>
> `r0`
>
> **、**
>
> `kE`
>
> **&#x20;两个 name 的处理**
>
> ，并把它们纳入读数合成。



***

## 3. 出站事件（Unity → 平台）

### 3.1 WebEvent 顶层结构



```
{

&#x20; "schemaVersion": 1,

&#x20; "step": 1,

&#x20; "type": "OnMeasureData",

&#x20; "eventId": "会话内唯一序号串",

&#x20; "sessionId": "平台会话ID（InitExperiment 后可回填）",

&#x20; "caseId": "case-side-effects",

&#x20; "timestamp": "2026-09-20T08:00:00.000Z",

&#x20; "elapsedMs": 12345,

&#x20; "code": "record",

&#x20; "detail": "",

&#x20; "success": true,

&#x20; "isDemo": false,

&#x20; "maxIs\_mA": 10,

&#x20; "maxIm\_A": 1,

&#x20; "state": { "...WebState..." },

&#x20; "measurement": { "...WebMeasurement..." }

}
```

### 3.2 type 枚举



| type                   | 时机                               | 平台处理                                                                 |
| ---------------------- | -------------------------------- | -------------------------------------------------------------------- |
| `OnReady`              | Unity 启动完成（对应 `WebBridge.Start`） | 平台建立会话、初始化                                                           |
| `OnCommandResult`      | 每条入站命令的回执                        | `code='InitExperiment'` 且 `success=true` 时关闭加载态                      |
| `OnMeasureData`        | 一次方向读数完成                         | 平台保存 measurement；`groupComplete=true` 合成一组                           |
| `OnSnapshot`           | 响应 `RequestSnapshot`             | 平台刷新状态（不入事件库）                                                        |
| `OnAbnormalEvent`      | 异常 / 非法操作                        | 平台记录异常（如 `CIRCUIT_NOT_READY`、`INVALID_INIT`、`EXPERIMENT_INCOMPLETE`） |
| `OnExperimentComplete` | 结束实验                             | 平台标记会话完成并 flush                                                      |

### 3.3 WebMeasurement（需新增 V0/VE 分项）

基础实验现有字段：



```
groupId, timestamp, slot, isDirection, imDirection, voltageDirection,

IS\_mA, IM\_A, isSetpoint\_mA, imSetpoint\_A, B\_T,

rawVoltage\_mV, VH\_mV, normalizedVH\_mV, groupComplete, row
```

**副效应构建需新增字段：**



```
{

&#x20; "V0\_mV": 0.250,

&#x20; "VE\_mV": 0.010

}
```

（如需完整教学分解，可再附 `VN_mV / VR_mV / noise_mV`，平台会原样保存在事件 payload 中。）

> 说明：
>
> `sim_measurements`
>
>  表
>
> **不新增列、不做迁移**
>
> 。V0/VE 分项随完整事件 JSON 保存在 
>
> `sim_events.payload`
>
> ；结束汇总保存在 
>
> `sim_sessions.state.report`
>
> 。

### 3.4 WebState（建议新增分项，便于实时显示）



```
{

&#x20; "powered": true,

&#x20; "circuitValid": true,

&#x20; "stable": true,

&#x20; "finished": false,

&#x20; "pendingDirections": 0,

&#x20; "completeGroups": 1,

&#x20; "IS\_mA": 5,

&#x20; "IM\_A": 0.8,

&#x20; "B\_T": 0.4,

&#x20; "rawVoltage\_mV": -0.828,

&#x20; "VH\_mV": -1.088,

&#x20; "V0\_mV": 0.250,

&#x20; "VE\_mV": 0.010

}
```

### 3.5 实验结束 report（放在完成事件的 `state.report`）

`OnExperimentComplete` 事件的 `state` 需携带：



```
{

&#x20; "report": {

&#x20;   "module": "side-effects",

&#x20;   "completedAt": "完成时间 ISO",

&#x20;   "params": { "IS\_mA": 5, "IM\_A": 0.8, "B\_T": 0.4, "RH": -2.7e-4, "d\_mm": 0.5, "r0": 0.05, "kE": 0.005 },

&#x20;   "forwardReadings": { "V1": -0.828, "V3": 1.068 },

&#x20;   "reverseReadings": { "V2": 1.308, "V4": -1.088 },

&#x20;   "readings": { "V1": -0.828, "V2": 1.308, "V3": 1.068, "V4": -1.088 },

&#x20;   "beforeCorrection\_mV": -0.828,

&#x20;   "correctedHall\_mV": -1.078,

&#x20;   "residualVE\_mV": 0.010,

&#x20;   "afterCorrection\_mV": -1.088,

&#x20;   "operations": \[ { "time": "…", "text": "关键操作" } ]

&#x20; }

}
```

平台会把该 `state`（含 report）整体写入 `sim_sessions.state`，从而保存：**参数快照、正向读数、反向读数、修正前结果、修正后结果、完成时间、关键操作记录**。



***

## 4. 单位与符号约定



* 电流：事件 / 命令统一 **mA（IS）**、**A（IM）**；电压统一 **mV**。

* 方向用整数 `+1 / -1`：`isDirection`（电流）、`imDirection`（磁场）、`voltageDirection`。

* 内部物理量用 SI；合成关系：


  * `VH = RH·I·B/d`

  * `V0 = r0·I`（与 B 无关，随电流换向变号）

  * `VE = kE·I·B`（随 I・B 换向，与 VH 同规律）

  * `Vmeasured = VH + V0 + VE`

* **四方向对称法** `(V1−V2+V3−V4)/4` 消除 V0（及固定零点），但 **VE 与 VH 换向规律相同、无法靠换向消除**，修正值 = VH + VE；只有已知 kE（或温差参数）才能减去 VE 得理想 VH。

### 四方向 slot 对照



| slot | 电流 | 磁场 | 名称 |
| ---- | -- | -- | -- |
| 1    | +  | +  | V1 |
| 2    | +  | −  | V2 |
| 3    | −  | −  | V3 |
| 4    | −  | +  | V4 |

服务器在 `groupComplete` 时按 `(raw1−raw2+raw3−raw4)/4` 合成，结果应与 Unity 的 `correctedHall` 一致。



***

## 5. 真实 Build 放置清单（待提供，当前缺失）

真实 Unity 副效应构建需输出到本目录，例如：



```
public/sim/case-side-effects/

&#x20; index.html                 # 改为加载真实 Unity（参照基础实验 index.html）

&#x20; Build/

&#x20;   WebGL.loader.js

&#x20;   WebGL.data.unityweb

&#x20;   WebGL.framework.js.unityweb

&#x20;   WebGL.wasm.unityweb
```

Unity 侧改动要点（C#）：



1. `WebBridge.cs`：给 `WebMeasurement` 增加 `V0_mV / VE_mV`（建议 `WebState` 同步增加分项）；`SetParameter` 支持 `r0 / kE`。

2. 读数合成接入副效应模型（不等位、厄廷豪森），并在 `RecordMeasurement` 输出分项。

3. 结束时在 `state.report` 输出第 3.5 节汇总。

4. 用 WebGL 构建入口 `HallLab.Editor.WebPlayerBuild.Build` 重新构建后放入上述目录。

**在真实 Build 提供前，不得在本目录放置或宣称已存在 Build 文件。**