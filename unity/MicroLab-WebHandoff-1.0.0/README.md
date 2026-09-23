# 微观机理 Unity WebGL 源工程

这是霍尔效应微观机理演示的完整 Unity 源工程，使用 **Unity 6000.6.1f1** 打开。

## 场景与源码

- 主场景：`Assets/Scenes/HallEffectMicroscopicLab.unity`
- 备用场景：`Assets/Scenes/SampleScene.unity`
- 仿真模型：`Assets/Scripts/Physics/`
- 平台桥接：`Assets/Scripts/Platform/HallPlatformWebBridge.cs`
- WebGL 编辑器构建入口：`Assets/Editor/HallWebGLBuild.cs`
- WebGL 浏览器桥接：`Assets/Plugins/WebGL/HallPlatformBridge.jslib`
- 中文字体：`Assets/Resources/Fonts/NotoSansSC.otf`

## 本地打开与构建

1. 在 Unity Hub 中安装并选择 Unity `6000.6.1f1` 及 WebGL Build Support；Unity 2022 LTS 不能替代本工程的构建环境。
2. 用 Unity 打开本目录，等待资源导入完成。
3. 构建 WebGL：

```powershell
"C:\Program Files\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod HallEffectLab.EditorTools.HallWebGLBuild.Build -logFile -
```

构建输出位于 `BuildWebGL/Build/`。发布到平台时，将同一次构建产生的 `BuildWebGL/Build/BuildWebGL.*` 成套复制到平台的 `public/sim/case-microscopic/Build/`，并同步更新页面资源版本号；不要混用不同构建的 loader、data、framework 或 wasm 文件。独立构建不需要把 Unity 编辑器缓存上传到服务器。

## 模型约定

模型使用归一化教学单位，界面会明确显示坐标约定：相机位于世界坐标 `-Z`，`+Z` 指向屏内。`B = 0` 时横向洛伦兹力、霍尔场和电荷面均为零，并在力分析区显示对照组说明；演示达到 8 s 后显示“已完成”，点击重置才能重新开始。控制面板在窄屏下改为堆叠布局，粒子图例可折叠，移动端验收应同时检查无横向溢出。

浏览器验收至少覆盖加载完成事件与无 page error、B=0 对照状态、8 s 完成状态、重置、暂停/继续、速度档位、三条参数滑块、霍尔场/电子/电流切换、窄屏布局和退出/全屏按钮。该演示用于解释方向和受力关系，不提供真实材料标定或仪器不确定度结果。

## 源码归档范围

本目录只保留可复现工程所需的 `Assets`、`Packages` 和 `ProjectSettings`。Unity 生成的 `Library`、`Logs`、`Temp`、`UserSettings`、`BuildWebGL` 以及历史截图均不纳入源码归档；重新打开工程或构建时这些目录会自动生成。
