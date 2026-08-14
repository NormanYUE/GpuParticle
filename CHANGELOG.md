# Changelog / 更新日志

## 0.4.2 - 2026-08-14

### English

- Fixed spurious `Material ... doesn't have a texture property '_BaseMap'` errors during baking: the `_BaseMap` → `_MainTex` texture bridge in material creation now checks `HasProperty` first, so shaders without either slot stay silent.

### 中文

- 修复烘焙时出现的 `Material ... doesn't have a texture property '_BaseMap'` 报错：材质创建中的 `_BaseMap` → `_MainTex` 纹理桥接现在先检查 `HasProperty`，没有对应属性的 Shader 不再报错。

## 0.4.1 - 2026-08-14

### English

- Fixed `'tex2D': no matching 2 parameter intrinsic function` on Metal when the source shader declares `sampler2D _MainTex`: `_MainTex` is no longer declared by the shared `GpuParticleVatInput.hlsl` nor stripped from user code, so the user's own declaration and sampling calls always stay type-consistent. The built-in VAT shaders (Billboard/Stretch/Mesh/CustomExample) now declare `TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);` themselves.

### 中文

- 修复源 Shader 以 `sampler2D _MainTex` 声明时 Metal 报 `'tex2D': no matching 2 parameter intrinsic function` 的问题：共享头文件 `GpuParticleVatInput.hlsl` 不再声明 `_MainTex`，变换器也不再剥离用户声明，用户声明与采样调用始终保持类型自洽。内置 VAT Shader（Billboard/Stretch/Mesh/CustomExample）改为各自显式声明 `TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);`。

## 0.4.0 - 2026-08-14

### English

- The `UV2` vertex stream is now supported for procedural billboard-family render modes (Billboard/Stretched/Horizontal/Vertical). Native `BakeMesh` probing showed these particles fill UV2 with a copy of the quad UV (next sheet tile when frame blending is on), so variants fill it with the same expression as the UV stream. This also keeps `Custom1`/`Custom2` aligned on `TEXCOORD1`/`TEXCOORD2` when UV2 pads `TEXCOORD0.zw`, matching the native packing. Mesh render mode with UV2 still falls back to Native (the VAT mesh's second UV channel carries the particle index).

### 中文

- `UV2` 顶点流现在支持程序化 billboard 家族渲染模式（Billboard/Stretched/Horizontal/Vertical）。经原生 `BakeMesh` 探针实证，这类粒子的 UV2 流是角点 UV 的翻版（序列帧混合开启时为下一帧 tile uv），变体以与 UV 流相同的表达式填充。UV2 占住 `TEXCOORD0.zw` 后，`Custom1`/`Custom2` 在 `TEXCOORD1`/`TEXCOORD2` 上与原生打包严格对齐。Mesh 模式使用 UV2 仍回退原生（VAT 网格的第二 UV 通道用于粒子索引）。

## 0.3.2 - 2026-08-14

### English

- Generated VAT shader variants now use consistent LF line endings: CRLF source shader bodies inlined into the variant are normalized at transform entry, so Unity no longer warns about inconsistent line endings on import.

### 中文

- 生成的 VAT 变体 Shader 现在统一使用 LF 行尾：CRLF 保存的源 Shader 函数体在变换入口归一化，Unity 导入时不再出现 mixed line endings 警告。

## 0.3.1 - 2026-08-13

### English

- Fixed `HLSLcc: Duplicate constant buffer declaration: UnityPerMaterial` on Metal/GLES: the shared `GpuParticleVatInput.hlsl` cbuffer is now named `GpuParticleVat`, leaving `UnityPerMaterial` to user shaders, the example shader, and generated variants.

### 中文

- 修复 Metal/GLES 上的 `HLSLcc: Duplicate constant buffer declaration: UnityPerMaterial`：共享头文件 `GpuParticleVatInput.hlsl` 的 cbuffer 改名为 `GpuParticleVat`，把 `UnityPerMaterial` 留给用户 shader、示例 shader 与生成变体。

## 0.3.0 - 2026-08-13

### English

- Extended the custom shader VAT transform to `CGPROGRAM` and `HLSLINCLUDE` shader forms: CGPROGRAM sources now generate UnityCG-flavored variants (`sampler2D`/`tex2Dlod` via a dual-flavor `GpuParticleVatInput.hlsl`), and shared `HLSLINCLUDE` blocks recover their pass state from the following `Pass` body.
- Horizontal/Vertical billboard render modes are now supported by the transform (corner math mirrors the built-in VAT billboard shader) instead of falling back to Native.
- Input struct parsing now tolerates `#if/#elif/#else` conditional fields (guards are replayed onto the fill statements), `UNITY_VERTEX_INPUT_INSTANCE_ID` macro lines, and unknown semantics such as `TANGENT` (zero-filled, matching unfed native attributes).
- Fixed TEXCOORD packing in generated wrappers: Position/Normal/Color occupy their own semantic slots instead of TEXCOORD space, and out-of-range components are zero-padded.
- Generated variants now ship with a sibling copy of `GpuParticleVatInput.hlsl`, and relative `#include` directives in source shaders are rewritten to project-rooted paths so variants compile from the generated folder.
- Fixed malformed `.meta` GUIDs for `GpuParticleVatInput.hlsl` and `GpuParticleVatCustomExample.shader` that Unity's YAML parser rejected.

### 中文

- 自定义 Shader VAT 变换扩展支持 `CGPROGRAM` 与 `HLSLINCLUDE` 形态：CGPROGRAM 来源生成 UnityCG 风格变体（`GpuParticleVatInput.hlsl` 双 flavor 宏提供 `sampler2D`/`tex2Dlod`），共享 `HLSLINCLUDE` 块会从随后的 `Pass` 体补抓 Pass 状态。
- Horizontal/VerticalBillboard 渲染模式现在支持自动变换（角点数学镜像内置 VAT Billboard Shader），不再回退原生。
- 输入结构体解析容忍 `#if/#elif/#else` 条件字段（guard 原样回放到填充语句）、`UNITY_VERTEX_INPUT_INSTANCE_ID` 宏行与未知语义字段（如 `TANGENT`，零填充，与原生未喂属性一致）。
- 修复生成 wrapper 的 TEXCOORD 打包：Position/Normal/Color 占独立语义槽、不再占用 TEXCOORD 空间，越界分量零填充。
- 生成变体现在会随带一份 `GpuParticleVatInput.hlsl` 拷贝，源 Shader 的相对 `#include` 会重写为项目根路径，保证变体在生成目录下可编译。
- 修复 `GpuParticleVatInput.hlsl` 与 `GpuParticleVatCustomExample.shader` 的 `.meta` GUID 格式问题（Unity YAML 解析器无法识别）。

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
