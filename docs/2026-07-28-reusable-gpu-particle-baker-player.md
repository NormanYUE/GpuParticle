# 通用 GPU 粒子烘焙与播放工具 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一个可跨 Unity 项目复用的一键式 GPU 粒子烘焙与播放 UPM 包，并在 DigitDoor 中无侵入接入现有技能 VFX 链路；转换结果只对外显示“GPU 可用”或“保留原生”，任何硬性表现失败都自动保留 Unity `ParticleSystem`。

**Architecture:** 包内采用“离线执行 Unity 原粒子并采样最终结果，运行时 GPU 回放”的路线。标准 Billboard、Stretched Billboard 和 Mesh 粒子优先烘焙为状态轨道，由 Compute Shader 批量插值、剔除和绘制；Shader 已验证但因 Trail、复杂顶点流或拓扑无法状态化的轨道，在严格摄像机约束下烘焙为逐帧几何轨道；任何无法通过硬失败校验的 Prefab 整体保留原生播放。DigitDoor 只在 `EffectController` 下增加粒子播放后端，保留现有配置、对象池、GPUI 模型、跟随、停止和技能生命周期语义。

**Tech Stack:** Unity 2022.3.62f2、C#、URP 14.0.12、Compute Shader、`GraphicsBuffer`、Indirect/Instanced Draw、Unity Editor Preview Scene、Unity `ParticleSystem` 采样 API、Android Vulkan/GLES3、现有 DigitDoor `EffectController`/`EffectRes`/GameObjectPool 链路。

## Global Constraints

- 通用实现必须位于 `Packages/com.hlwd.gpu-particle`，不得依赖 DigitDoor 业务代码、Ember、YooAsset、Luban、GPU Instancer Pro 或 `Packages/Hlwd-Framework`。
- 依赖方向只能是 `DigitDoor -> Hlwd.GpuParticle`；包内禁止出现 `Hlwd.Game`、`GameEntity`、`EffectRes`、`SkillLifecycleVFX` 等项目符号。
- 不修改 `Packages/Hlwd-Framework`、GPU Instancer Pro 包、Ember 包和配置生成器。
- 不改变 `EffectRes`、`Data_SkillLifecycleVFX`、技能配置表及其资源路径语义。
- 源 Prefab 的粒子模块、材质和层级不被改写；默认只允许增加或更新一个 `GpuParticleBinding`。无法写入的只读 Prefab 生成 Prefab Variant。
- 首版不做包体压缩或数值量化。位置、速度、旋转、大小、颜色、自定义数据均使用 32 位数据，先保证稳定和表现。
- 首版正式支持 Unity 2022.3、URP 14、Editor、Android Vulkan/GLES3。Built-in/HDRP 只保留扩展接口，不纳入首版完成标准。
- 首版覆盖常见 Billboard、Stretched Billboard、Mesh、Texture Sheet Animation、Custom Data 和 Trail；动态物理碰撞、Trigger、脚本逐帧修改模块、不可预测的 World Space Trail 等场景可直接判定“保留原生”。
- 对用户只暴露二态结果：`GPU 可用`、`保留原生`。状态轨道、几何轨道、材质适配策略只作为内部诊断信息，不形成质量档位。
- 不允许静默丢失子粒子、爆炸、闪光、主要 Trail 或附加节点。运行时发现平台、数据或 Shader 不满足条件时，必须自动回退原生。
- 不新增或修改 DigitDoor 自动化测试文件。验证通过包内校验工具、示例场景、Editor Play Mode、压力场景和 Android 真机完成。
- 保留当前工作区中与本计划无关的改动，尤其不触碰：
  - `Assets/Scripts_Hotfix/Game/Battle/Systems/SkillGraph/SkillGraphCleanupSystem.cs`
  - `Assets/Scripts_Hotfix/Game/Battle/Systems/SkillGraph/SkillHitDetectionSystem.cs`
- 每个任务完成后先执行该任务的验证，再提交；不得把编译失败、未运行真机或仅静态检查描述为“已完成”。

---

## 1. 完成标准

### 1.1 “GPU 可用”的判定

一个 Prefab 只有同时满足以下条件，才写入 `GpuParticleBakeStatus.GpuReady`：

1. 所有启用的 `ParticleSystem` 和 `ParticleSystemRenderer` 都已被采样并映射到运行时轨道。
2. 所有实际可见的子发射器、Trail 和 Mesh 粒子都有对应输出。
3. 所用 Shader/材质可由标准适配器、项目扩展适配器或几何回放路径正确渲染。
4. 原版与 GPU 版在关键帧比较中不存在本文定义的硬失败。
5. `Play`、循环、暂停、恢复、停止、倍速和对象池复用语义一致。
6. 当前平台支持该 Clip 的 GPU 能力；不支持时运行时能够原生回退。

任一条件不满足，整个 Prefab 标记为 `GpuParticleBakeStatus.Native`，原始 `ParticleSystem` 保持可播放。

### 1.2 允许的表现差异

以下差异不会单独造成烘焙失败：

- 随机粒子的精确分布、数量、Noise、颜色或透明度存在轻微差异。
- Trail 曲线细分数量降低，但主体连续且没有明显断裂。
- 同材质内部粒子排序有轻微变化。
- Blend、亮度存在轻微差异，但没有黑块、粉色材质或主体消失。
- 生命周期事件出现 1 至 2 个采样帧的偏移。

### 1.3 硬失败

以下任一情况必须标记为 `Native`，禁止启用 GPU 路径：

- 整个子粒子、弹体、爆炸、闪光或主要 Trail 缺失。
- 位置、方向、缩放、跟随目标或世界/本地空间关系明显错误。
- `Play`、循环、停止、对象池复用、暂停恢复或 `timeScale` 语义错误。
- 材质变粉、变黑，顶点严重拉伸，或出现持续闪烁。
- Android 包内不可见、Shader/Compute 报错或 Graphics API 不支持。
- GPU 表现改变技能命中、伤害、生命周期或其他战斗逻辑。
- 结果已经不能被识别为原技能特效。

### 1.4 首批验收资源

- `Assets/ResBundles/EffectRes/SkillEntity_BulletHit.prefab`
- `Assets/ResBundles/EffectRes/SkillEntity_GrenadeExplosion.prefab`
- `Assets/ResBundles/EffectRes/SkillEntity_Rocket.prefab`

这三个资源用于覆盖受击、爆炸、移动弹体三类常见技能表现。它们不是包内硬编码名单。

---

## 2. 总体数据流

```text
Project Prefab
  -> GpuParticleAnalyzer
  -> isolated Preview Scene
  -> deterministic ParticleSystem sampling
  -> state tracks / geometry tracks
  -> binary payload + GpuParticleClip.asset
  -> hard-failure validation
  -> GpuParticleBinding on source prefab

Runtime Prefab Instance
  -> GpuParticlePlayer.Play()
  -> GpuParticleWorld batch registration
  -> shared clip buffers + per-instance transform/time
  -> Compute interpolation/culling/sorting
  -> indirect/instanced rendering
  -> Stop/Hide releases instance handle
  -> unsupported/stale/error => original ParticleSystem fallback
```

DigitDoor 中保持以下业务链不变：

```text
Data_SkillLifecycleVFX
  -> SkillQuery
  -> SkillCtrl.EmitSkillLifecycleVFXConfigs
  -> BattleEffectRequest / BattleAttachedEffectRequest
  -> PlayBattleEffectRequestSystem / BattleAttachedEffectSystem
  -> GameEntity.Effect.Play
  -> EffectRes
  -> GameObjectPool
  -> EffectController
  -> ParticleSystemEffectBackend or GpuParticleEffectBackend
```

---

## 3. 文件地图

### 3.1 新增 UPM 包

| 文件 | 职责 |
|---|---|
| `Packages/com.hlwd.gpu-particle/package.json` | 包名、版本、Unity 版本和依赖声明 |
| `Packages/com.hlwd.gpu-particle/README.md` | 快速安装和一分钟工作流 |
| `Packages/com.hlwd.gpu-particle/CHANGELOG.md` | 版本变更 |
| `Packages/com.hlwd.gpu-particle/Runtime/Hlwd.GpuParticle.Runtime.asmdef` | 运行时程序集 |
| `Packages/com.hlwd.gpu-particle/Editor/Hlwd.GpuParticle.Editor.asmdef` | Editor 程序集，引用 Runtime 与 URP Editor API |
| `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleTypes.cs` | 枚举、序列化结构、能力位 |
| `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleClip.cs` | Clip 元数据、资源引用、二进制数据引用 |
| `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleBlobReader.cs` | 校验并读取二进制 Payload |
| `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleRendererRecipe.cs` | Alignment、顶点流、排序、材质和 Seed Variant 等完整渲染配方 |
| `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleRuntimeResources.cs` | Compute、播放 Shader、默认 Mesh 和 Shader Variant 的强引用容器 |
| `Packages/com.hlwd.gpu-particle/Runtime/Resources/HlwdGpuParticleRuntimeResources.asset` | Player/AssetBundle 可追踪的包内运行时资源资产 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePlayParams.cs` | 播放参数 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleNativeRestoreState.cs` | 中途失败切回原生时的时间、Seed、倍速和暂停快照 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleHandle.cs` | 带代次的轻量句柄 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePrewarmLease.cs` | Clip/Buffer 显式预热引用，Dispose 后释放 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleBinding.cs` | Prefab 与 Clip 的序列化绑定、原生回退入口 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePlayer.cs` | `Play/Stop/Pause/Resume/SetTransform/Prewarm` 公共 API |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleRuntime.cs` | 运行时初始化、平台能力检测、World 生命周期 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleWorld.cs` | 活跃实例、批次、摄像机回调和资源释放 |
| `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleInstancePool.cs` | 无 GC 实例槽和版本号管理 |
| `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleBufferCache.cs` | Clip 共享 `GraphicsBuffer` 缓存和引用计数 |
| `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleStateRenderer.cs` | 状态轨道 Compute/Indirect 绘制 |
| `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleGeometryRenderer.cs` | 逐帧 Mesh/Trail 几何回放和实例合批 |
| `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleNativeFallback.cs` | 原生 `ParticleSystem` 播放、停止和倍速同步 |
| `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleMaterialRecipe.cs` | 烘焙后的材质渲染配方 |
| `Packages/com.hlwd.gpu-particle/Runtime/URP/GpuParticleRendererFeature.cs` | 把 GPU 粒子注入 URP Camera Renderer |
| `Packages/com.hlwd.gpu-particle/Runtime/URP/GpuParticleRenderPass.cs` | 在透明阶段提交状态/几何 Draw Packet |
| `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticlePlayback.compute` | 状态取样、插值、剔除、排序键和 Draw Args |
| `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticleBillboard.shader` | URP Billboard/Stretched Billboard 回放 Shader |
| `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticleMesh.shader` | URP Mesh 粒子回放 Shader |
| `Packages/com.hlwd.gpu-particle/Editor/Settings/GpuParticleProjectSettings.cs` | 默认输出、采样率、最大时长、验证阈值、摄像机配置 |
| `Packages/com.hlwd.gpu-particle/Editor/Settings/GpuParticleSettingsProvider.cs` | Project Settings 页面 |
| `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleAnalysisReport.cs` | 内部逐轨道分析报告和最终二态结论 |
| `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleAnalyzer.cs` | 扫描模块、脚本、材质、子发射器和外部依赖 |
| `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleSourceHasher.cs` | 排除 Binding/生成目录的源内容 Hash |
| `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleBakeFingerprint.cs` | 汇总源内容、设置、Adapter、Unity/URP/平台的完整烘焙指纹 |
| `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleShaderAdapter.cs` | 项目 Shader 到回放配方的扩展接口 |
| `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleBakeHook.cs` | 烘焙前后扩展接口 |
| `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleValidationRule.cs` | 项目级硬失败规则接口 |
| `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleRenderPipelineAdapter.cs` | 渲染管线扩展接口 |
| `Packages/com.hlwd.gpu-particle/Editor/Extensibility/GpuParticleExtensionRegistry.cs` | Editor `TypeCache` 扩展发现和稳定排序 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticlePreviewScene.cs` | 隔离 Preview Scene、摄像机、灯光和清理 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleSampler.cs` | 确定性时间推进、种子和生命周期采样 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleStateCapture.cs` | `GetParticles` 状态捕获和稳定粒子槽分配 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleGeometryCapture.cs` | `BakeMesh/BakeTrailsMesh` 几何捕获 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleClipBuilder.cs` | 轨道、帧索引、资源表和 Payload 构建 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBakeAssetWriter.cs` | 原子写入 `.asset`、`.bytes` 和 Mesh 子资源 |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBindingWriter.cs` | 非破坏式增加/更新 Prefab Binding |
| `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBakePipeline.cs` | Analyze -> Sample -> Build -> Validate -> Bind 总编排 |
| `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleValidationResult.cs` | 二态结果、硬失败码和可定位证据 |
| `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleHardFailureValidator.cs` | 结构、生命周期、Transform、材质和图像硬失败判定 |
| `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleRenderCapture.cs` | 原版/GPU 版关键帧离屏捕获 |
| `Packages/com.hlwd.gpu-particle/Editor/UI/GpuParticleBakerWindow.cs` | 一键/批量烘焙窗口 |
| `Packages/com.hlwd.gpu-particle/Editor/UI/GpuParticleMenu.cs` | Project 右键菜单和快捷入口 |
| `Packages/com.hlwd.gpu-particle/Editor/Build/GpuParticleBuildValidator.cs` | Build 前检查过期 Clip、丢失 Payload 和不支持 Shader |
| `Packages/com.hlwd.gpu-particle/Editor/URP/GpuParticleUrpInstaller.cs` | 一键检查/安装 Renderer Feature，并验证 RenderPass Event |
| `Packages/com.hlwd.gpu-particle/Documentation~/workflow.md` | 美术和技术美术工作流 |
| `Packages/com.hlwd.gpu-particle/Documentation~/architecture.md` | 数据格式、扩展点和运行时设计 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/README.md` | 独立项目接入说明 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleSampleController.cs` | 公共 API 示例 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleBasic.unity` | Billboard、Mesh、Trail 和池复用示例场景 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleBillboard.prefab` | Billboard 示例 Prefab |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleMesh.prefab` | Mesh 示例 Prefab |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleTrail.prefab` | Trail 示例 Prefab |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleBillboard.mat` | Billboard Sample URP 材质 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleMesh.mat` | Mesh Sample URP 材质 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleTrail.mat` | Trail Sample URP 材质 |
| `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleSampleProfile.asset` | Sample 烘焙/摄像机配置 |

### 3.2 DigitDoor 适配层

| 文件 | 操作 | 职责 |
|---|---|---|
| `Assets/Scripts_Hotfix/hlwd.game.asmdef` | 修改 | 显式引用 `Hlwd.GpuParticle.Runtime` |
| `Assets/Scripts_Hotfix/Modules/Effect/Playback/IEffectPlaybackBackend.cs` | 新增 | 项目侧统一播放后端接口 |
| `Assets/Scripts_Hotfix/Modules/Effect/Playback/ParticleSystemEffectBackend.cs` | 新增 | 封装现有粒子逻辑 |
| `Assets/Scripts_Hotfix/Modules/Effect/Playback/GpuParticleEffectBackend.cs` | 新增 | 封装包内 `GpuParticlePlayer` |
| `Assets/Scripts_Hotfix/Modules/Effect/EffectController.cs` | 修改 | 选择粒子后端、统一播放/隐藏/Transform，并让 GPU 粒子与独立 GPUI 模型并存 |
| `Assets/Scripts_Hotfix/Game/Battle/Systems/Effect/BattleAttachedEffectSystem.cs` | 修改 | 调用统一表现 Transform 同步接口 |
| `Assets/Scripts_Hotfix/Game/Skill/SkillCtrl.cs` | 修改 | 在现有 Effect Pool 注册后预热 GPU Clip/Buffer |
| `Assets/Editor/GpuParticle/DigitDoorGpuParticleBakeProfile.cs` | 新增 | 固定 DigitDoor 输出路径和战斗摄像机验收配置 |
| `Assets/Editor/GpuParticle/DigitDoorUnlitEffectBase02ShaderAdapter.cs` | 新增 | 适配首批资源使用的 `Custom/UnlitEffectBase02` 材质属性和 Variant |
| `Assets/Editor/GpuParticle/DigitDoorGpuParticleBuildGuard.cs` | 新增 | 在 YooAsset 构建前校验，构建后核对 Manifest/Build Map 依赖 |
| `Assets/Editor/GpuParticle/DigitDoorGpuParticleAndroidAcceptance.cs` | 新增 | Vulkan-only/GLES3-only 验收构建参数和运行时 API 记录 |
| `Assets/Editor/Build/BuildWindow/BuildStepData.cs` | 修改 | 在 `BuildAssetBundle` 步骤前后调用项目侧 GPU 粒子门禁 |
| `Assets/ResBundles/Generated/GpuParticle/` | 生成 | DigitDoor 的 Clip、Payload 和必要 Mesh 资源 |

不修改 `EffectCtrl.Play` 的业务签名，不修改 `EffectRes.Res`，不把 GPU 类型写进配置表。

---

## 4. 公共 API 与数据契约

### 4.1 用户可见状态

```csharp
namespace Hlwd.GpuParticle
{
    public enum GpuParticleBakeStatus : byte
    {
        Native = 0,
        GpuReady = 1,
    }

    internal enum GpuParticleTrackMode : byte
    {
        State = 0,
        Geometry = 1,
    }
}
```

`GpuParticleTrackMode` 不显示为质量等级，只用于诊断某个 GPU 可用资源内部采用了哪种回放方式。

### 4.2 Clip

```csharp
namespace Hlwd.GpuParticle
{
    public sealed class GpuParticleClip : ScriptableObject
    {
        [SerializeField] private int schemaVersion;
        [SerializeField] private string sourcePrefabGuid;
        [SerializeField] private Hash128 sourceContentHash;
        [SerializeField] private Hash128 bakeFingerprint;
        [SerializeField] private GpuParticleBakeStatus status;
        [SerializeField] private float duration;
        [SerializeField] private float sampleRate;
        [SerializeField] private bool loop;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private GpuParticleCapability requiredCapabilities;
        [SerializeField] private TextAsset payload;
        [SerializeField] private GpuParticleRuntimeResources runtimeResources;
        [SerializeField] private GpuParticleRendererRecipe[] rendererRecipes;
        [SerializeField] private GpuParticleMaterialRecipe[] materialRecipes;
        [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;
        [SerializeField] private Material[] materials;
        [SerializeField] private Mesh[] sourceMeshes;
        [SerializeField] private Mesh[] geometryFrames;

        public GpuParticleBakeStatus Status => status;
        public float Duration => duration;
        public float SampleRate => sampleRate;
        public bool Loop => loop;
        public Bounds LocalBounds => localBounds;
        public TextAsset Payload => payload;
    }
}
```

约束：

- `.asset` 只保存可检查元数据和 Unity 资源引用。
- 大块数值数据写入同目录 `.bytes`，由 Clip 强引用，确保 YooAsset/AssetBundle 自动收集依赖。
- `sourceContentHash` 由 `GpuParticleSourceHasher` 生成：规范化遍历 Prefab 层级和组件序列化属性，明确排除 `GpuParticleBinding` 与生成目录，再合并排序后的材质、Shader、纹理、Mesh 和 AnimationCurve 依赖 Hash。禁止直接保存完整 Prefab Dependency Hash，否则 Binding 反向引用 Clip 后会造成指纹自我失效。
- `bakeFingerprint` 在 Source Hash 之外还包含 Schema、采样设置、Camera Profile、启用的 Adapter 类型/版本、工具包版本、Unity/URP 版本、Render Pipeline Asset、Color Space、目标 Graphics API 和 Runtime Resource Hash；任一输入变化都必须重烘。
- Runtime 首次加载必须校验 Magic、Schema、长度、区段范围和 CRC；失败直接走 Native，不允许越界读取。

### 4.3 二进制 Payload

首版格式固定为 little-endian、16 字节对齐：

```text
Header
  magic = "HLGP"
  schemaVersion
  totalLength
  crc32
  sampleRate
  duration
  trackCount
  sectionTableOffset

TrackTable[]
  mode
  rendererMode
  simulationSpace
  flags
  rendererRecipeIndex
  materialRange
  trailMaterialRange
  subMeshMaterialMapOffset
  seedVariantTableOffset
  meshTableOffset
  particleTableOffset
  frameTableOffset
  dataOffset

RendererRecipe[]
  alignment, pivot, flip, sortMode, sortingFudge
  velocityScale, cameraVelocityScale, lengthScale
  normalDirection, minParticleSize, maxParticleSize
  shadowCasting, receiveShadows, motionVectorMode
  renderQueue, sortingLayerId, sortingOrder
  activeVertexStreams, transparencyOrderClass, drawPacketPolicy
  material/subMesh/trail mappings

SeedVariantTable[]
  systemSeedOffset, systemSeedCount, particleRange, frameRange, dataRange
SystemSeedRecord[]
  nativeSystemIndex, randomSeed

State Track Sections
  ParticleRecord[]: stableSlot, randomSeed, birthSample, deathSample, stateOffset, meshIndex, flags
  FrameRecord[]: activeSlotOffset, activeSlotCount
  ActiveSlotIds[]
  ParticleState[]: position, velocity, rotation3D, size3D, color, custom1, custom2
                   stableRandom, varyingRandom, noiseSum, animFrame, animBlend, uv

Geometry Track Sections
  FrameRecord[]: meshResourceIndex, trailMeshResourceIndex, bounds
```

`ParticleState` 全部使用 32 位 float/uint。每个离线粒子分配稳定 `stableSlot`，其存活期间状态连续存储；每帧额外保存活跃 Slot 列表，使 Compute 只处理当前活跃粒子，不扫描整段历史。

### 4.4 播放 API

```csharp
namespace Hlwd.GpuParticle
{
    public readonly struct GpuParticlePlayParams
    {
        public readonly Matrix4x4 LocalToWorld;
        public readonly float TimeScale;
        public readonly bool Loop;
        public readonly uint SeedVariant;

        public GpuParticlePlayParams(
            Matrix4x4 localToWorld,
            float timeScale = 1f,
            bool loop = false,
            uint seedVariant = uint.MaxValue)
        {
            LocalToWorld = localToWorld;
            TimeScale = timeScale;
            Loop = loop;
            SeedVariant = seedVariant;
        }
    }

    public enum GpuParticleStartResult : byte
    {
        GpuStarted = 0,
        NativeRequired = 1,
    }

    public readonly struct GpuParticleNativeRestoreState
    {
        public readonly float ElapsedClipTime;
        public readonly uint SeedVariant;
        public readonly float TimeScale;
        public readonly bool IsPaused;
    }

    public sealed class GpuParticlePlayer : MonoBehaviour
    {
        public bool IsPlaying { get; }
        public bool IsUsingGpu { get; }
        public GpuParticleHandle Play(in GpuParticlePlayParams parameters);
        public GpuParticleStartResult TryPlayGpu(
            in GpuParticlePlayParams parameters,
            out GpuParticleHandle handle);
        public void Stop(bool clear = true);
        public void Pause();
        public void Resume();
        public void SetTransform(Vector3 position, Quaternion rotation, Vector3 scale);
        public void SetTransform(Matrix4x4 localToWorld);
        public void Prewarm();
        public bool TryConsumeNativeFallbackRequest(
            out GpuParticleFailure failure,
            out GpuParticleNativeRestoreState restoreState);
    }
}
```

`GpuParticleHandle` 保存 World Slot 与 Generation。`Stop`、异步加载回调和播放完成回调都必须同时匹配 Slot + Generation，避免对象池 `ReturnFirst` 复用后旧回调停止新播放。

- `Play` 是独立项目的便利 API：GPU 预检失败时由包内 `GpuParticleNativeFallback` 负责原生播放。
- `TryPlayGpu` 不操作原生系统，只返回 `GpuStarted/NativeRequired`。DigitDoor 使用此 API，由 `EffectController` 作为唯一回退所有者，禁止包和项目重复 `Clear/Simulate/Play`。
- `TryPlayGpu` 模式中若播放期间发生异常，Player 只记录一条可消费的 Native fallback request；请求必须同时带出 Clip 时间、实际 Seed Variant、TimeScale 和暂停状态，不能只报告 `elapsedTime`。`EffectController.Update` 消费后按同一恢复算法切换 Native；独立 `Play` 模式由 Player 自己消费并回退。
- `GpuParticleRuntime.AcquirePrewarm(GpuParticleClip)` 返回 `GpuParticlePrewarmLease`；它只解析 Clip、创建共享 Buffer/Material，不激活或实例化 Prefab。

`SeedVariant == uint.MaxValue` 表示 Auto：对使用自动随机种子的源系统，以 Instance Generation 的稳定 Hash 对烘焙 Variant 数取模；源系统固定 Seed 或调用者显式传值时，使用指定 Variant 对 Variant 数取模。这样对象池重播不会永远重复同一组粒子，也不会引入运行时不可复现状态。

### 4.5 Prefab Binding

```csharp
namespace Hlwd.GpuParticle
{
    [DisallowMultipleComponent]
    public sealed class GpuParticleBinding : MonoBehaviour
    {
        [SerializeField] private GpuParticleBakeStatus status;
        [SerializeField] private GpuParticleClip clip;
        [SerializeField] private GpuParticleNativeSystemState[] nativeSystemStates;
        [SerializeField] private GpuParticleNativeRendererState[] nativeRendererStates;
        [SerializeField] private string lastFailureCode;

        public GpuParticleBakeStatus Status => status;
        public GpuParticleClip Clip => clip;
        public bool CanAttemptGpuPlayback => status == GpuParticleBakeStatus.GpuReady && clip != null;
    }
}
```

- Binding 保存每个原生系统的 `playOnAwake`、GameObject Active、Renderer Enabled、`useAutoRandomSeed`、`randomSeed` 和作者配置的 `main.simulationSpeed` 基线；`DefaultExecutionOrder(-10000)` 的 `OnEnable` 在首次 Camera Render 前完成 GPU 预检和原生 Renderer 抑制，不能先闪现一帧原生粒子。
- `GpuReady` 时播放器成功注册后，停止原生模拟并只关闭 Binding 记录的 Particle Renderer，不影响 MeshRenderer、GPUIPrefab 或其他表现节点。
- 注册失败或能力不满足时，恢复 Renderer 状态并由 `GpuParticleNativeFallback` 播放原生系统。
- `Native` 时完全不触碰原始粒子状态。

### 4.6 扩展接口

```csharp
namespace Hlwd.GpuParticle.Editor
{
    public interface IGpuParticleShaderAdapter
    {
        int Priority { get; }
        string Version { get; }
        bool CanHandle(Material material, ParticleSystemRenderer renderer);
        bool TryBuildRecipe(
            Material material,
            ParticleSystemRenderer renderer,
            out GpuParticleMaterialRecipe recipe,
            out string failureCode);
    }

    public interface IGpuParticleBakeHook
    {
        int Priority { get; }
        string Version { get; }
        void BeforeSample(GpuParticleBakeContext context);
        void AfterSample(GpuParticleBakeContext context);
    }

    public interface IGpuParticleValidationRule
    {
        int Priority { get; }
        string Version { get; }
        void Validate(GpuParticleValidationContext context, List<GpuParticleFailure> failures);
    }
}
```

- 扩展仅在 Editor 通过 `TypeCache.GetTypesDerivedFrom<T>()` 发现并按 `Priority + FullName` 稳定排序。
- 每个扩展必须提供显式 `Version`；类型全名、程序集版本、扩展版本和可定位的 MonoScript Hash 一并进入 Bake Fingerprint。
- Runtime 不做反射扫描，所有结果在烘焙时固化到 Clip。
- `IGpuParticleRenderPipelineAdapter` 负责创建 Preview Camera、材质配方和安装/验证对应 Renderer Feature。运行时硬失败的唯一合法结果是 Native，因此不提供可以“忽略错误继续 GPU”的策略扩展点。

---

## 5. 烘焙算法

### 5.1 分析阶段

`GpuParticleAnalyzer` 对 Prefab 做一次完整扫描：

1. 收集所有启用和禁用层级中的 `ParticleSystem`、`ParticleSystemRenderer`、子发射器和 Renderer 材质。
2. 记录 `simulationSpace`、Scaling Mode、Renderer Mode、Alignment、Sort Mode、Pivot、Flip、Texture Sheet、Custom Vertex Streams 和 Trail。
3. 检测改变粒子模块或调用 `Emit/TriggerSubEmitter` 的 MonoBehaviour；任何未被内置白名单或 `IGpuParticleBakeHook` 明确接管的启用脚本、Animator、Timeline/Tween 驱动都按 `DynamicScriptMutation` 处理，不能因为静态扫描没找到调用就假定安全。
4. 检测 Collision/Trigger、External Forces、Lights、World Space Trail、Rate over Distance 等依赖运行时环境的模块。
5. 为每个材质查询 Shader Adapter；State 与 Geometry 都必须先通过 Shader 兼容性验证。Geometry 只解决拓扑、顶点流和 Trail，不会烘焙 Shader 的顶点位移、`_Time`、屏幕采样或 Distortion；无法验证 Shader 时直接 `UnsupportedShader -> Native`。
6. 检查预计采样时长、最大粒子数、Mesh 可读性、Shader Instancing/平台关键字和 Android Graphics API。
7. 生成内部逐轨道 Route；只要任一必要轨道最终不能回放，Prefab 结论即为 Native。

首版支持矩阵必须硬编码为明确路由，而不是只记录警告：

| 能力 | 首版路线 |
|---|---|
| Birth/Death 等仅依赖确定生命周期的子发射器 | 可采样，仍需覆盖校验 |
| Collision/Trigger/External Forces/Lights | `DynamicWorldInput -> Native` |
| 脚本 `Emit`、手动 `TriggerSubEmitter`、运行时模块修改 | `DynamicScriptMutation -> Native` |
| Skinned Mesh Shape 或 Animator/Timeline/Tween 驱动 | 无 Bake Hook 时 `DynamicAnimationInput -> Native` |
| 移动发射器 + World/Custom Space | `WorldSpaceEmitterHistory -> Native` |
| Rate over Distance、移动 World Space Trail | `MovementHistoryRequired -> Native` |
| 自定义 Shader 的 `_Time`、屏幕采样、Distortion、不可验证顶点位移 | `UnsupportedShader -> Native` |

### 5.2 确定性采样

- 在 `EditorSceneManager.NewPreviewScene()` 中实例化 Prefab，不在当前场景运行，也不修改源粒子模块。
- 保存每个系统的 `useAutoRandomSeed` 和 `randomSeed`，采样副本强制固定 Seed；默认烘焙 4 个 Seed Variant，运行时按播放 Seed 选择其一。
- 默认采样率 120 Hz，`dt = 1 / 120`。设置页允许提高但不允许低于 60 Hz。
- 按顺序推进副本：`Stop(true, StopEmittingAndClear)` -> `Clear(true)` -> 设置 Seed -> `Play(true)` -> 每步 `Simulate(dt, true, false, false)`；最后一个参数必须为 false，避免项目 `fixedDeltaTime` 覆盖 120 Hz 采样步长。
- 非循环时长先递归计算所有子发射器链的 `startDelay + duration + maxStartLifetime` 上界，再实际采样到 `IsAlive(true) == false` 连续两个采样帧；超过项目最大时长时直接 Native，不截断。
- 循环系统按最长循环周期和递归粒子寿命完成预热，再至少比较连续两个完整周期边界的粒子数、状态和子发射器轨道；不能证明周期稳定时增加记录周期，达到最大时长仍不稳定则 Native。
- 对 Root Transform 分别验证静止、平移、旋转、非等比缩放；首版只有 Local Space 或“发射器全程静止”的 World/Custom Space 可以进入 GPU。需要发射器历史、每粒子出生矩阵或不可预测移动输入的一律 Native。

### 5.3 状态轨道

适用：标准 Billboard、Stretched Billboard、Mesh 粒子，且 Shader Adapter 能生成等价配方。

每个采样帧通过 `GetParticles` 记录：

- `randomSeed`、出生/死亡采样帧和稳定 Slot。Slot 使用 `randomSeed + 出生采样帧 + 同帧序号` 建立，并用剩余寿命变化与位置连续性复核；出现不可消歧碰撞时切 Geometry Track。`randomSeed` 同时写入 Payload，不能只在 Editor 临时用于配对。
- Position、Velocity、Rotation3D、AngularVelocity3D。
- Current Size3D、Current Color、RemainingLifetime、StartLifetime。
- 通过 `GetMeshIndex` 与 `GetCustomParticleData` 捕获 Mesh Index、Custom Data 1/2。
- Shader Adapter 必须声明实际读取的 Stable Random、Varying Random、Noise Sum、AnimFrame、AnimBlend、UV 等顶点流；能由已验证公式从状态直接求值的字段直接记录求值结果，不能求值的字段才进入 `BakeMesh` Probe。
- `BakeMesh` 本身不提供粒子 ID，禁止用顶点顺序或 `GetParticles` 数组下标猜测对应关系。Probe 只能在 Adapter 确认可占用一个未被 Shader 使用的 Custom1/Custom2 分量时，把 `stableSlot` 编码到该诊断顶点流，再从烘焙 Mesh 反查每组顶点；必须验证每个活跃 Slot 恰好映射一次且所有所属顶点携带同一 ID。
- 没有空闲诊断分量、ID 精度不足、一个顶点落入多个 Slot、活跃 Slot 缺失，或跨帧映射不稳定时，整个 Renderer 改走 Geometry Track；Geometry 仍不满足摄像机/Shader/排序约束时最终转 Native。
- Simulation Space 与 Root Transform 关系。
- 静态 World/Custom Space 状态在烘焙时乘参考 Root/Custom Matrix 的逆矩阵归一到 Clip 空间；运行时只使用 Play 起始矩阵。检测到 Root/Custom Transform 后续变化时立即请求 Native，禁止把当前矩阵重复施加到已经发射的世界粒子。

运行时在两个相邻采样间：

- Position/Velocity/Size/Color/Custom Data 线性插值。
- Rotation 使用归一化 Quaternion 插值。
- 出生和死亡按采样边界开关，允许最多 1 个采样帧偏差。
- Billboard 朝向在当前摄像机下重建；Stretched Billboard 同时使用速度与 `cameraVelocityScale/velocityScale/lengthScale`。

### 5.4 几何轨道

适用：Shader 已通过兼容验证，但因 Trail、复杂顶点流或拓扑而无法由状态数据重建，且几何对摄像机独立或摄像机相对姿态固定的轨道。Geometry 不是任意 Shader 的兜底。

- 每帧显式传入 Bake Profile Camera，调用 `ParticleSystemRenderer.BakeMesh` 捕获最终粒子几何，并把 Camera View/Projection、相对 Root Pose、Aspect 和 Bake Options 写入 Recipe。
- Trail 使用同一 Camera 调用 `ParticleSystemRenderer.BakeTrailsMesh` 单独捕获。
- 保存 Mesh 顶点布局、SubMesh、Bounds、原材质引用、Sorting Layer/Order 和渲染队列。
- 几何帧作为 Clip 子资源写入，运行时取最近采样帧；不对拓扑变化帧做错误插值。
- 只有验证为顺序无关或允许组内排序差异的材质，才把相同 Clip + Frame + Material 的实例用 `DrawMeshInstanced` 合批，每批最多 1023 个矩阵；需要逐实例透明顺序时拆 Draw Packet，仍不能与场景正确排序则 Native。
- Camera-dependent Geometry 只允许当前 Camera 的投影、相对 Root 旋转和相对位置在严格 epsilon 内匹配参考值；“落在某个角度/FOV 范围”不等于几何正确。DigitDoor 中弹体可出现在不同屏幕位置时，Camera-facing Geometry 默认 Native，除非改走 State Track。

### 5.5 资源写入

输出命名规则：

```text
<OutputRoot>/<SourcePrefabName>/<SourcePrefabName>.gpuparticle.asset
<OutputRoot>/<SourcePrefabName>/<SourcePrefabName>.gpuparticle.bytes
<OutputRoot>/<SourcePrefabName>/<SourcePrefabName>_Geometry.asset
```

写入规则：

1. 全部先写到同目录临时文件/临时 Asset。
2. 完成 Payload 校验、资源引用校验和视觉校验后再替换正式输出。
3. 失败时删除临时资源，保留上一版产物但把 Binding 状态设为 Native；不得把旧 Fingerprint 的 Clip 继续标记为 GPU 可用，也不得留下半写入 Clip。
4. 成功后通过 `PrefabUtility.SaveAsPrefabAsset` 只更新 `GpuParticleBinding`。
5. `AssetDatabase.StartAssetEditing/StopAssetEditing` 必须包在 `try/finally`。

---

## 6. Runtime 设计

### 6.1 World 与实例生命周期

- `GpuParticleRuntime` 通过 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 创建一个 `DontDestroyOnLoad` 的隐藏 World。
- World 订阅 `RenderPipelineManager.beginCameraRendering`；关闭/Domain Reload 时取消订阅并释放全部 Buffer。
- `GpuParticleInstancePool` 使用数组空闲链表管理 Slot，不在 Play/Stop 每次分配集合。
- 每个实例只保存 Clip ID、播放时间、倍速、状态、LocalToWorld、Bounds、Seed Variant 和 Generation。
- Update 只推进时间和处理结束事件；具体粒子状态在 Camera Rendering 阶段按可见 Batch 提交。
- `Stop(clear: true)` 当帧从批次移除；`Pause` 保留当前时间；`Resume` 继续；循环按 Clip Duration 取模。
- `Stop`、`OnDisable`、World Dispose 和原生回退释放必须幂等；同一 Generation 只能归还一次实例槽和 Buffer 引用。
- `GpuParticleRendererFeature` 在 URP 透明阶段调用 World 生成 Draw Packet；`beginCameraRendering` 只更新 Camera Context，不能在不确定的 SRP 阶段直接提交透明 Draw。
- Editor Installer 必须把 Renderer Feature 安装到当前 URP Renderer Data；缺失 Feature 时预检返回 `NativeRequired`，Build Validator 直接报错。

### 6.2 Clip Buffer 缓存

- `GpuParticleBufferCache` 按 Clip Instance ID 建共享条目。
- `Prewarm` 解析 Payload 并一次性创建不可变 `GraphicsBuffer`、MaterialPropertyBlock 和 Draw Args。
- Clip 强引用 `HlwdGpuParticleRuntimeResources.asset`；该资产强引用 Compute、两个播放 Shader、默认 Mesh 和 ShaderVariantCollection。Prewarm 验证 Kernel/Pass 并 WarmUp Variant，避免仅靠包内文件路径导致 Player Strip。
- 多个播放实例共享 Clip 数据，只上传每实例的 Transform/Time。
- 引用计数归零后不立即释放；按可配置空闲帧数延迟释放，避免对象池高频进出抖动。
- 场景切换、Low Memory、Subsystem Registration 和应用退出都必须显式 Dispose。

### 6.3 状态轨道 GPU 流程

每个 Clip Batch 每个 Camera：

1. CPU 更新一段连续 Instance Data。
2. Compute 根据实例时间查 Frame Record 和 Active Slot 范围。
3. Compute 读取相邻状态、插值并构建粒子实例数据。
4. Compute 用 Clip Local Bounds 与实例矩阵做实例级剔除，再做粒子级可选剔除。
5. 需要排序时生成 Key，并按活跃粒子数选择小批次 Bitonic 或大批次 4-pass Radix Sort；覆盖 `Distance/Oldest/Youngest/Depth`，不需要排序时不运行 Sort Kernel。
6. Compute 写入 Indirect Args。
7. 根据 `transparencyOrderClass` 提交 Draw Packet：只有 Opaque/Cutout/Additive 等经 Adapter 和视觉校验确认顺序无关的配方，才允许同 Clip/Track/Material 跨实例合并为一次 Indirect Draw；普通 Alpha Blend 每个原始 Renderer、每个 Effect 实例保留独立 Packet，禁止跨实例合批。

State Track 的粒子内部排序不能替代 Renderer 级透明排序。普通 Alpha Packet 必须复现 `RenderQueue + SortingLayerValue + SortingOrder + CameraDepth + SortingFudge` 的稳定顺序；由于一个 URP Renderer Feature 无法任意插入 Unity 场景透明 Renderer 的排序列表，只有项目 Profile 证明“专用透明 Queue 区间”或“不会与场景透明物交错”时才允许 GPU。需要与场景透明物逐 Renderer 交错且无法证明顺序的轨道，标记 `TransparentOrderUnrecoverable -> Native`。

首版性能约束：

- 正常稳定帧 `GC.Alloc == 0 B`。
- 同 Clip 的实例不重复创建 Material、Mesh 或 Clip Buffer。
- 禁止每帧 `GetComponentsInChildren`、LINQ、反射、字符串查找和 `new List`。
- Renderer 不访问 Ember World，不参与命中和伤害计算。

### 6.4 几何轨道流程

- CPU 只计算离散 Frame Index，并先按 `RenderQueue + SortingLayerValue + SortingOrder + CameraDepth + SortingFudge` 生成稳定 Draw Packet，再按允许合批的 `Clip + Track + Frame + Material` 聚组。
- Matrix 列表来自预分配 Native/Managed 数组，超过 1023 自动拆批。
- 使用原材质或烘焙出的材质副本；副本只开启 Instancing，不修改源材质 Asset。`DrawMeshInstanced` 的一组实例不会逐实例透明排序，因此只有 Recipe/视觉校验允许组内排序差异时才合批。
- 不支持 Instancing 的 Shader 先由 Adapter 生成兼容材质；生成失败即 Native。
- 参考摄像机不匹配时不尝试错误播放，直接触发 Native fallback。

### 6.5 原生回退

运行时出现以下情况立即回退：

- Clip/Payload 丢失、Schema 不匹配、CRC 错误。
- Compute Shader 或所需 GraphicsBuffer 能力不可用。
- 当前 Graphics API 不在 Clip 能力集合内。
- 材质/Shader 被 Strip 或创建失败。
- Geometry Track 的摄像机约束不满足。
- World 注册、Buffer 分配或 Draw 准备抛出异常。

平台、Renderer Feature、Camera、Shader/Kernel/Variant、Payload 和 Buffer 必须在关闭原生 Renderer 前一次性预检。独立 API `Play` 与 DigitDoor Native backend 必须共用以下确定性恢复算法，禁止简写成一次 `Simulate(elapsedTime)`：

1. 原子记录 `ElapsedClipTime + 实际 SeedVariant + TimeScale + IsPaused`，取消 GPU Handle，并按 Binding 基线恢复所有原生系统/Renderer。
2. 对 Variant 中每个系统恢复对应 `randomSeed`，关闭 `useAutoRandomSeed`；仅对根 ParticleSystem 执行 `Stop(true, StopEmittingAndClear) -> Clear(true) -> Play(true)`，子系统由 `withChildren: true` 推进，不能重复推进。
3. Catch-up 期间撤掉外部 TimeScale 乘数，恢复每个系统作者配置的 `main.simulationSpeed`。以与烘焙完全相同的 `dt = 1 / sampleRate` 循环调用 `Simulate(step, true, false, false)`，最后余数也用同一参数推进；`ElapsedClipTime` 已经是缩放后的 Clip 时间，不得再乘一次 TimeScale。
4. Catch-up 完成后才重新应用当前 TimeScale；`IsPaused` 时立即 Pause，否则继续 Play。恢复结果必须在关键帧与 Native 基线比较，Seed、粒子数和状态无法复现即将该 Prefab 标为 Native。

首版 GPU 资源已经排除发射器历史和动态世界输入，因此该重建只用于确定性轨道。DigitDoor 调用 `TryPlayGpu`，启动失败只接收 `NativeRequired`；播放中失败消费完整 `GpuParticleNativeRestoreState`，由 `EffectController` 唯一决定切换 Native backend。

预检后仍发生不可恢复的中途异常时，当前播放记为 `RuntimeGpuFailure` 并把该 Clip 加入本进程 Native 黑名单，后续播放不再尝试 GPU。独立 `Play` 由包恢复原生；`TryPlayGpu` 只写入一次 fallback request，由项目所有者消费并恢复。日志只在首次失败时输出 Prefab、Clip 和 FailureCode，禁止每帧刷屏。

---

## 7. 硬失败校验

校验器最终只返回 `GpuReady` 或 `Native`，但报告保留明确 FailureCode：

| FailureCode | 判定 |
|---|---|
| `MissingTrack` | 原版某 Renderer 连续 2 个关键帧可见，而 GPU 版对应轨道不存在 |
| `BlankOutput` | 原版 Alpha 覆盖超过 64 像素且 GPU 覆盖低于原版 10%，连续 2 帧 |
| `MajorCoverageLoss` | 主体可见阶段 GPU 粒子/Trail 覆盖低于原版 50% 连续 3 帧，或轮廓 IoU 低于 0.25 导致主体不可识别 |
| `MajorParticleCountLoss` | 原版活跃粒子超过 20 且 GPU 活跃数低于原版 50%，连续 3 帧 |
| `TransformMismatch` | 主体质心世界偏移大于 `max(0.2m, 原版 Bounds 对角线 25%)` |
| `BoundsMismatch` | GPU Bounds 任一轴超过原版 4 倍或低于原版 0.25 倍，且主体可见 |
| `NonFiniteGeometry` | 任一位置、旋转、大小、Bounds 出现 NaN/Infinity |
| `SevereStretch` | 单粒子/三角形边长超过原版同帧 Bounds 对角线 4 倍 |
| `MaterialFailure` | Error Shader、粉色输出、源明显发光但 GPU 持续近黑，或必要纹理丢失 |
| `PersistentFlicker` | 静态输入下 GPU 连续帧覆盖面积在非出生/死亡区间反复跳变超过 70% |
| `LifecycleMismatch` | 首次可见、结束或循环接缝偏差超过 2 个采样帧 |
| `PoolReuseFailure` | 第二次播放未从零开始、残留上一轮粒子或旧 Handle 停止新播放 |
| `UnsupportedPlatform` | Android Vulkan/GLES3 所需 Kernel、Shader Variant 或 Buffer 能力缺失 |
| `DynamicWorldInput` | Collision/Trigger/External Forces/Lights 等依赖运行时世界输入 |
| `DynamicScriptMutation` | 未被 Bake Hook 接管的脚本 Emit、手动子发射器或模块修改 |
| `DynamicAnimationInput` | Animator/Timeline/Tween/Skinned Shape 的运行时输入不可确定 |
| `WorldSpaceEmitterHistory` | 移动发射器的 World/Custom Space 需要历史矩阵 |
| `MovementHistoryRequired` | Rate over Distance 或移动 World Trail 需要真实路径历史 |
| `UnsupportedShader` | Shader 的顶点/像素行为无法由 Recipe 验证和重建 |
| `TransparentOrderUnrecoverable` | 顺序相关透明粒子需要与场景透明 Renderer 交错，但 URP Draw Packet 无法复现原排序 |
| `MissingRendererFeature` | 当前 URP Renderer Data 未安装正确版本的 GPU Particle Feature |
| `StaleBakeFingerprint` | Source/设置/Adapter/Unity/URP/平台任一指纹输入变化 |
| `RuntimeGpuFailure` | 预检后仍发生不可恢复异常；本进程后续播放强制 Native |

关键帧集合至少包含：首帧、首次可见、首个 Burst、峰值粒子数、50% 生命周期、最后可见帧、结束后一帧、循环接缝前后各两帧。

粒子数、轨道覆盖、生命周期、Bounds 和非有限数值对全部 120 Hz 采样帧检查；HDR 图像捕获先覆盖关键帧，再自动加入轻量指标发现异常的前后帧。这样允许细节差异，但不会让仅剩少量粒子的明显失真结果通过。

验证场景至少覆盖：

- 原点静止。
- Root 平移与旋转。
- 非等比缩放。
- `timeScale = 0.5 / 1 / 2`。
- Pause 0.5 秒后 Resume。
- Stop 后同一对象池实例立即重播。
- 配置的战斗摄像机中心、边缘和缩放范围。
- 参考不透明遮挡物、透明遮挡物，以及不同 Sorting Layer/Order 的前后关系；无法在 URP RenderPass 中保持主体遮挡关系时转 Native。

图像差异只用于发现硬失败，不输出“高/中/低还原度”，也不因为允许的细节差异拒绝 GPU。

---

## 8. 一键工具 UX

菜单：`Tools/Hlwd/GPU Particle Baker`。

Project 右键：

- `GPU Particle/Bake Selected Prefabs`
- `GPU Particle/Validate Selected Prefabs`
- `GPU Particle/Revert To Native`

窗口默认区域：

1. Prefab 列表，可拖入 Prefab 或文件夹。
2. 统一主按钮 `分析并烘焙`。
3. 每项只显示绿色 `GPU 可用` 或灰色 `保留原生`。
4. Native 项显示一行首要原因和“定位对象/材质”按钮。
5. 顶部显示成功数、Native 数、过期数，不显示质量分数。

高级折叠区：

- 输出目录。
- 采样率，默认 120 Hz。
- 最大采样时长，超过即 Native。
- Seed Variant 数，默认 4。
- 参考摄像机 Profile。
- 是否保留验证截图。

批处理行为：

- 依赖 Hash 未变化且 Schema 一致时跳过。
- 任一资源失败不阻断其余资源，但批处理最终明确返回失败数量。
- 支持取消；取消时完成当前 Prefab 的清理，不写入半成品。
- Build 前若发现 `GpuReady` Binding 的 Clip 过期，直接让 Build 失败并列出资源；需要保留原生时，必须在窗口中显式执行 `Revert To Native`，不能在 Build 过程中静默修改 Asset 或使用旧 GPU 数据。

---

## 9. 任务拆解

### Task 1: 建立 UPM 包骨架与程序集边界

**Consumes:** 本计划的目录边界和 Unity 2022.3 版本约束。

**Produces:** 可被 Unity 导入、Runtime/Editor 分离且不依赖 DigitDoor 的空包。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/package.json`
- Create: `Packages/com.hlwd.gpu-particle/README.md`
- Create: `Packages/com.hlwd.gpu-particle/CHANGELOG.md`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Hlwd.GpuParticle.Runtime.asmdef`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Hlwd.GpuParticle.Editor.asmdef`

- [ ] 记录 `git status --short`，确认并保护现有无关改动。
- [ ] 创建 `com.hlwd.gpu-particle` 0.1.0 包，`unity` 设为 `2022.3`，并在 `package.json` 声明 `com.unity.render-pipelines.universal: 14.0.12`。
- [ ] Runtime asmdef 不引用 DigitDoor，显式引用 Core/Universal Runtime；Editor asmdef 引用 Runtime 和必要的 Core/Universal Editor 程序集，并限制 Editor 平台。
- [ ] 在 Unity 刷新程序集，确认没有新增 Console Error。
- [ ] 执行依赖边界检查：

```bash
rg -n "Hlwd\.Game|GameEntity|Ember|YooAsset|Luban|GPUInstancer" \
  Packages/com.hlwd.gpu-particle/Runtime \
  Packages/com.hlwd.gpu-particle/Editor
```

Expected: 无匹配。

- [ ] Commit: `feat(gpu-particle): scaffold reusable UPM package`

### Task 2: 定义 Clip、Binding、播放 API 和安全 Blob Reader

**Consumes:** Task 1 程序集。

**Produces:** 稳定的公共 Runtime API 与可校验数据契约。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleTypes.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleClip.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleBlobReader.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleRendererRecipe.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Data/GpuParticleRuntimeResources.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePlayParams.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleNativeRestoreState.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleHandle.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePrewarmLease.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleBinding.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePlayer.cs`

- [ ] 按第 4 节实现二态 Status、Capability、Renderer/Material Recipe、逐系统 Seed Variant Table、Native Restore State 和 FailureCode。
- [ ] 实现 Blob Header/Section Table 的范围、对齐、版本和 CRC 校验。
- [ ] 实现 Runtime Resource 强引用与缺失资源校验契约；实际 Compute/Shader 资源在 Task 8 创建，任一引用缺失时必须得到 `NativeRequired`，不能空白渲染。
- [ ] `GpuParticlePlayer.Play` 实现独立使用的 Native fallback；`TryPlayGpu` 只报告结果，不操作原生系统。
- [ ] 实现 Slot + Generation Handle；无效或过期 Handle 的操作只返回 false，不影响当前实例。
- [ ] 在一个临时 Sample Prefab 上验证 Play、Pause、Resume、Stop、SetTransform 和重复播放。
- [ ] Unity 编译无 Error；执行 `git diff --check`。
- [ ] Commit: `feat(gpu-particle): define clip and playback contracts`

### Task 3: 实现 Project Settings、分析模型和扩展注册

**Consumes:** Task 2 类型。

**Produces:** 可判断 State/Geometry/Native 内部路线的分析器。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/Settings/GpuParticleProjectSettings.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Settings/GpuParticleSettingsProvider.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleAnalysisReport.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleAnalyzer.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleSourceHasher.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Analysis/GpuParticleBakeFingerprint.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleShaderAdapter.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleBakeHook.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleValidationRule.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Extensibility/IGpuParticleRenderPipelineAdapter.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Extensibility/GpuParticleExtensionRegistry.cs`

- [ ] 实现默认 120 Hz、4 Seed、最大采样时长、输出目录、战斗摄像机范围设置。
- [ ] 使用 `TypeCache` 实现扩展发现，按 Priority 与类型全名稳定排序。
- [ ] Source Hasher 排除 `GpuParticleBinding` 和生成目录；Fingerprint 覆盖设置、Adapter、工具/Unity/URP、Camera、Color Space 和 Graphics API。
- [ ] 扫描所有 ParticleSystem/Renderer/材质/子发射器/Trail/动态模块。
- [ ] 实现本文列出的确定 Native 原因，不做模糊“可能不支持”结果。
- [ ] 内置 URP Particle Unlit、标准 Billboard、Stretched Billboard、Mesh 分析规则。
- [ ] 用首批三个 Prefab 跑 Analyze，报告必须能定位具体 Transform 与 Material。
- [ ] Unity 编译无 Error；`git diff --check`。
- [ ] Commit: `feat(gpu-particle): add analyzer and extension registry`

### Task 4: 实现隔离 Preview Scene 与确定性采样

**Consumes:** Analysis Report 和 Settings。

**Produces:** 不修改源 Prefab 的确定性逐帧采样会话。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticlePreviewScene.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleSampler.cs`

- [ ] Preview Scene 在 `IDisposable`/`try/finally` 中创建和关闭。
- [ ] 克隆 Prefab，固定 Seed，按 120 Hz 顺序 Simulate。
- [ ] 只对根 ParticleSystem 层级推进一次 `Simulate(withChildren: true)`；子系统不得再单独推进造成双倍时间。
- [ ] 推导非循环结束时间与循环预热/记录区间；超过最大时长直接 Native。
- [ ] 捕获 Root 静止、移动、旋转、缩放 Profile。
- [ ] 同 Prefab 同 Seed 连续采样两次，对粒子数、位置、颜色和生命周期做 byte-level/epsilon 一致性检查。
- [ ] 强制中断烘焙一次，确认 Preview Scene、临时对象和 Mesh 全部清理。
- [ ] Commit: `feat(gpu-particle): add deterministic preview sampling`

### Task 5: 捕获状态轨道与几何轨道

**Consumes:** Task 4 Sample Session。

**Produces:** 完整的中间轨道数据。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleStateCapture.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleGeometryCapture.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleMaterialRecipe.cs`

- [ ] 为粒子分配跨帧稳定 Slot，记录 birth/death、状态和 Frame Active Slot 表。
- [ ] 捕获 `randomSeed`、Rotation3D、Size3D、Color、Custom Data 和 Mesh Index，并按 Adapter 写入实际使用的 Stable/Varying Random、Noise、AnimFrame/AnimBlend/UV 求值结果。
- [ ] BakeMesh Probe 只用未被 Shader 使用的 Custom1/2 分量显式注入 `stableSlot`；不得按顶点顺序猜粒子身份，身份通道不可用或映射不唯一时切 Geometry。
- [ ] 支持 Local，以及发射器全程静止的 World/Custom Simulation Space；检测到移动历史需求时输出对应 FailureCode 并转 Native。
- [ ] 通过 `BakeMesh` 捕获复杂粒子几何，通过 `BakeTrailsMesh` 捕获 Trail。
- [ ] 保存 SubMesh、Bounds、Sorting、原材质和 Instancing 能力。
- [ ] 对每个源 Renderer 做轨道覆盖断言；漏掉任何启用 Renderer 直接 Native。
- [ ] 用首批三个 Prefab 导出一次中间报告，人工确认子粒子/爆炸/弹体轨道都存在。
- [ ] Commit: `feat(gpu-particle): capture state and geometry tracks`

### Task 6: 构建 Clip、Payload 与非破坏式 Binding

**Consumes:** Task 5 中间数据。

**Produces:** 可加载的 `.asset + .bytes + geometry sub-assets` 和默认 `Native` 的 Prefab Binding；Task 10 完成视觉门禁前不得写成 `GpuReady`。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleClipBuilder.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBakeAssetWriter.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBindingWriter.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBakePipeline.cs`

- [ ] 实现第 4.3 节 Payload 格式、16 字节对齐和 CRC。
- [ ] 使用临时资源 + 原子替换，失败不破坏上一版。
- [ ] 生成不自引用的 `sourceContentHash`、完整 `bakeFingerprint`、Renderer/Material Recipe、Runtime Resource 和项目 ShaderVariantCollection 引用表。
- [ ] 源 Prefab 仅增加/更新 `GpuParticleBinding`；记录变更前后 Prefab YAML Diff，确认粒子模块/材质引用未变化。
- [ ] Binding 记录每个原生系统/Renderer 的基线 Active、Enabled、playOnAwake、`useAutoRandomSeed`、`randomSeed`、作者 `simulationSpeed`，并在首帧前预检成功后才抑制原生 Renderer。
- [ ] 在视觉校验器尚未完成时，生成 Binding 的公开状态固定为 `Native`；内部报告可以记录候选路线，但不能提前启用 GPU。
- [ ] 对只读/Package Prefab 生成 Variant，并在结果中给出 Variant 路径。
- [ ] Runtime 用 Blob Reader 加载刚生成的 Clip，逐 Section 校验长度和内容。
- [ ] Commit: `feat(gpu-particle): build clips and prefab bindings`

### Task 7: 实现 Runtime World、实例池、Buffer Cache 和原生回退

**Consumes:** Task 2 Runtime API 与 Task 6 Clip。

**Produces:** 可预热、可复用、无过期回调的 Runtime 核心。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleRuntime.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleWorld.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleInstancePool.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleBufferCache.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleNativeFallback.cs`
- Modify: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticlePlayer.cs`

- [ ] 实现隐藏 World 的初始化、Camera 回调和所有退出路径的 Dispose。
- [ ] 实现实例空闲链表与 Generation 校验。
- [ ] 实现 Clip Buffer 引用计数、预热、空闲延迟释放和 Low Memory 清理。
- [ ] 实现 `AcquirePrewarm` Lease；加载但未播放的 Clip 可持有共享 GPU 资源，Dispose 后引用计数正确回落。
- [ ] 实现 Runtime Capability 检测和一次性 Failure 日志。
- [ ] 实现统一 Native catch-up：恢复 Variant 的逐系统 Seed 和作者 simulationSpeed，仅推进根系统，按烘焙 `dt` 分步 `Simulate(..., false)` 并处理余数，最后再应用 TimeScale/Pause；禁止一次性 `Simulate(elapsedTime)`。
- [ ] 在 GPU 播放中途注入失败，比较恢复后的 Native 与同 Seed 基线，确认粒子数、关键状态、暂停状态一致且 TimeScale 未重复施加。
- [ ] 构造 `ReturnFirst` 等价复用场景，验证旧 Handle/旧完成回调不能停止新播放。
- [ ] Profiler 验证稳定 Play/Stop 循环没有持续 GC，Buffer 数量不会单调增长。
- [ ] Commit: `feat(gpu-particle): add runtime world and fallback lifecycle`

### Task 8: 实现状态轨道 GPU Renderer

**Consumes:** State Track Payload、World Batch 和 Material Recipe。

**Produces:** Billboard/Stretched/Mesh 粒子的 Compute + GPU 绘制路径。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleStateRenderer.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/URP/GpuParticleRendererFeature.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/URP/GpuParticleRenderPass.cs`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticlePlayback.compute`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticleBillboard.shader`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Shaders/GpuParticleMesh.shader`
- Create: `Packages/com.hlwd.gpu-particle/Runtime/Resources/HlwdGpuParticleRuntimeResources.asset`

- [ ] 实现按 Renderer Recipe 的透明顺序分类：顺序无关材质可按 Clip/Track/Material 跨实例批处理，普通 Alpha 每个源 Renderer/Effect 实例保持独立 Draw Packet。
- [ ] 创建并填充 Runtime Resources Asset，强引用 Compute、播放 Shader、默认 Mesh 和标准 ShaderVariantCollection；Prewarm 验证所有 Kernel/Pass 并 WarmUp Variant。
- [ ] 通过 URP Renderer Feature 在配置的透明 RenderPass Event 提交 Draw；没有 Feature 时返回 Native，不在 `beginCameraRendering` 直接乱序绘制。
- [ ] Compute 查活跃 Slot、插值状态、应用 LocalToWorld、剔除并写 Draw Args。
- [ ] 实现 Billboard、Stretched Billboard、Mesh Index 和 Texture Sheet UV。
- [ ] 实现 Color、Custom1/2、Soft Particle 所需深度数据和 MaterialPropertyBlock。
- [ ] 对需要排序的轨道实现粒子内排序键和 Renderer Packet 稳定排序；无法满足场景透明交错契约时返回 `TransparentOrderUnrecoverable -> Native`。
- [ ] 在 Scene/Game 两个 Camera 下验证朝向和 Bounds。
- [ ] Profiler 验证 50 个同 Clip 实例共享 Buffer，稳定帧 0 B GC，Draw Call 按批次而非按粒子增长。
- [ ] Commit: `feat(gpu-particle): render sampled state tracks on GPU`

### Task 9: 实现几何与 Trail 回放

**Consumes:** Geometry Frame Mesh 与 Camera Profile。

**Produces:** Shader 已验证的复杂顶点流/Trail GPU 几何回放路径。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Runtime/Rendering/GpuParticleGeometryRenderer.cs`
- Modify: `Packages/com.hlwd.gpu-particle/Runtime/Playback/GpuParticleWorld.cs`
- Modify: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleGeometryCapture.cs`

- [ ] 按最近采样帧选择 Mesh，不跨拓扑错误插值。
- [ ] 按 Clip + Track + Frame + Material 分组 `DrawMeshInstanced`，自动拆分 1023 上限。
- [ ] 保留原 Material Property、Render Queue、Sorting Layer/Order 和 SubMesh；明确 `DrawMeshInstanced` 只保证组排序，需逐实例透明排序时拆包或 Native。
- [ ] 实现参考摄像机约束检查；超出约束触发 Native。
- [ ] 验证 Trail 首帧、峰值、停止和循环接缝没有整段缺失或明显断裂。
- [ ] Profiler 验证矩阵批次数组复用且稳定帧 0 B GC。
- [ ] Commit: `feat(gpu-particle): add geometry and trail playback`

### Task 10: 实现硬失败自动校验和二态结论

**Consumes:** 原版 Preview、GPU Runtime 和第 7 节 FailureCode。

**Produces:** 可复现、可定位且不会误启 GPU 的最终结果。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleValidationResult.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleHardFailureValidator.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Validation/GpuParticleRenderCapture.cs`
- Modify: `Packages/com.hlwd.gpu-particle/Editor/Baking/GpuParticleBakePipeline.cs`

- [ ] 同一 Preview 环境分别渲染原版与 GPU 版到线性 HDR RenderTexture。
- [ ] 对全部采样帧实现 Missing/MajorCount/MajorCoverage/Transform/Bounds/NonFinite/Lifecycle 判定，对关键帧和异常邻帧实现 Blank/Stretch/Material/Flicker/轮廓判定。
- [ ] 失败报告包含 Prefab、Transform Path、Track、Material、Frame、FailureCode 和原/GPU 截图路径。
- [ ] 任一硬失败把 Binding 设为 Native，并确认原生 Renderer 未被禁用。
- [ ] 只有结构校验、全帧指标和关键帧图像校验全部通过后，Bake Pipeline 才原子更新 Binding 为 `GpuReady`。
- [ ] 人为删除一个子轨道、替换 Error Shader、写入 NaN、制造旧 Handle 回调，逐项确认校验器拒绝 GPU。
- [ ] 对允许差异制造轻微颜色/随机差异，确认不会出现质量分档或无理由拒绝。
- [ ] Commit: `feat(gpu-particle): gate GPU playback on hard-failure validation`

### Task 11: 完成一键窗口、批量增量烘焙与 Build 门禁

**Consumes:** 完整 Bake Pipeline 和 Validation Result。

**Produces:** 美术可用的一键工具。

**Files:**

- Create: `Packages/com.hlwd.gpu-particle/Editor/UI/GpuParticleBakerWindow.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/UI/GpuParticleMenu.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/Build/GpuParticleBuildValidator.cs`
- Create: `Packages/com.hlwd.gpu-particle/Editor/URP/GpuParticleUrpInstaller.cs`

- [ ] 实现拖拽 Prefab/目录、主按钮、二态结果和定位失败对象。
- [ ] 实现右键 Bake/Validate/Revert。
- [ ] 按完整 Bake Fingerprint 跳过未变资源；Schema、设置、Adapter、Camera、Unity/URP、Color Space 或 Graphics API 变化都强制重烘。
- [ ] 窗口显示 Renderer Feature 状态，并提供一键安装/修复；安装后重新保存 URP Renderer Data 并重跑验证。
- [ ] 实现可取消批处理和 `try/finally` 清理。
- [ ] Build 前扫描 `GpuReady` Binding：Clip 缺失/过期时让 Build 失败，并引导用户重烘或显式 `Revert To Native`。
- [ ] 让一名未参与开发者仅按 README 完成一次首批三个 Prefab 烘焙，记录所有需要额外解释的步骤并简化 UI。
- [ ] Commit: `feat(gpu-particle): add one-click baker and build validation`

### Task 12: 补齐包文档、Sample 和跨项目验证

**Consumes:** 已完成的公共 API 与 Editor 工作流。

**Produces:** 不依赖 DigitDoor 也能导入和使用的包。

**Files:**

- Modify: `Packages/com.hlwd.gpu-particle/package.json`
- Modify: `Packages/com.hlwd.gpu-particle/README.md`
- Modify: `Packages/com.hlwd.gpu-particle/CHANGELOG.md`
- Create: `Packages/com.hlwd.gpu-particle/Documentation~/workflow.md`
- Create: `Packages/com.hlwd.gpu-particle/Documentation~/architecture.md`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/README.md`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleSampleController.cs`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleBasic.unity`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleBillboard.prefab`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleMesh.prefab`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Prefabs/GpuParticleTrail.prefab`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleBillboard.mat`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleMesh.mat`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/Materials/GpuParticleTrail.mat`
- Create: `Packages/com.hlwd.gpu-particle/Samples~/Basic/GpuParticleSampleProfile.asset`

- [ ] README 用不超过 6 个步骤描述安装、打开窗口、拖入 Prefab、点击烘焙、看二态结果、运行。
- [ ] 文档明确首版平台、允许差异、硬失败、Native fallback 和扩展接口。
- [ ] Sample 演示 Play/Stop/Pause/Resume/SetTransform、循环和对象池重播。
- [ ] 在 `package.json.samples` 注册 Basic Sample，导入后场景、三个 Prefab、材质和 Profile 引用完整。
- [ ] 在一个最小 Unity 2022.3 + URP 14 项目中以本地 Package 导入，确认没有 DigitDoor/Ember/YooAsset/GPUI 依赖。
- [ ] 在该项目中烘焙并播放一个 Billboard、一个 Mesh、一个 Trail 示例。
- [ ] Commit: `docs(gpu-particle): document portable bake and playback workflow`

### Task 13: 接入 DigitDoor `EffectController` 后端

**Consumes:** `Hlwd.GpuParticle.Runtime` 公共 API；现有 `EffectController` 播放语义。

**Produces:** 不改配置即可让已绑定 Prefab 自动走 GPU。

**Files:**

- Modify: `Assets/Scripts_Hotfix/hlwd.game.asmdef`
- Create: `Assets/Scripts_Hotfix/Modules/Effect/Playback/IEffectPlaybackBackend.cs`
- Create: `Assets/Scripts_Hotfix/Modules/Effect/Playback/ParticleSystemEffectBackend.cs`
- Create: `Assets/Scripts_Hotfix/Modules/Effect/Playback/GpuParticleEffectBackend.cs`
- Modify: `Assets/Scripts_Hotfix/Modules/Effect/EffectController.cs`

项目侧接口：

```csharp
namespace Hlwd.Game
{
    internal interface IEffectPlaybackBackend
    {
        bool IsGpu { get; }
        bool TryPlay(float timeScale);
        void Stop();
        void SetParticleVisible(bool visible);
        void SyncTransform(Transform source);
        bool TryConsumeNativeFallbackRequest(
            out GpuParticleNativeRestoreState restoreState);
    }
}
```

- [ ] asmdef 显式加入 `Hlwd.GpuParticle.Runtime`，不改变 `autoReferenced: false`。
- [ ] 把现有 `RestartParticleSystems`、Particle Renderer 开关和停止行为搬入 `ParticleSystemEffectBackend`，保持结果等价；模型 Renderer/GPUI 可见性仍由 `EffectController` 管理。
- [ ] `GpuParticleEffectBackend.TryPlay` 只调用 `GpuParticlePlayer.TryPlayGpu`；返回 false 时由 `EffectController` 唯一切换到 Native backend，禁止包内再次播放原生系统。
- [ ] `EffectController.Update` 消费 GPU backend 的中途 fallback request，停止旧 Handle，再让 Native backend 按第 6.5 节用 Variant 逐系统 Seed、烘焙 `dt` 分步恢复到 `ElapsedClipTime`，最后应用 TimeScale/Pause；同一失败只消费一次，禁止一次性 `Simulate(elapsedTime)`。
- [ ] 保持 `EffectCtrl.Play` 现有 Parent 设置；`EffectController.Play` 在启动 Backend 前完成 Position/Scale/Rotation。
- [ ] 仅当 `effectType == cfg.EEffectType.World && binding.CanAttemptGpuPlayback` 时尝试 GPU；UI、SpineUI、SpineWorld 保持原路径。
- [ ] `EffectController.Update` 调用 `SyncPresentationTransform()`，由门面分别同步 GPU Particle 或现有 GPUI；不再把通用表现同步写死为 GPUI。
- [ ] GPUI 与 GPU Particle 不互斥：GPU Particle 只接管 Binding 中的 Particle Renderer；Prefab 内独立 `GPUIPrefab` 子节点仍执行现有注册、Transform 同步和注销，确保 Rocket 本体模型不消失。
- [ ] `EffectController.Hide` 先 Stop Backend，再取消 GPUI、归位并释放对象池。
- [ ] 压力测试的 `SkillParticlesVisible` 路由到 `SetParticleVisible`；`SkillModelsVisible` 保持现有模型 Renderer/GPUI 逻辑，两个开关不得合并。
- [ ] 增加 `SyncPresentationTransform()` 门面，不让战斗系统直接依赖包类型。
- [ ] 明确 `EffectController.leftTime` 是 GameObject 回池的唯一权威，继续按未乘 TimeScale 的 `Time.deltaTime` 倒计时；GPU Clip 播放结束只停止 Draw Handle，不得隐藏或释放 EffectController。
- [ ] Editor 下回归一个没有 Binding 的旧 Effect，确认播放、时间缩放、隐藏和回池行为不变。
- [ ] Commit: `feat(effect): support baked GPU particle backend`

### Task 14: 保持附着特效和对象池预热语义

**Consumes:** Task 13 `EffectController` 门面。

**Produces:** 跟随、停止、ReturnFirst 和首发无尖峰闭环。

**Files:**

- Modify: `Assets/Scripts_Hotfix/Game/Battle/Systems/Effect/BattleAttachedEffectSystem.cs`
- Modify: `Assets/Scripts_Hotfix/Game/Skill/SkillCtrl.cs`
- Create: `Assets/Scripts_Hotfix/Game/Skill/SkillCtrl.GpuParticlePrewarm.cs`

- [ ] 把 `effect.SyncGpuInstancerTransform()` 调整为 `effect.SyncPresentationTransform()`；所有每帧附着更新都调用同一门面。
- [ ] 保持现有 Follow Target、Caster、Skill Entity、Offset、Euler 和停止条件不变。
- [ ] 禁止通过 `GameObjectPool.Get` 借实例预热：它会 `BeginUsePoolItem` 激活 GameObject、触发 `playOnAwake/OnEnable`，池满时还可能拿到正在播放的 `ReturnFirst` 实例。
- [ ] `RegisterSkillLifecycleVFXEffectPools()` 注册池后，为每个唯一 `effectCfg.Res` 直接加载 Prefab Asset、读取 Binding 并持有资源/Buffer Lease：

```csharp
GameObject prefab = GameEntity.Loader.LoadWithHandler<GameObject>(
    effectCfg.Res,
    out AssetReleaseHandler assetHandler);
GpuParticleBinding binding = prefab != null
    ? prefab.GetComponent<GpuParticleBinding>()
    : null;

if (binding != null && binding.CanAttemptGpuPlayback)
{
    m_GpuParticleAssetHandlers.Add(assetHandler);
    m_GpuParticlePrewarmLeases.Add(
        GpuParticleRuntime.AcquirePrewarm(binding.Clip));
}
else
{
    assetHandler?.Release();
}
```

- [ ] 先收集唯一 `effectCfg.Res`，再分别执行“若无池则 AddNewPool”和 Asset Load/Acquire Lease；已有 Pool 不能因为原来的 `continue` 而跳过 GPU 预热。
- [ ] 预热只创建 Clip/Buffer，不播放声音、不触发特效、不修改战斗状态。
- [ ] `SkillCtrl.Dispose()` 先 Dispose 所有 `GpuParticlePrewarmLease`，再 Release 所有 `AssetReleaseHandler` 并清空集合；不得使用 `LoadAndNeverRelease` 永久钉住资源。
- [ ] 验证附着弹体移动时 GPU Transform 持续更新，销毁 Skill Entity 后按原 Stop Policy 停止。
- [ ] 验证 `MaxCount + ReturnFirst` 下旧播放 Handle 不干扰新实例。
- [ ] Commit: `feat(skill-vfx): prewarm and sync GPU particle effects`

### Task 15: 烘焙首批 DigitDoor 资源

**Consumes:** 完成的一键工具和 DigitDoor Adapter。

**Produces:** 首批可进包的生成资源与二态报告。

**Files:**

- Create: `Assets/Editor/GpuParticle/DigitDoorGpuParticleBakeProfile.cs`
- Create: `Assets/Editor/GpuParticle/DigitDoorUnlitEffectBase02ShaderAdapter.cs`
- Create: `Assets/Editor/GpuParticle/DigitDoorGpuParticleBuildGuard.cs`
- Modify: `Assets/Editor/Build/BuildWindow/BuildStepData.cs`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_BulletHit/SkillEntity_BulletHit.gpuparticle.asset`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_BulletHit/SkillEntity_BulletHit.gpuparticle.bytes`
- Create Generated when needed: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_BulletHit/SkillEntity_BulletHit_Geometry.asset`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_GrenadeExplosion/SkillEntity_GrenadeExplosion.gpuparticle.asset`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_GrenadeExplosion/SkillEntity_GrenadeExplosion.gpuparticle.bytes`
- Create Generated when needed: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_GrenadeExplosion/SkillEntity_GrenadeExplosion_Geometry.asset`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_Rocket/SkillEntity_Rocket.gpuparticle.asset`
- Create Generated: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_Rocket/SkillEntity_Rocket.gpuparticle.bytes`
- Create Generated when needed: `Assets/ResBundles/Generated/GpuParticle/SkillEntity_Rocket/SkillEntity_Rocket_Geometry.asset`
- Modify Binding only: `Assets/ResBundles/EffectRes/SkillEntity_BulletHit.prefab`
- Modify Binding only: `Assets/ResBundles/EffectRes/SkillEntity_GrenadeExplosion.prefab`
- Modify Binding only: `Assets/ResBundles/EffectRes/SkillEntity_Rocket.prefab`

- [ ] Profile 固定输出到 `Assets/ResBundles/Generated/GpuParticle/`；State Track 使用战斗 Camera 验证范围，Camera-dependent Geometry 只允许严格参考姿态。
- [ ] 为首批资源使用的 `Custom/UnlitEffectBase02` 实现显式属性/Keyword/Blend/Depth/Distortion 检查，并在生成目录写入/更新对应 ShaderVariantCollection；无法验证的分支返回 `UnsupportedShader`，不能无条件宣称适配。
- [ ] 在现有 `BuildAssetBundle` Step 调用 YooAsset 前执行 `DigitDoorGpuParticleBuildGuard.ValidateBeforeAssetBundleBuild()`；YooAsset 完成后读取 Build Map/Package Manifest，确认 Prefab -> Clip -> bytes/Mesh/RuntimeResources/Shader Variant 依赖全部进入包。
- [ ] 分别烘焙 BulletHit、GrenadeExplosion、Rocket。
- [ ] 对每项记录最终 `GPU 可用` 或 `保留原生` 及明确 FailureCode；不得为追求成功率放宽硬失败。
- [ ] 检查源 Prefab Diff，确认除 Binding 外没有粒子模块、材质或层级变化。
- [ ] 进入正式战斗链路触发三个 Effect，不通过单独 Debug Prefab 代替业务验收。
- [ ] 验证火箭弹/手雷本体、受击、爆炸、Trail、Follow、停止和池复用。
- [ ] Commit: `content(vfx): bake initial DigitDoor GPU particle clips`

### Task 16: Editor 压力与性能验收

**Consumes:** 首批 Clip 和现有压力测试玩法。

**Produces:** 性能、GC、正确性证据。

- [ ] 运行现有压力测试关卡，同时触发子弹、火箭弹、手雷及受击/爆炸效果，总活跃技能特效 50+。
- [ ] 分别采集 Native 与 GPU 路径的 Main Thread、Render Thread、GPU、Draw Calls、Batches、SetPass、GC Alloc 和显存。
- [ ] 两条路径都预热 10 秒，再采集连续 300 帧的中位数和 P95；保留同一摄像机、怪物数、技能频率和画质设置。
- [ ] 同 Clip 50+ 并发时，GPU 路径稳定帧 GC 必须为 0 B；GPU Particle 主线程中位耗时不高于 Native 的 60%，Draw Calls 不高于 Native 的 40%，总 GPU Frame Time 不得比 Native 恶化超过 10%。未达标视为性能目标未完成，不用表现正确替代性能验收。
- [ ] 确认技能命中、伤害、碰撞和生命周期统计完全不因渲染后端变化。
- [ ] 观察 5 分钟，确认 Buffer、Mesh、Material 和实例槽数量没有持续增长。
- [ ] 反复 Pause/Resume、切场景、回战斗，确认 Camera 回调不重复注册。
- [ ] 保存 Profiler 截图与校验报告到 `docs/reports/`，只写测量结果，不把未测项目标记通过。
- [ ] Commit: `perf(gpu-particle): validate pooled battle playback`

### Task 17: Android 整包与真机验收

**Consumes:** Editor 验收通过的内容。

**Produces:** Vulkan/GLES3 包内可见性和稳定性证据。

- Create: `Assets/Editor/GpuParticle/DigitDoorGpuParticleAndroidAcceptance.cs`

- [ ] 实现两个 Editor 入口：`BuildVulkanNotHotfix` 将 Graphics API 设为 Vulkan-only，`BuildGles3NotHotfix` 设为 OpenGLES3-only，再调用现有无热更自动构建。入口把原设置记录到 `Library`，并通过 `IPostprocessBuildWithReport` 在 Unity 导出结束后恢复，异常启动时也先恢复遗留记录。
- [ ] 关闭占用项目的 Unity Editor 后，分别执行两个入口。现有自动构建是可续跑流程，因此命令不能带 `-quit`；由项目自己的 AutoClose 在流程完成后退出：

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity"
"$UNITY" \
  -batchmode \
  -buildTarget Android \
  -projectPath "/Users/norman/Documents/UnityProjects/DigitDoor" \
  -executeMethod DigitDoorGpuParticleAndroidAcceptance.BuildVulkanNotHotfix \
  -logFile /tmp/digitdoor-gpu-particle-vulkan.log
```

- [ ] 从构建日志确认 Unity Android Project 导出成功，并确认输出为项目下 `TempBuild`；此时尚未生成最终 APK。
- [ ] Unity 步骤导出 `TempBuild` Gradle 工程后，使用 fail-fast 脚本删除旧 APK、构建并校验新文件时间：

```bash
set -euo pipefail
cd "/Users/norman/Documents/UnityProjects/DigitDoor/TempBuild"
APK="$PWD/launcher/build/outputs/apk/debug/launcher-debug.apk"
EXPORT_TIME="$(stat -f %m settings.gradle)"
rm -f "$APK"
./gradlew :launcher:assembleDebug
test -s "$APK"
test "$(stat -f %m "$APK")" -ge "$EXPORT_TIME"
mkdir -p "/Users/norman/Documents/UnityProjects/DigitDoor/Builds/GpuParticleAcceptance"
cp "$APK" "/Users/norman/Documents/UnityProjects/DigitDoor/Builds/GpuParticleAcceptance/DigitDoor-Vulkan.apk"
```

- [ ] 以 Unity AutoBuild 成功标记、Gradle `BUILD SUCCESSFUL`、非空 APK 和 APK 时间晚于本次导出同时成立作为整包成功证据。

- [ ] 检查连接设备并安装实际生成的 APK：

```bash
ADB="/Applications/Unity/Hub/Editor/2022.3.62f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
APK="/Users/norman/Documents/UnityProjects/DigitDoor/TempBuild/launcher/build/outputs/apk/debug/launcher-debug.apk"
"$ADB" devices -l
"$ADB" install -r "$APK"
```

- [ ] 清空 Logcat，启动游戏，进入正式战斗和压力测试关卡；`GpuParticleRuntime` 启动日志必须打印并断言 `SystemInfo.graphicsDeviceType == Vulkan`。
- [ ] Vulkan 验收完成后，使用同样流程调用 `DigitDoorGpuParticleAndroidAcceptance.BuildGles3NotHotfix`，输出 `DigitDoor-GLES3.apk`，安装后断言运行日志为 `OpenGLES3`。两种 API 必须各有独立 APK、Logcat 和验收记录，不能用一个包推断两条路径。
- [ ] 检查三个首批特效本体/爆炸/受击/Trail 均可见，无粉色、黑块、拉伸、闪烁和 Compute/Shader Error。
- [ ] 验证 50+ 并发、对象池复用、后台切回、暂停恢复和场景重进。
- [ ] 若任一资源包内硬失败，将其 Binding 改为 Native 并重新构建验证；禁止仅记录已知问题后保留 GPU 状态。
- [ ] Commit: `fix(gpu-particle): close Android device acceptance gaps`（仅在真机发现并修复问题时创建）。

### Task 18: 最终审查与交付

**Consumes:** 全部任务结果。

**Produces:** 可复用包、DigitDoor 接入和可核验交付说明。

- [ ] 执行 `git diff --check`。
- [ ] 确认 Package Runtime/Editor 依赖扫描无项目符号。
- [ ] 确认没有修改 `Packages/Hlwd-Framework`、Ember、GPUI 包和配置生成文件。
- [ ] 确认当前无关 dirty files 保持原样。
- [ ] Unity Console 无新增 Error；记录仍存在但与本改动无关的 Error。
- [ ] Package Sample、DigitDoor 正式战斗、压力关卡、Android 真机四层验收都有结果。
- [ ] 对照第 1 节逐项确认，不用“看起来正常”替代硬失败检查。
- [ ] 运行代码审查，重点检查资源释放、Domain Reload、旧 Handle、Camera 回调、Shader Strip 和 Native fallback。
- [ ] 最终提交：`feat(gpu-particle): deliver reusable baker and DigitDoor integration`

---

## 10. 风险与处理策略

| 风险 | 处理 |
|---|---|
| 任意 Shader 无法自动 GPU 化 | State/Geometry 都先通过 Shader Adapter 验证；无法验证直接整体 Native |
| 几何烘焙依赖摄像机 | 只允许严格匹配参考 View/Projection/相对姿态；可变视角直接 Native |
| 子发射器由碰撞/脚本动态触发 | 保留原生，或由业务先把触发拆成独立 VFX 后再烘焙 |
| 长特效/高采样率导致大资源 | 首版接受体积；超过最大时长不截断而是 Native |
| Shader Variant Strip 导致包内不可见 | Clip 生成显式 Shader/Material 引用，Build Validator + Android 真机门禁 |
| 首次播放上传 Buffer 尖峰 | DigitDoor 直接加载 Prefab Asset 读取 Clip，并持有可释放的 GPU Prewarm Lease；不激活对象池实例 |
| `ReturnFirst` 重用导致旧回调干扰 | Slot + Generation Handle，所有回调校验代次 |
| World/GraphicsBuffer 泄漏 | Subsystem/Scene/Application/LowMemory 全生命周期显式释放和 5 分钟稳定性验证 |
| 与 GPUI 重复接管 Renderer | GPU Particle 只关闭绑定的 Particle Renderer；同 Prefab 的 GPUI 模型继续独立注册和同步 |
| GPU 表现侵入战斗逻辑 | Package 只接收 Transform/Time，不访问 Ember/技能/命中/伤害数据 |

---

## 11. 明确不做

- 不在首版重写 Unity ParticleSystem 的所有模拟公式。
- 不承诺跨 Editor、Mali、Adreno 的逐像素 bitwise 一致。
- 不支持任意运行时脚本修改粒子模块后仍复用同一 Clip。
- 不修改技能命中、碰撞、伤害和 Skill Graph。
- 不把 GPU 粒子塞进 GPU Instancer Pro 的 Prefab/Crowd 注册流程。
- 不为了提升“转换成功率”关闭硬失败校验。
- 不在首版做半精度、曲线压缩、纹理图集重排或包体优化。
- 不为这个工具修改 Hlwd Framework 的对象池或 UI 框架。

---

## 12. 实施顺序与检查点

```text
Checkpoint A: Task 1-6
  能分析、采样并生成安全 Clip；尚未承诺 GPU 表现。

Checkpoint B: Task 7-10
  Runtime、两类 Renderer、Native fallback 和硬失败门禁闭环。

Checkpoint C: Task 11-12
  一键工具可由非作者使用，包能脱离 DigitDoor 导入。

Checkpoint D: Task 13-15
  DigitDoor 正式 VFX 链路接入，首批资源完成二态判定。

Checkpoint E: Task 16-18
  压力、Android 整包、资源释放和最终审查完成。
```

任何 Checkpoint 出现硬失败，优先保证 Native fallback 正常，再继续扩大 GPU 覆盖；不得用后续任务掩盖前一阶段的数据或生命周期缺陷。

---

## 13. Unity API 依据

- [`ParticleSystem.Simulate`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystem.Simulate.html)：Editor 中按固定步长推进原生粒子模拟。
- [`ParticleSystemRenderer.BakeMesh`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystemRenderer.BakeMesh.html)：捕获当前粒子最终几何。
- [`ParticleSystemRenderer.BakeTrailsMesh`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystemRenderer.BakeTrailsMesh.html)：捕获当前 Trail 几何。
- [`ParticleSystemRenderer.GetActiveVertexStreams`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystemRenderer.GetActiveVertexStreams.html)：分析材质实际依赖的顶点流。
- [`Mesh.GetVertexBuffer`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Mesh.GetVertexBuffer.html)：后续需要 Compute 直接写 Mesh Buffer 时的升级入口；首版状态轨道使用独立 `GraphicsBuffer`，几何轨道使用烘焙 Mesh。
