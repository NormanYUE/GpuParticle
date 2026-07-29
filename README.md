# HLWD GPU Particle / HLWD GPU 粒子

## English

HLWD GPU Particle is a reusable Unity package for baking selected `ParticleSystem` prefabs into GPU-playable geometry clips while preserving deterministic Native fallback.

This precompiled package contains:

- `Runtime/GpuParticle.Runtime.dll`: runtime clip, binding, player, fallback and geometry playback APIs.
- `Editor/GpuParticle.Editor.dll`: editor baker window, project settings, prefab binding writer and validation entry points.
- `package.json`: Unity Package Manager metadata for Unity 2022.3 and URP 14.

### Quick Start

1. Add this folder as a local package in Unity Package Manager.
2. Open `Tools/GPU Particle/Baker`.
3. Drag one or more prefab assets or folders into the window.
4. Click `分析并烘焙`.
5. Each prefab is marked as either `GPU 可用` or `保留原生`.
6. At runtime, use `GpuParticle.Runtime.GpuParticlePlayer` or read `GpuParticleBinding` from the prefab to decide whether GPU playback can be attempted.

### Current Scope

Version `0.1.0` implements the safe geometry playback path:

- Editor sampling runs the original Unity particle system in an isolated preview scene.
- Mesh and Trail output is captured through `ParticleSystemRenderer.BakeMesh` and `BakeTrailsMesh`.
- Runtime playback draws baked geometry frames with the original materials and preserves particle sorting layer/order as a renderer-priority approximation.
- Billboard, Stretched Billboard, and camera-facing Mesh renderers can be baked as camera-constrained geometry. At runtime, if the active camera does not match the bake profile, playback requests Native fallback instead of drawing the wrong result.
- Prefabs with unsupported runtime-world inputs such as Collision, Trigger or External Forces are marked `保留原生`.
- Payload headers are CRC-checked before playback. Missing or stale data returns to Native fallback.

The Compute/state-track renderer described in the implementation plan is represented by runtime contracts and capability flags, but is not enabled in this DLL build yet. Camera-constrained geometry is a conservative bridge for fixed-camera effects.

## 中文

HLWD GPU 粒子是一个可复用 Unity 包，用于把选中的 `ParticleSystem` Prefab 烘焙为可由 GPU 绘制的几何 Clip，同时保留确定性的原生回退路径。

这个预编译包包含：

- `Runtime/GpuParticle.Runtime.dll`：运行时 Clip、Binding、Player、回退和几何回放 API。
- `Editor/GpuParticle.Editor.dll`：编辑器烘焙窗口、项目设置、Prefab Binding 写入和校验入口。
- `package.json`：面向 Unity 2022.3 与 URP 14 的 UPM 元数据。

### 快速开始

1. 在 Unity Package Manager 中以 local package 方式添加本目录。
2. 打开 `Tools/GPU Particle/Baker`。
3. 把 Prefab 资源或文件夹拖入窗口。
4. 点击 `分析并烘焙`。
5. 每个 Prefab 只会显示 `GPU 可用` 或 `保留原生`。
6. 运行时可使用 `GpuParticle.Runtime.GpuParticlePlayer`，或读取 Prefab 上的 `GpuParticleBinding` 判断是否尝试 GPU 播放。

### 当前范围

`0.1.0` 实现了安全几何回放路径：

- Editor 在隔离 Preview Scene 中运行原始 Unity 粒子。
- 通过 `ParticleSystemRenderer.BakeMesh` 与 `BakeTrailsMesh` 捕获 Mesh 和 Trail 输出。
- Runtime 使用原材质绘制烘焙出的几何帧，并用 renderer-priority 近似保留粒子的 Sorting Layer/Order。
- Billboard、Stretched Billboard 和摄像机朝向 Mesh 可以烘焙为“摄像机约束几何”。运行时如果当前摄像机与烘焙 Profile 不匹配，会请求 Native 回退，不会错误绘制。
- Collision、Trigger、External Forces 等依赖运行时世界输入的 Prefab 会标记为 `保留原生`。
- 播放前会校验 Payload Header 与 CRC；缺失或过期数据会回退 Native。

实施计划中的 Compute/state-track 渲染器已经保留运行时契约和 Capability 标记，但当前 DLL 构建尚未启用。摄像机约束几何是面向固定摄像机特效的保守过渡路径。
