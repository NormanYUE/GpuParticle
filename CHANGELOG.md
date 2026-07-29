# Changelog / 更新日志

## 0.1.0 - 2026-07-28

### Changed

#### English

- Camera-facing Billboard, Stretched Billboard, and Mesh alignment paths now bake as camera-constrained geometry instead of being rejected immediately.
- Runtime geometry playback now checks the active camera against the baked camera profile and requests Native fallback when it does not match.
- Baker results now show failure context and message, not only the failure code.

#### 中文

- Billboard、Stretched Billboard 和摄像机朝向 Mesh 路径现在会烘焙为摄像机约束几何，不再直接拒绝。
- Runtime 几何回放现在会检查当前摄像机是否匹配烘焙摄像机 Profile；不匹配时请求 Native 回退。
- Baker 结果现在显示失败对象和详细说明，不再只显示 FailureCode。

### English

- Added precompiled Runtime and Editor assemblies.
- Added `GpuParticleClip`, `GpuParticleBinding`, `GpuParticlePlayer`, `GpuParticleHandle`, `GpuParticlePlayParams` and Native fallback APIs under `GpuParticle.Runtime`.
- Added safe payload header parsing with magic, schema, length, 16-byte alignment and CRC validation.
- Added Editor baker under `GpuParticle.Editor` with project settings, drag-and-drop baker window, menu commands, prefab binding writer and validation result model.
- Added geometry-track baking through Unity `ParticleSystemRenderer.BakeMesh` and `BakeTrailsMesh`.
- Added runtime geometry playback through SRP camera rendering callback using original materials.
- Preserved source GameObject layer, rejected non-default particle sorting layer/order for the current geometry path, and baked camera-facing Billboard modes through the camera-constrained geometry path.
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
- 保留源 GameObject Layer；当前几何路径会拒绝非默认粒子 Sorting Layer/Order，并通过摄像机约束几何路径烘焙摄像机朝向 Billboard 类模式。
- 新增基于源文件/依赖文件内容 Hash 与烘焙指纹的过期校验。
- 新增 Payload Header、Section Table 与烘焙几何轨道之间的 track-count 一致性校验。
- 新增独立 Trail 材质配方和多根 ParticleSystem 原生回退 catch-up。
- 对依赖运行时世界输入的粒子模块新增 Native 判定。
- 新增中英双语 README 和 Unity `package.json`。
