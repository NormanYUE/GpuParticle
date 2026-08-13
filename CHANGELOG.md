# Changelog / 更新日志

## 0.2.0 - 2026-08-13

### English

- Added automatic VAT transform for project custom particle shaders: the baker now generates a `<name>_GpuVat` variant that inlines the original vertex/fragment code and rebuilds its input struct from VAT sampling, so GPU playback keeps custom shading with no user-side shader rewrite.
- Added Custom Data VAT channels (`_CustomData1Tex`/`_CustomData2Tex`) with per-particle per-frame float4 capture for `Custom1`/`Custom2` vertex streams.
- Vertex stream packing table covers Position, Center, Normal, Color, UV, AnimFrame, SizeX/XY/XYZ, Rotation, Rotation3D, Velocity, AgePercent and Custom1/2 scalar streams; unsupported streams fall back to Native playback.
- Surface shaders, Shader Graph assets, Horizontal/Vertical billboards and unparseable shader forms now report explicit failure codes (17-20) and fall back to Native playback.
- Added Compare Scene button in the baker window for manual A/B checks between native and GPU playback.

### 中文

- 新增项目内自定义粒子 Shader 的自动 VAT 变换：烘焙器生成 `<名字>_GpuVat` 变体，内联原 vertex/fragment 代码并以 VAT 采样重建输入结构体，GPU 回放保留自定义着色效果，用户无需改写 Shader。
- 新增 Custom Data VAT 通道（`_CustomData1Tex`/`_CustomData2Tex`），按粒子按帧捕获 `Custom1`/`Custom2` 顶点流的 float4 数据。
- 顶点流打包表覆盖 Position、Center、Normal、Color、UV、AnimFrame、SizeX/XY/XYZ、Rotation、Rotation3D、Velocity、AgePercent 与 Custom1/2 标量流；不支持的流自动回退原生播放。
- Surface Shader、Shader Graph、Horizontal/VerticalBillboard 与无法静态解析的写法现在会给出明确失败码（17-20）并回退原生播放。
- 烘焙窗口新增 Compare Scene 按钮，用于原生与 GPU 回放的手动 A/B 对比。

## 0.1.0 - 2026-07-28

### Changed

#### English

- Camera-facing Billboard, Stretched Billboard, and Mesh alignment paths now bake as camera-constrained geometry instead of being rejected immediately.
- Runtime geometry playback now checks the active camera against the baked camera profile and requests Native fallback when it does not match.
- Non-default particle sorting layer/order now bakes through the geometry path and is mapped to runtime renderer priority.
- Baker results now show failure context and message, not only the failure code.

#### 中文

- Billboard、Stretched Billboard 和摄像机朝向 Mesh 路径现在会烘焙为摄像机约束几何，不再直接拒绝。
- Runtime 几何回放现在会检查当前摄像机是否匹配烘焙摄像机 Profile；不匹配时请求 Native 回退。
- 非默认粒子 Sorting Layer/Order 现在会走几何烘焙路径，并映射为运行时 renderer priority。
- Baker 结果现在显示失败对象和详细说明，不再只显示 FailureCode。

### English

- Added precompiled Runtime and Editor assemblies.
- Added `GpuParticleClip`, `GpuParticleBinding`, `GpuParticlePlayer`, `GpuParticleHandle`, `GpuParticlePlayParams` and Native fallback APIs under `GpuParticle.Runtime`.
- Added safe payload header parsing with magic, schema, length, 16-byte alignment and CRC validation.
- Added Editor baker under `GpuParticle.Editor` with project settings, drag-and-drop baker window, menu commands, prefab binding writer and validation result model.
- Added geometry-track baking through Unity `ParticleSystemRenderer.BakeMesh` and `BakeTrailsMesh`.
- Added runtime geometry playback through SRP camera rendering callback using original materials.
- Preserved source GameObject layer, mapped non-default particle sorting layer/order to renderer priority for the geometry path, and baked camera-facing Billboard modes through the camera-constrained geometry path.
- Added stale bake validation through source/dependency file content hashes and bake fingerprints.
- Added track-count consistency checks between payload headers, section tables and baked geometry tracks.
- Added separate Trail material recipes and multi-root Native fallback catch-up.
- Added Native classification for unsupported runtime-world particle modules.
- Added bilingual README and Unity `package.json`.

### 中文

- 新增预编译 Runtime 与 Editor 程序集。
- 在 `GpuParticle.Runtime` 命名空间下新增 `GpuParticleClip`、`GpuParticleBinding`、`GpuParticlePlayer`、`GpuParticleHandle`、`GpuParticlePlayParams` 和原生回退 API。
- 新增 Payload Header 安全解析，覆盖 Magic、Schema、长度、16 字节对齐和 CRC 校验。
- 在 `GpuParticle.Editor` 命名空间下新增项目设置、拖拽烘焙窗口、菜单命令、Prefab Binding 写入和校验结果模型。
- 新增基于 Unity `ParticleSystemRenderer.BakeMesh` 与 `BakeTrailsMesh` 的几何轨道烘焙。
- 新增通过 SRP Camera Rendering 回调和原始材质绘制的运行时几何回放。
- 保留源 GameObject Layer；当前几何路径会把非默认粒子 Sorting Layer/Order 映射为 renderer priority，并通过摄像机约束几何路径烘焙摄像机朝向 Billboard 类模式。
- 新增基于源文件/依赖文件内容 Hash 与烘焙指纹的过期校验。
- 新增 Payload Header、Section Table 与烘焙几何轨道之间的 track-count 一致性校验。
- 新增独立 Trail 材质配方和多根 ParticleSystem 原生回退 catch-up。
- 对依赖运行时世界输入的粒子模块新增 Native 判定。
- 新增中英双语 README 和 Unity `package.json`。
