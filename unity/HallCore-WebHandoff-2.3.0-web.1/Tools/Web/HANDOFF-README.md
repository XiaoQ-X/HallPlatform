# 霍尔效应仿真核心交接包

版本：2.3.0-web.1。请把整个 ZIP 交给接手开发者。

| 目录 | 用途 |
|---|---|
| WebGL | 可部署网页产物；index.html 为实验，handoff.html 为联调页 |
| Source | Unity 源码工程；用 Unity 2022.3.62f3c1 打开 |
| Docs | 操作、参数、通信协议与接手说明 |
| Tools/Web/serve.cjs | 本地静态预览服务器，需要 Node.js 18+ |
| Validation | 验证结果、截图、四方向操作录像 |
| manifest-sha256.json | 包内文件 SHA-256 与大小，可用于传输后校验 |

## 快速启动

在本目录打开终端：

```text
node Tools/Web/serve.cjs WebGL 8090
```

访问 http://127.0.0.1:8090/handoff.html 。不要直接双击 HTML 文件。

先读 `Docs/Web交接-接手说明.md`，再读 `Docs/Web交接-通信协议-v1.md`。接口事件由 Unity 发出，正式平台的数据保存、分析和评分由接手方实现。

重要边界：样品固定 N 型硅、0.5 mm；没有真实仪表调零与预热；内置管线而非 URP。当前面向桌面鼠标浏览器。详细限制和验证范围见 Docs 与 Validation，不把这些未实现能力作为已交付功能。

Windows 2.3.0 安装器是此前版本，不含本次网页桥接改动，因此未作为本交接包的运行入口。
