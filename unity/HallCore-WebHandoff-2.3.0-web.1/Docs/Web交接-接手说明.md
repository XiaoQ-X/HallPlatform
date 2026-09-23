# 霍尔效应仿真核心接手说明

## 交付范围

Unity 仿真核心和网页桥接、WebGL 输出、独立联调页及维护源码。资源中心、数据分析工坊、互评、教师端、账号、数据库和评分由仓库根目录的平台实现；本目录只描述 Unity 核心及其桥接交接，不重复维护这些业务模块。

版本为 2.3.0-web.1，基于 Windows 2.3.0；Unity 精确版本 2022.3.62f3c1，内置渲染管线，非 URP。打开源码包根目录为 Unity 工程，编辑场景为 `Assets/THH/Scenes/THH_ReferenceWorkbench.unity`。

## 先体验，再集成

1. 安装 Node.js 18 或更新版本（仅本地预览服务器使用，不是网站运行依赖）。
2. 在交接包根目录运行 `node Tools/Web/serve.cjs WebGL 8090`。
3. 浏览器访问 `http://127.0.0.1:8090/handoff.html`。开发者页可运行四方向联调、下载 JSON；`index.html` 为学生实验画面。
4. 阅读通信协议，在自己的同源网页中嵌入 `index.html`，订阅事件，再接数据库。联调页只是开发示例，不是教学资源平台。

从完整源码工程预览时使用 `node Tools/Web/serve.cjs Builds/WebGL 8090`。

标准实验：逐根连接同名 IS+/−、V+/−、IM+/−，检查回路，电流设定归零，POWER 开机，设电流，等待稳定，采样；归零关机后换向，恢复同一组幅值再采样，完成四个方向。完整步骤见配套清单。

## 重新构建

安装指定 Unity 版本及同版本 WebGL Build Support，然后运行：

```powershell
./Tools/Build-WebGL.ps1 -UnityEditor '你的Unity目录/Editor/Unity.exe'
```

输出为 `Builds/WebGL/`。也可在 Unity 菜单 `TH-H/Build WebGL handoff` 构建。构建脚本会配置 WebGL 压缩、模板和内存，切换平台，可能更新 ProjectSettings；不要将编辑器缓存 Library 作为交接依赖。常驻对象 WebBridge 在运行时创建，不需要手动拖入场景。

构建会先运行 27 项模型和序列化断言。重跑浏览器验收可在源码根目录执行 `npm --prefix Tools/Web install`，启动上述 8090 预览服务器，再执行 `node Tools/Web/test-browser.cjs`。脚本默认使用已安装的 Windows Chrome；可通过 HALL_BROWSER 指定其他 Chromium 浏览器。没有系统 Chrome 时需先安装 Playwright Chromium。依赖只用于测试，不进入网页产物。重新生成交接 ZIP 使用 PowerShell 7 运行 `Tools/Package-WebHandoff.ps1`。

## 网站部署

原样上传 WebGL 整个目录，保留 `index.html`、`Build/`、`hall-host.js` 和许可文件。联调页 `handoff.html` 可仅部署在开发环境。建议与资源中心同源。

当前采用 Brotli 压缩并启用 Unity decompression fallback，产物为 `.unityweb` 时由加载器解压，可直接用普通静态服务器部署。**不要给 `.unityweb` 文件错误添加 `Content-Encoding: br`**。若后续禁用 fallback 改为 `.br`，才需按文件配置 Brotli 编码和正确的 JavaScript/wasm MIME。生产建议 HTTPS、版本化目录、合理缓存，更新时保持同一构建的 loader/data/framework/wasm 文件成套一致。

加载阶段由 HTML 进度条显示；当前单个实验场景，未使用 Addressables 分包或单独 Unity 加载场景。先依据实测包体和首屏时间决定是否分包，不宣称已经实现分包。

## 已知边界

- 样品固定 N 型硅、厚度 0.5 mm；可配置教学案例电流上限，不支持任意材料/厚度。接口明确拒绝不支持的样品配置。
- 已有电流归零和通断电互锁，未模拟真实仪表零点校准与预热。SIM 饰件不是调零旋钮。
- 磁场由模型计算，样品 X/Y 只演示机构位置。未做实机标定，不能用于实机不确定度验收。
- Unity 网页版负责采样、四方向合成与事件证据，数据分析按钮改为快照上报。原桌面分析代码保留在源码供维护，不作为 Web 平台分析功能。
- 浏览器缓存为示例保底，不替代后端可靠保存。当前没有离线实验续做、上传确认/重试协议或自动恢复接线的实现。
- 操作录屏是交接示例；生产平台的学生录屏采集、上传及互评归对方开发。
- 面向桌面鼠标浏览器；建议实验视区至少 1280×800。手机可加载和缩放画面，但没有承诺适合触屏完成复杂接线。
- 未实现可信评分、登录和远程步骤自动完成。GotoStep 仅导航，不能绕过安全互锁。

以上是本次明确支持的可交接核心范围。若最终课程验收要求可变样品、真实调零或其他未支持能力，需要后续增补，不应误标为已完成。

## 文件与来源

源码打包仅包含 Assets、Packages、ProjectSettings、Tools、Docs、说明和许可证；排除 Library、历史 Builds、个人 Logs、Backups、参考资料原件、Git 历史和本机工具缓存。Noto Sans SC 中文字体随包提供，SIL OFL 许可见 Assets/ThirdParty/NotoSansSC。

交接包中的 Validation 保存本次构建、浏览器检查结果、截图及操作录像；实际验证结论以其中结果为准。原 Windows 安装器仅供历史行为参考，不代表本次新增桥接已包含于旧安装器。
