# Changelog / 更新日志

## 0.4.10 - 2026-08-17

### English

- Fixed Stretch Billboard looking unstretched and billboard particles rendering oversized on non-identity nodes. VAT corner math ignored `ParticleSystemRenderer.pivot` (e.g. sparks with `pivot.y = 0.5`) so stretch quads stayed centered instead of anchored, and billboard/stretch sizes were applied in world space without the instance transform's hierarchy/local scale (e.g. a 0.53-scale child looked ~1.9x too large). The bake now writes `renderer.pivot` onto `_ParticlePivot`, and VAT shaders (built-in + custom variants) apply pivot offsets plus `GpuParticleInstanceScale(localToWorld)`. Mesh mode is unchanged (it already multiplies through `localToWorld`). Re-bake required for existing clips so materials pick up the pivot.

### 中文

- 修复 Stretch Billboard 看起来没有拉伸、以及非恒等节点上 Billboard 粒子偏大的问题：VAT 角点数学之前忽略 `ParticleSystemRenderer.pivot`（例如火花 `pivot.y = 0.5`），拉伸四边形始终居中而非锚定；Billboard/Stretch 的 size 在世界空间应用时也不乘实例层级/本地缩放（例如 scale≈0.53 的子节点会偏大约 1.9 倍）。烘焙现在把 `renderer.pivot` 写入 `_ParticlePivot`，VAT Shader（内置与自定义变体）应用 pivot 偏移并乘 `GpuParticleInstanceScale(localToWorld)`。Mesh 模式不变（本身已走 `localToWorld`）。已有 clip 需要重新烘焙，材质才会带上 pivot。

## 0.4.9 - 2026-08-14

### English

- Fixed wrong texture-sheet tiles in GPU playback. The bake stored the continuous sheet frame value and the VAT shader rounded it after interpolation, so particles with a fractional start frame (e.g. Two Constants 0–1) could round up to the next tile — on smoke-like sheets with mixed soft/hard tiles this showed up as darker, harder, seemingly larger particles. The bake now stores the floored integer tile (matching native truncation) and the shader floors after interpolation, so the tile stays stable until the next sampled frame.

### 中文

- 修复 GPU 播放中序列帧 tile 选错的问题：烘焙之前保存连续帧值，着色器插值后四舍五入——随机起始帧为小数（如 Two Constants 0–1）的粒子会被进位到下一帧 tile。对软硬 tile 混合的烟雾图集，表现为部分粒子更黑、更硬、看起来更大。烘焙现在保存向下取整的整数 tile（与原生截断一致），着色器插值后同样向下取整，tile 在下一个采样帧之前保持稳定。

## 0.4.8 - 2026-08-14

### English

- Fixed mis-positioned and oversized particles for local-simulation systems whose node is not at the identity transform. Particle positions, velocities and rotations are reported in simulation space, but the bake always inverse-transformed them as if they were world-space — for local-sim systems that silently dropped the system node's own offset/scale/rotation at render time (e.g. a scaled-down, rotated cone emitter rendered ~1.9x larger and unrotated). The bake now branches on `simulationSpace`: local-sim states are captured as-is, world-sim states keep the inverse transform. Systems using Custom simulation space now fall back to Native instead of rendering wrong.

### 中文

- 修复节点不在恒等变换处的本地模拟粒子位置偏移、尺寸偏大的问题：粒子的位置/速度/旋转按模拟空间上报，但烘焙之前一律按世界空间做逆变换——对本地模拟系统，这会在渲染时静默丢掉系统节点自身的位移/缩放/旋转（例如一个缩小并旋转的锥形发射器被放大 ~1.9 倍且不旋转地渲染出来）。烘焙现在按 `simulationSpace` 分支：本地模拟状态原样采集，世界模拟状态保持逆变换。使用 Custom 模拟空间的系统现在回退原生，而不是渲染错误。

## 0.4.7 - 2026-08-14

### English

- Fixed looping particles going almost invisible after the first replay. 0.4.6 wrapped at the emission period but then sampled one period into the timeline; the bake only captures a single emission cycle, so that window holds no live particles and every short-cycle system rendered nothing. Looping now wraps in place at `LoopPeriod`. Known limitation: particles still alive at the wrap point are cut instead of overlapping into the next cycle like native looping.

### 中文

- 修复循环粒子在第一次重播后几乎不可见的问题：0.4.6 按发射周期回绕后把采样时间偏移了一个周期，但烘焙只采集了一个发射周期，偏移窗口内没有活粒子，短周期系统全部渲空。循环现在按 `LoopPeriod` 原位回绕。已知限制：回绕点仍存活的粒子会被截断，不会像原生循环那样与下一周期重叠。

## 0.4.6 - 2026-08-14

### English

- Fixed looping playback wrapping at the wrong period. Looping used to wrap at the full clip duration — the group-wide sampled timeline including the trailing lifetime of the last particles (e.g. 5.34s for a prefab whose own emission cycle is 1s) — so between replays there was a long dead window that native looping does not have. Each clip now records its source system's emission cycle (`startDelay + duration`) as `LoopPeriod`, and looping wraps there instead. When the baked timeline covers at least two cycles, sampling shifts one period in so leftovers from the previous cycle finish their lifetime, matching native loop overlap. When the timeline is shorter than two cycles the wrap falls back to in-place modulo (the tail is cut), and legacy clips without a loop period keep the old duration-based wrap.

### 中文

- 修复循环播放回绕周期错误的问题：之前循环按完整 clip 时长回绕——即包含最后粒子存活尾巴的组级采样时间轴（例如自身发射周期仅 1s 的 prefab 被按 5.34s 回绕）——导致两次重播之间出现原生循环没有的长空窗。现在每个 clip 记录源系统的发射周期（`startDelay + duration`）为 `LoopPeriod`，循环按该周期回绕；当烘焙时间轴覆盖 ≥2 个周期时，采样时间偏移一个周期，让上一周期残留粒子走完寿命，与原生循环的重叠行为一致。时间轴不足两个周期时退化为原位取模（尾巴被截断），无 loopPeriod 的旧资产保持原有的按时长回绕。

## 0.4.5 - 2026-08-14

### English

- Fixed texture sheet animation rendering the whole atlas in custom-shader variants: the UV/UV2 vertex streams were filled with the raw quad corner UV, so user fragment code sampling `_MainTex` saw the entire sheet instead of the current tile. Both streams now carry the current-frame tile UV via `GpuParticleApplyTextureSheet` (a no-op when sheet animation is off), matching the native UV stream contract. Frame blending is still not reproduced (UV2 gets the current tile, not the next one).

### 中文

- 修复自定义 Shader 变体把序列帧图集整表渲染出来的问题：UV/UV2 顶点流之前填的是原始角点 UV，用户 frag 直接采样 `_MainTex` 时看到的是整张图集而非当前帧 tile。两个流现在通过 `GpuParticleApplyTextureSheet` 携带当前帧 tile UV（无序列帧时为恒等变换），与原生 UV 流契约一致。序列帧混合（frame blending）仍不还原（UV2 填当前帧而非下一帧）。

## 0.4.4 - 2026-08-14

### English

- Fixed wrong corner math for particle systems that share one custom shader with different render modes. The transform inlines render-mode corner math into the generated variant, but all systems wrote to the same `<ShaderName>_GpuVat.shader` file — the last write won, so Horizontal/Vertical/Stretched systems sharing a shader with Billboard ones rendered camera-facing. Variants are now cached and named per (shader, render mode, vertex streams, custom data) combination: Billboard keeps `_GpuVat`, other modes get `_H`/`_V`/`_S`/`_M` suffixes, and stream-set collisions get a numeric suffix.

### 中文

- 修复共享同一自定义 Shader 但渲染模式不同的系统角点数学错误的问题：变换把渲染模式的角点数学内联进变体，但所有系统都写同一个 `<ShaderName>_GpuVat.shader` 文件——最后写入的覆盖其余，导致与 Billboard 系统共享 Shader 的水平/垂直/拉伸系统被渲成面向相机。变体现在按 (Shader, 渲染模式, 顶点流, CustomData) 组合缓存与命名：Billboard 保持 `_GpuVat`，其余模式加 `_H`/`_V`/`_S`/`_M` 后缀，流集合冲突追加数字后缀。

## 0.4.3 - 2026-08-14

### English

- Fixed stale Native bindings surviving a successful bake. A failed bake writes a `Native` marker binding onto the source prefab root, but a later successful bake never refreshed it, and the runtime prefab builder copied that stale binding (with its particle references stripped to null) onto the runtime prefab root — so root-level readiness probes like `GetComponentInChildren<GpuParticleBinding>` reported `status=Native, clip=null` even though every system baked successfully. Successful bakes now refresh the source prefab binding to `GpuReady`, the runtime prefab builder strips copied bake artifacts before writing fresh per-system bindings, and group bakes add a summary binding on the runtime prefab root.

### 中文

- 修复陈旧 Native 标记在成功烘焙后残留的问题：失败的烘焙会在源 Prefab 根节点写入 Native 状态的 `GpuParticleBinding`，而之后成功的烘焙不会刷新它；同时运行时 Prefab 构建器会把这份陈旧 binding（其粒子引用已被剥离为空）一并拷到运行时 Prefab 根节点——导致 `GetComponentInChildren<GpuParticleBinding>` 这类根级就绪探测误报 `status=Native, clip=null`，即使所有系统都已成功烘焙。现在成功烘焙会把源 Prefab 的 binding 刷新为 `GpuReady`，运行时 Prefab 构建器在写入各系统新 binding 前会剥离拷贝来的旧烘焙组件，组烘焙还会在运行时 Prefab 根节点补一个汇总 binding。

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
