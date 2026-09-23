# HallCore Unity source

This directory is the reproducible source handoff for the Hall Effect core WebGL experiment.

- Unity editor: `2022.3.62f3c1`
- Scene: `Assets/THH/Scenes/THH_ReferenceWorkbench.unity`
- WebGL output consumed by the platform: `../../public/sim/Build/`
- Build entry point: `HallLab.Editor.WebPlayerBuild.Build`

Only source and project configuration are tracked here. Unity `Library`, `Logs`, `UserSettings`, generated `Builds`, and local caches are deliberately excluded. The deployed WebGL files are kept under `public/sim/Build` so the platform release is self-contained.

## Current web handoff behavior

- The workbench rejects duplicate terminal connections and caps external cables at six; the wiring panel exposes **撤销上一根接线** and reports the current cable count.
- IS and IM knob readouts show the live value and range (`IS` in mA, `IM` in A). The web host owns the exit/fullscreen toolbar and forwards `RequestExit`, `UndoLastCable`, `ConnectTerminals`, `SetParameter`, `RecordMeasurement`, and `FinishFromWeb` through `WebBridge`.
- Four-direction records are kept as raw readings and are synthesized by the teaching convention `VH = (V1 - V2 + V3 - V4) / 4`. The raw values include modelled offset and side-effect terms; `IM` is not a calibrated `B` value.

## Build and browser check

Use the exact Unity editor above with WebGL Build Support, then run `Tools/Build-WebGL.ps1` (or invoke `HallLab.Editor.WebPlayerBuild.Build` in batch mode). Keep the loader, data, framework and wasm files from one build together when copying to `public/sim/Build/`, and update the cache-busting version in `public/sim/index.html`. Before publishing, run the platform test suite, Vite production build and browser smoke test; manually check loading, power/interlock, duplicate-wire rejection, undo, live readouts, four-direction records, reset, fullscreen and exit in a desktop Chromium window.
