# Non-destructive Runtime Prefab Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change the GPU particle baker so it creates a new `_Runtime.prefab` for GPU playback instead of modifying the original particle prefab.

**Architecture:** Add a `GpuParticleRuntimePrefabBuilder` that duplicates the original prefab, strips `ParticleSystem`/`ParticleSystemRenderer` components, adds `GpuParticleBinding` + `GpuParticlePlayer` per baked system, and saves the result as a new prefab. Update `GpuParticleBakePipeline` to call this builder, and adjust `GpuParticleValidationResult` + `RevertToNative` accordingly.

**Tech Stack:** C#, Unity Editor APIs (`PrefabUtility`, `AssetDatabase`, `Object.DestroyImmediate`), existing `GpuParticle.Editor` and `GpuParticle.Runtime` assemblies.

## Global Constraints
- Target framework: `netstandard2.1` for Runtime/Editor, `net9.0` for Tests.
- All new source files must include a `.meta` file (Unity package requirement).
- Do not modify the original prefab asset during or after baking.
- Keep existing public APIs where possible; add overloads rather than breaking changes.
- Build must produce 0 errors; warnings are acceptable only if pre-existing.

---

### Task 1: Extend `GpuParticleValidationResult` with runtime prefab reference

**Files:**
- Modify: `Editor/Validation/GpuParticleValidationResult.cs`

**Interfaces:**
- Consumes: existing constructor `(string prefabPath, GpuParticleBakeStatus status, GpuParticleFailure failure, GpuParticleClip? clip)`.
- Produces: new constructor overload accepting `GameObject? runtimePrefab`; new read-only property `RuntimePrefab`.

- [ ] **Step 1: Add `RuntimePrefab` property and constructor overload**

```csharp
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Editor
{
    public sealed class GpuParticleValidationResult
    {
        public GpuParticleValidationResult(
            string prefabPath,
            GpuParticleBakeStatus status,
            GpuParticleFailure failure,
            GpuParticleClip? clip)
            : this(prefabPath, status, failure, clip, null)
        {
        }

        public GpuParticleValidationResult(
            string prefabPath,
            GpuParticleBakeStatus status,
            GpuParticleFailure failure,
            GpuParticleClip? clip,
            GameObject? runtimePrefab)
        {
            PrefabPath = prefabPath;
            Status = status;
            Failure = failure;
            Clip = clip;
            RuntimePrefab = runtimePrefab;
        }

        public string PrefabPath { get; }
        public GpuParticleBakeStatus Status { get; }
        public GpuParticleFailure Failure { get; }
        public GpuParticleClip? Clip { get; }
        public GameObject? RuntimePrefab { get; }
        public bool IsGpuReady => Status == GpuParticleBakeStatus.GpuReady;
    }
}
```

- [ ] **Step 2: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Editor/Validation/GpuParticleValidationResult.cs
git commit -m "feat: add RuntimePrefab to GpuParticleValidationResult"
```

---

### Task 2: Add in-memory `WriteBinding` overload to `GpuParticleBindingWriter`

**Files:**
- Modify: `Editor/Baking/GpuParticleBindingWriter.cs`

**Interfaces:**
- Consumes: `GpuParticleBakeStatus`, `GpuParticleClip?`, native state arrays, target `GameObject`.
- Produces: `WriteBinding(GameObject target, ...)` overload used by the runtime prefab builder.

- [ ] **Step 1: Refactor path-based overload to share core logic**

Keep the existing path-based overloads for backward compatibility. Extract the component-add/configure logic into a private method that operates on an already-resolved `GameObject`.

```csharp
using System;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    internal static class GpuParticleBindingWriter
    {
        public static void WriteBinding(
            GameObject prefab,
            GpuParticleBakeStatus status,
            GpuParticleClip? clip,
            string failureCode)
        {
            WriteBinding(prefab, string.Empty, status, clip, failureCode, captureChildren: true);
        }

        public static void WriteBinding(
            GameObject prefab,
            string transformPath,
            GpuParticleBakeStatus status,
            GpuParticleClip? clip,
            string failureCode,
            bool captureChildren = false,
            bool addPlayer = false)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                GameObject target = ResolveTarget(root, transformPath);
                CaptureAndConfigure(
                    target,
                    status,
                    clip,
                    failureCode,
                    captureChildren,
                    addPlayer);
                EditorUtility.SetDirty(target);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void WriteBinding(
            GameObject target,
            GpuParticleBakeStatus status,
            GpuParticleClip? clip,
            string failureCode,
            GpuParticleNativeSystemState[] systemStates,
            GpuParticleNativeRendererState[] rendererStates,
            bool addPlayer = false)
        {
            ConfigureBinding(target, status, clip, failureCode, systemStates, rendererStates, addPlayer);
            EditorUtility.SetDirty(target);
        }

        private static GameObject ResolveTarget(GameObject root, string transformPath)
        {
            if (string.IsNullOrEmpty(transformPath))
            {
                return root;
            }

            Transform child = root.transform.Find(transformPath);
            return child != null ? child.gameObject : root;
        }

        private static void CaptureAndConfigure(
            GameObject target,
            GpuParticleBakeStatus status,
            GpuParticleClip? clip,
            string failureCode,
            bool captureChildren,
            bool addPlayer)
        {
            ParticleSystem[] systems;
            ParticleSystemRenderer[] renderers;
            if (captureChildren)
            {
                systems = target.GetComponentsInChildren<ParticleSystem>(true);
                renderers = target.GetComponentsInChildren<ParticleSystemRenderer>(true);
            }
            else
            {
                systems = target.GetComponents<ParticleSystem>();
                renderers = target.GetComponents<ParticleSystemRenderer>();
            }

            GpuParticleNativeSystemState[] systemStates = CaptureSystems(systems);
            GpuParticleNativeRendererState[] rendererStates = CaptureRenderers(renderers);
            ConfigureBinding(target, status, clip, failureCode, systemStates, rendererStates, addPlayer);
        }

        private static GpuParticleNativeSystemState[] CaptureSystems(ParticleSystem[] systems)
        {
            GpuParticleNativeSystemState[] systemStates = new GpuParticleNativeSystemState[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                GpuParticleNativeSystemState state = new GpuParticleNativeSystemState();
                state.Capture(systems[i]);
                systemStates[i] = state;
            }

            return systemStates;
        }

        private static GpuParticleNativeRendererState[] CaptureRenderers(ParticleSystemRenderer[] renderers)
        {
            GpuParticleNativeRendererState[] rendererStates = new GpuParticleNativeRendererState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                GpuParticleNativeRendererState state = new GpuParticleNativeRendererState();
                state.Capture(renderers[i]);
                rendererStates[i] = state;
            }

            return rendererStates;
        }

        private static void ConfigureBinding(
            GameObject target,
            GpuParticleBakeStatus status,
            GpuParticleClip? clip,
            string failureCode,
            GpuParticleNativeSystemState[] systemStates,
            GpuParticleNativeRendererState[] rendererStates,
            bool addPlayer)
        {
            GpuParticleBinding binding = target.GetComponent<GpuParticleBinding>();
            if (binding == null)
            {
                binding = target.AddComponent<GpuParticleBinding>();
            }

            if (addPlayer && target.GetComponent<GpuParticlePlayer>() == null)
            {
                target.AddComponent<GpuParticlePlayer>();
            }

            string clipPath = clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty;
            GpuParticleClip persistedClip = !string.IsNullOrEmpty(clipPath)
                ? AssetDatabase.LoadAssetAtPath<GpuParticleClip>(clipPath)
                : clip;
            binding.Configure(status, persistedClip, systemStates, rendererStates, failureCode);
        }
    }
}
```

- [ ] **Step 2: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Editor/Baking/GpuParticleBindingWriter.cs
git commit -m "refactor: add in-memory WriteBinding overload for runtime prefab builder"
```

---

### Task 3: Create `GpuParticleRuntimePrefabBuilder`

**Files:**
- Create: `Editor/Baking/GpuParticleRuntimePrefabBuilder.cs`
- Create: `Editor/Baking/GpuParticleRuntimePrefabBuilder.cs.meta`

**Interfaces:**
- Consumes: source `GameObject`, `GpuParticleBakerSettings`, `IReadOnlyList<BakedSystemEntry>`.
- Produces: `GameObject?` reference to the saved runtime prefab asset.

- [ ] **Step 1: Define `BakedSystemEntry` data structure**

Inside `GpuParticleRuntimePrefabBuilder.cs` (or a separate small file), define:

```csharp
namespace GpuParticle.Editor.Baking
{
    internal readonly struct BakedSystemEntry
    {
        public readonly string TransformPath;
        public readonly GpuParticle.Runtime.GpuParticleClip Clip;
        public readonly GpuParticle.Runtime.GpuParticleNativeSystemState[] SystemStates;
        public readonly GpuParticle.Runtime.GpuParticleNativeRendererState[] RendererStates;

        public BakedSystemEntry(
            string transformPath,
            GpuParticle.Runtime.GpuParticleClip clip,
            GpuParticle.Runtime.GpuParticleNativeSystemState[] systemStates,
            GpuParticle.Runtime.GpuParticleNativeRendererState[] rendererStates)
        {
            TransformPath = transformPath;
            Clip = clip;
            SystemStates = systemStates;
            RendererStates = rendererStates;
        }
    }
}
```

- [ ] **Step 2: Implement the builder class**

```csharp
using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    internal static class GpuParticleRuntimePrefabBuilder
    {
        public static GameObject? Build(
            GameObject sourcePrefab,
            GpuParticleBakerSettings settings,
            IReadOnlyList<BakedSystemEntry> bakedSystems)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[GpuParticle] Cannot build runtime prefab: source prefab has no asset path.");
                return null;
            }

            string folder = GetRuntimeFolder(sourcePrefab, settings);
            GpuParticleProjectSettings.EnsureFolder(folder);
            string runtimePrefabPath = $"{folder}/{sourcePrefab.name}_Runtime.prefab";

            DeleteAssetIfExists(runtimePrefabPath);

            GameObject sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            if (sourceInstance == null)
            {
                Debug.LogError("[GpuParticle] Failed to instantiate source prefab for runtime prefab creation.");
                return null;
            }

            try
            {
                StripParticleSystems(sourceInstance);

                for (int i = 0; i < bakedSystems.Count; i++)
                {
                    WriteSystemBinding(sourceInstance, bakedSystems[i]);
                }

                EnsureGroupPlayer(sourceInstance);

                GameObject runtimePrefab = PrefabUtility.SaveAsPrefabAsset(sourceInstance, runtimePrefabPath);
                if (runtimePrefab == null)
                {
                    Debug.LogError($"[GpuParticle] Failed to save runtime prefab at {runtimePrefabPath}.");
                    return null;
                }

                AssetDatabase.ImportAsset(runtimePrefabPath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(runtimePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(sourceInstance);
            }
        }

        private static string GetRuntimeFolder(GameObject prefab, GpuParticleBakerSettings settings)
        {
            return $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
        }

        private static void StripParticleSystems(GameObject root)
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = systems.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(systems[i]);
            }

            ParticleSystemRenderer[] renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(renderers[i]);
            }
        }

        private static void WriteSystemBinding(GameObject root, BakedSystemEntry entry)
        {
            Transform targetTransform = root.transform.Find(entry.TransformPath);
            if (targetTransform == null)
            {
                Debug.LogWarning($"[GpuParticle] Could not find transform '{entry.TransformPath}' in runtime prefab; skipping binding.");
                return;
            }

            GameObject target = targetTransform.gameObject;
            GpuParticleBindingWriter.WriteBinding(
                target,
                GpuParticleBakeStatus.GpuReady,
                entry.Clip,
                string.Empty,
                entry.SystemStates,
                entry.RendererStates,
                addPlayer: true);
        }

        private static void EnsureGroupPlayer(GameObject root)
        {
            if (root.GetComponent<GpuParticleGroupPlayer>() == null)
            {
                root.AddComponent<GpuParticleGroupPlayer>();
            }
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
```

- [ ] **Step 3: Create the `.meta` file**

Generate a new GUID and write:

```yaml
fileFormatVersion: 2
guid: <new-guid>
```

Use Unity or a fresh GUID. From terminal:
```bash
python3 -c "import uuid; print('fileFormatVersion: 2\nguid:', uuid.uuid4())" > Editor/Baking/GpuParticleRuntimePrefabBuilder.cs.meta
```

- [ ] **Step 4: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Editor/Baking/GpuParticleRuntimePrefabBuilder.cs Editor/Baking/GpuParticleRuntimePrefabBuilder.cs.meta
git commit -m "feat: add GpuParticleRuntimePrefabBuilder for non-destructive runtime prefabs"
```

---

### Task 4: Add native-state capture helper

**Files:**
- Modify: `Editor/Baking/GpuParticleBakePipeline.cs` (add private helper)

**Interfaces:**
- Consumes: a `GameObject` (preview instance) and a transform path.
- Produces: `(GpuParticleNativeSystemState[] systems, GpuParticleNativeRendererState[] renderers)`.

- [ ] **Step 1: Add capture helper near other private helpers**

```csharp
private static (GpuParticleNativeSystemState[] systems, GpuParticleNativeRendererState[] renderers)
    CaptureNativeStatesAtPath(GameObject root, string transformPath)
{
    GameObject target = root;
    if (!string.IsNullOrEmpty(transformPath))
    {
        Transform child = root.transform.Find(transformPath);
        if (child != null)
        {
            target = child.gameObject;
        }
    }

    ParticleSystem[] systems = target.GetComponents<ParticleSystem>();
    ParticleSystemRenderer[] renderers = target.GetComponents<ParticleSystemRenderer>();

    GpuParticleNativeSystemState[] systemStates = new GpuParticleNativeSystemState[systems.Length];
    for (int i = 0; i < systems.Length; i++)
    {
        GpuParticleNativeSystemState state = new GpuParticleNativeSystemState();
        state.Capture(systems[i]);
        systemStates[i] = state;
    }

    GpuParticleNativeRendererState[] rendererStates = new GpuParticleNativeRendererState[renderers.Length];
    for (int i = 0; i < renderers.Length; i++)
    {
        GpuParticleNativeRendererState state = new GpuParticleNativeRendererState();
        state.Capture(renderers[i]);
        rendererStates[i] = state;
    }

    return (systemStates, rendererStates);
}
```

- [ ] **Step 2: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Editor/Baking/GpuParticleBakePipeline.cs
git commit -m "feat: add helper to capture native particle states at transform path"
```

---

### Task 5: Rewrite `BakePrefab` to use the runtime prefab builder

**Files:**
- Modify: `Editor/Baking/GpuParticleBakePipeline.cs` lines 90-116

**Interfaces:**
- Consumes: `BakedSystemEntry` from single-system capture.
- Produces: `GpuParticleValidationResult` with `RuntimePrefab` set.

- [ ] **Step 1: Replace the binding-writing section of `BakePrefab`**

After `GpuParticleClip clip = WriteVatAssets(...)`, change from:

```csharp
GpuParticleBindingWriter.WriteBinding(prefab, GpuParticleBakeStatus.GpuReady, clip, string.Empty);
AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
return new GpuParticleValidationResult(prefabPath, GpuParticleBakeStatus.GpuReady, GpuParticleFailure.None, clip);
```

to:

```csharp
(GpuParticleNativeSystemState[] systemStates, GpuParticleNativeRendererState[] rendererStates) =
    CaptureRootNativeStates(instance);

BakedSystemEntry entry = new BakedSystemEntry(
    string.Empty,
    clip,
    systemStates,
    rendererStates);

GameObject? runtimePrefab = GpuParticleRuntimePrefabBuilder.Build(prefab, settings, new[] { entry });
if (runtimePrefab == null)
{
    report.Fail(GpuParticleFailureCode.RuntimeGpuFailure, "Failed to build runtime prefab.", prefabPath);
    return WriteNativeBinding(prefab, prefabPath, report.Failure);
}

return new GpuParticleValidationResult(
    prefabPath,
    GpuParticleBakeStatus.GpuReady,
    GpuParticleFailure.None,
    clip,
    runtimePrefab);
```

- [ ] **Step 2: Add `CaptureRootNativeStates` helper**

```csharp
private static (GpuParticleNativeSystemState[] systems, GpuParticleNativeRendererState[] renderers)
    CaptureRootNativeStates(GameObject instance)
{
    ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
    ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);

    GpuParticleNativeSystemState[] systemStates = new GpuParticleNativeSystemState[systems.Length];
    for (int i = 0; i < systems.Length; i++)
    {
        GpuParticleNativeSystemState state = new GpuParticleNativeSystemState();
        state.Capture(systems[i]);
        systemStates[i] = state;
    }

    GpuParticleNativeRendererState[] rendererStates = new GpuParticleNativeRendererState[renderers.Length];
    for (int i = 0; i < renderers.Length; i++)
    {
        GpuParticleNativeRendererState state = new GpuParticleNativeRendererState();
        state.Capture(renderers[i]);
        rendererStates[i] = state;
    }

    return (systemStates, rendererStates);
}
```

- [ ] **Step 3: Remove old `AddGroupPlayer` call for single-system path**

Single-system bakes no longer modify the original prefab, so there is nothing to add to it. `GpuParticleRuntimePrefabBuilder.Build` already adds `GpuParticleGroupPlayer` to the runtime prefab root.

- [ ] **Step 4: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Editor/Baking/GpuParticleBakePipeline.cs
git commit -m "feat: make BakePrefab generate a non-destructive runtime prefab"
```

---

### Task 6: Rewrite `BakePrefabGroup` to use the runtime prefab builder

**Files:**
- Modify: `Editor/Baking/GpuParticleBakePipeline.cs` lines 193-244

**Interfaces:**
- Consumes: `Dictionary<ParticleSystemRenderer, VatCaptureData>` and the preview `instance`.
- Produces: `GpuParticleValidationResult` with `RuntimePrefab` set.

- [ ] **Step 1: Collect baked system entries while iterating captures**

Replace the loop body (lines 195-234) so it builds a list of `BakedSystemEntry` instead of calling `WriteBinding` on the original prefab:

```csharp
List<BakedSystemEntry> entries = new List<BakedSystemEntry>();
int bakedCount = 0;
foreach (KeyValuePair<ParticleSystemRenderer, VatCaptureData> entry in captures)
{
    VatCaptureData data = entry.Value;
    GpuParticleVatTextureBuilder.Result textures = GpuParticleVatTextureBuilder.Build(data.Frames, data.MaxParticles);
    Bounds bounds = GpuParticleBoundsCalculator.Calculate(data.Frames);
    Mesh mesh = BuildVatMesh(data);
    Material material = CreateVatMaterial(data);

    string systemName = GetTransformPath(instance.transform, entry.Key.transform);
    if (string.IsNullOrEmpty(systemName))
    {
        systemName = entry.Key.gameObject.name;
    }

    GpuParticleClip clip = WriteVatAssets(
        prefab,
        settings,
        duration,
        loop,
        bounds,
        data,
        textures,
        mesh,
        material,
        systemName);

    if (clip != null)
    {
        (GpuParticleNativeSystemState[] systemStates, GpuParticleNativeRendererState[] rendererStates) =
            CaptureNativeStatesAtPath(instance, systemName);

        entries.Add(new BakedSystemEntry(
            systemName,
            clip,
            systemStates,
            rendererStates));
        bakedCount++;
    }
}
```

- [ ] **Step 2: Build runtime prefab after the loop**

Replace the final section (after the loop) with:

```csharp
if (bakedCount == 0)
{
    report.Fail(GpuParticleFailureCode.RuntimeGpuFailure, "Failed to write any VAT clip assets.", prefabPath);
    return WriteNativeBinding(prefab, prefabPath, report.Failure);
}

GameObject? runtimePrefab = GpuParticleRuntimePrefabBuilder.Build(prefab, settings, entries);
if (runtimePrefab == null)
{
    report.Fail(GpuParticleFailureCode.RuntimeGpuFailure, "Failed to build runtime prefab.", prefabPath);
    return WriteNativeBinding(prefab, prefabPath, report.Failure);
}

return new GpuParticleValidationResult(
    prefabPath,
    GpuParticleBakeStatus.GpuReady,
    GpuParticleFailure.None,
    null,
    runtimePrefab);
```

- [ ] **Step 3: Remove the old `AddGroupPlayer(prefab)` and `ImportAsset(prefabPath)` calls**

The original prefab is no longer modified, so these are no longer needed.

- [ ] **Step 4: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Editor/Baking/GpuParticleBakePipeline.cs
git commit -m "feat: make BakePrefabGroup generate a non-destructive runtime prefab"
```

---

### Task 7: Update `RevertToNative` to delete the runtime prefab

**Files:**
- Modify: `Editor/Baking/GpuParticleBakePipeline.cs` lines 285-294

**Interfaces:**
- Consumes: source `GameObject` prefab.
- Produces: deletes generated `_Runtime.prefab` if present.

- [ ] **Step 1: Replace `RevertToNative` body**

```csharp
public static void RevertToNative(GameObject prefab)
{
    if (prefab == null)
    {
        return;
    }

    GpuParticleBakerSettings settings = GpuParticleProjectSettings.LoadOrCreate();
    string folder = $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
    string runtimePrefabPath = $"{folder}/{prefab.name}_Runtime.prefab";
    if (AssetDatabase.LoadAssetAtPath<Object>(runtimePrefabPath) != null)
    {
        AssetDatabase.DeleteAsset(runtimePrefabPath);
    }
}
```

- [ ] **Step 2: Update menu label (optional but recommended)**

In `Editor/UI/GpuParticleMenu.cs`, change the menu item label from "Revert to Native" to "Clear Baked Runtime Prefab" or keep the old name if users rely on it. If changing, also update the log message.

- [ ] **Step 3: Build and verify no errors**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Editor/Baking/GpuParticleBakePipeline.cs Editor/UI/GpuParticleMenu.cs
git commit -m "feat: RevertToNative now deletes the generated runtime prefab"
```

---

### Task 8: Manual verification in Unity

**Files:**
- No file changes.

- [ ] **Step 1: Open Unity project**

- [ ] **Step 2: Select a particle prefab with one system and run "Bake GPU Particle"**

Verify:
- Original prefab asset is **not** modified (no new components, no asterisk in Inspector).
- `Assets/GpuParticleGenerated/<PrefabName>/<PrefabName>_Runtime.prefab` exists.
- Runtime prefab has `GpuParticleBinding` + `GpuParticlePlayer` on the root.
- Runtime prefab has no `ParticleSystem` or `ParticleSystemRenderer`.

- [ ] **Step 3: Select a prefab with nested particle systems and run "Bake GPU Particle Group"**

Verify:
- Original prefab is **not** modified.
- Runtime prefab mirrors the original hierarchy.
- Each baked system transform has `GpuParticleBinding` + `GpuParticlePlayer`.
- Root has `GpuParticleGroupPlayer`.
- `ParticleSystem` / `ParticleSystemRenderer` components are stripped.

- [ ] **Step 4: Run "Clear Baked Runtime Prefab" / "Revert to Native"**

Verify:
- `_Runtime.prefab` is deleted.
- Original prefab remains untouched.

- [ ] **Step 5: Commit a changelog entry (optional)**

If the project keeps a changelog, add an entry under `CHANGELOG.md`.

---

### Task 9: Build, test, and push

**Files:**
- All modified/new files.

- [ ] **Step 1: Full Release build**

Run:
```bash
dotnet build GpuParticle.sln -c Release
```

Expected: 0 errors.

- [ ] **Step 2: Run test suite**

Run:
```bash
Tests/bin/Release/net9.0/GpuParticle.Tests
```

Expected: `All tests passed.`

- [ ] **Step 3: Update develop package DLLs**

Copy fresh DLLs to the package worktree:
```bash
cp Runtime/bin/Release/netstandard2.1/GpuParticle.Runtime.dll .worktrees/develop/Runtime/GpuParticle.Runtime.dll
cp Editor/bin/Release/netstandard2.1/GpuParticle.Editor.dll .worktrees/develop/Editor/GpuParticle.Editor.dll
```

Then commit and push:
```bash
cd .worktrees/develop
git add -A
git commit -m "feat: update Runtime/Editor DLLs with non-destructive runtime prefab support"
git push origin develop
```

- [ ] **Step 4: Push main**

```bash
git push origin main
```

---

## Self-Review Checklist

- [ ] **Spec coverage:** Each section of `2026-07-31-non-destructive-runtime-prefab-design.md` is implemented by at least one task.
  - New asset layout → Task 3
  - Runtime prefab structure (strip, add components) → Task 3
  - Pipeline changes → Tasks 5, 6
  - `GpuParticleBindingWriter` overload → Task 2
  - `GpuParticleValidationResult.RuntimePrefab` → Task 1
  - Native state capture → Task 4
  - `RevertToNative` behavior change → Task 7
  - Edge cases → handled in Task 3 implementation
- [ ] **Placeholder scan:** No TBD/TODO/"implement later"/"handle edge cases" in the plan.
- [ ] **Type consistency:**
  - `GpuParticleRuntimePrefabBuilder.Build` returns `GameObject?`.
  - `BakedSystemEntry` fields match usage in Tasks 3, 5, 6.
  - `GpuParticleValidationResult` constructor overload accepts `GameObject? runtimePrefab`.
