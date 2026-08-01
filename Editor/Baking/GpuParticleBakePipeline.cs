using System;
using System.Collections.Generic;
using System.IO;
using GpuParticle.Editor.Baking;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GpuParticle.Editor
{
    public static class GpuParticleBakePipeline
    {
        public static GpuParticleValidationResult BakePrefab(GameObject prefab)
        {
            return BakePrefab(prefab, GpuParticleProjectSettings.LoadOrCreate());
        }

        public static GpuParticleValidationResult BakePrefab(GameObject prefab, GpuParticleBakerSettings settings)
        {
            if (prefab == null)
            {
                return new GpuParticleValidationResult(
                    string.Empty,
                    GpuParticleBakeStatus.Native,
                    new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "No prefab supplied."),
                    null!);
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return new GpuParticleValidationResult(
                    prefabPath,
                    GpuParticleBakeStatus.Native,
                    new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "Selected object is not a prefab asset."),
                    null!);
            }

            GpuParticleBakeReport report = new GpuParticleBakeReport();
            using GpuParticlePreviewScene previewScene = new GpuParticlePreviewScene(settings);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene.Scene);
            if (instance == null)
            {
                report.Fail(GpuParticleFailureCode.NativeRequired, "Could not instantiate prefab in preview scene.", prefabPath);
                return NativeResult(prefabPath, report.Failure);
            }

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            if (systems.Length == 0 || renderers.Length == 0)
            {
                report.Fail(GpuParticleFailureCode.MissingGeometry, "Prefab contains no particle systems/renderers.", prefabPath);
                return NativeResult(prefabPath, report.Failure);
            }

            if (!AnalyzeSupported(systems, report))
            {
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            float duration = EstimateDuration(systems, settings.MaxDuration, out bool loop);
            if (duration <= 0f || duration > settings.MaxDuration)
            {
                report.Fail(
                    GpuParticleFailureCode.NativeRequired,
                    $"Estimated duration {duration:0.###} exceeds the configured max duration {settings.MaxDuration:0.###}.",
                    prefabPath);
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            PrepareSystems(systems);
            VatCaptureData? capture = CaptureVatData(
                instance,
                systems,
                renderers,
                settings.SampleRate,
                duration,
                report);

            if (report.HasFailure || !capture.HasValue)
            {
                GpuParticleFailure failure = report.HasFailure
                    ? report.Failure
                    : new GpuParticleFailure(GpuParticleFailureCode.MissingGeometry, "No visible particle geometry was captured.", prefabPath);
                return WriteNativeBinding(prefab, prefabPath, failure);
            }

            VatCaptureData data = capture.Value;
            GpuParticleVatTextureBuilder.Result textures = GpuParticleVatTextureBuilder.Build(data.Frames, data.MaxParticles);
            Bounds bounds = GpuParticleBoundsCalculator.Calculate(data.Frames);
            Mesh mesh = BuildVatMesh(data);
            Material material = CreateVatMaterial(data);

            GpuParticleClip clip = WriteVatAssets(
                prefab,
                settings,
                duration,
                loop,
                bounds,
                data,
                textures,
                mesh,
                material);

            if (clip == null)
            {
                report.Fail(GpuParticleFailureCode.RuntimeGpuFailure, "Failed to write VAT clip assets.", prefabPath);
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            (GpuParticleNativeSystemState[] systemStates, GpuParticleNativeRendererState[] rendererStates) =
                CaptureAllNativeStates(instance);

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
        }

        public static GpuParticleValidationResult BakePrefabGroup(GameObject prefab)
        {
            return BakePrefabGroup(prefab, GpuParticleProjectSettings.LoadOrCreate());
        }

        public static GpuParticleValidationResult BakePrefabGroup(GameObject prefab, GpuParticleBakerSettings settings)
        {
            if (prefab == null)
            {
                return new GpuParticleValidationResult(
                    string.Empty,
                    GpuParticleBakeStatus.Native,
                    new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "No prefab supplied."),
                    null!);
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return new GpuParticleValidationResult(
                    prefabPath,
                    GpuParticleBakeStatus.Native,
                    new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "Selected object is not a prefab asset."),
                    null!);
            }

            GpuParticleBakeReport report = new GpuParticleBakeReport();
            using GpuParticlePreviewScene previewScene = new GpuParticlePreviewScene(settings);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene.Scene);
            if (instance == null)
            {
                report.Fail(GpuParticleFailureCode.NativeRequired, "Could not instantiate prefab in preview scene.", prefabPath);
                return NativeResult(prefabPath, report.Failure);
            }

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            if (systems.Length == 0 || renderers.Length == 0)
            {
                report.Fail(GpuParticleFailureCode.MissingGeometry, "Prefab contains no particle systems/renderers.", prefabPath);
                return NativeResult(prefabPath, report.Failure);
            }

            if (!AnalyzeSupported(systems, report))
            {
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            float duration = EstimateDuration(systems, settings.MaxDuration, out bool loop);
            if (duration <= 0f || duration > settings.MaxDuration)
            {
                report.Fail(
                    GpuParticleFailureCode.NativeRequired,
                    $"Estimated duration {duration:0.###} exceeds the configured max duration {settings.MaxDuration:0.###}.",
                    prefabPath);
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            PrepareSystems(systems);
            Dictionary<ParticleSystemRenderer, VatCaptureData> captures = CaptureAllRenderers(
                instance,
                systems,
                renderers,
                settings.SampleRate,
                duration,
                report);

            if (captures.Count == 0)
            {
                GpuParticleFailure failure = report.HasFailure
                    ? report.Failure
                    : new GpuParticleFailure(GpuParticleFailureCode.MissingGeometry, "No visible particle geometry was captured.", prefabPath);
                return WriteNativeBinding(prefab, prefabPath, failure);
            }

            DeleteGeneratedFolder(prefab, settings);

            List<BakedSystemEntry> entries = new List<BakedSystemEntry>();
            int bakedCount = 0;
            foreach (KeyValuePair<ParticleSystemRenderer, VatCaptureData> entry in captures)
            {
                VatCaptureData data = entry.Value;
                GpuParticleVatTextureBuilder.Result textures = GpuParticleVatTextureBuilder.Build(data.Frames, data.MaxParticles);
                Bounds bounds = GpuParticleBoundsCalculator.Calculate(data.Frames);
                Mesh mesh = BuildVatMesh(data);
                Material material = CreateVatMaterial(data);

                string transformPath = GetTransformPath(instance.transform, entry.Key.transform);
                string systemName = string.IsNullOrEmpty(transformPath)
                    ? entry.Key.gameObject.name
                    : transformPath;

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
                        CaptureNativeStatesAtPath(instance, transformPath);

                    entries.Add(new BakedSystemEntry(
                        transformPath,
                        clip,
                        systemStates,
                        rendererStates));
                    bakedCount++;
                }
            }

            if (bakedCount == 0)
            {
                report.Fail(GpuParticleFailureCode.RuntimeGpuFailure, "Failed to write any VAT clip assets.", prefabPath);
                return WriteNativeBinding(prefab, prefabPath, report.Failure);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Debug.Log($"[GpuParticle] Baking runtime prefab entry {i}: path='{entries[i].TransformPath}', clip={entries[i].Clip?.name ?? "null"}");
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
        }

        public static GpuParticleValidationResult ValidatePrefab(GameObject prefab)
        {
            if (ReferenceEquals(prefab, null))
            {
                return NativeResult(
                    string.Empty,
                    new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "No prefab supplied."));
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GpuParticleBinding? binding = prefab.GetComponent<GpuParticleBinding>();
            if (binding == null || !binding.CanAttemptGpuPlayback)
            {
                return NativeResult(
                    prefabPath,
                    new GpuParticleFailure(GpuParticleFailureCode.ClipNative, "Prefab has no GPU-ready binding."));
            }

            if (!binding.Clip.TryValidateRuntime(out GpuParticleFailure failure))
            {
                return NativeResult(prefabPath, failure);
            }

            GpuParticleBakerSettings settings = GpuParticleProjectSettings.LoadOrCreate();
            string sourceHash = GpuParticleSourceHasher.ComputePrefabHash(prefab);
            string fingerprint = GpuParticleSourceHasher.ComputeFingerprint(prefab, settings);
            if (binding.Clip.SourceContentHash != sourceHash || binding.Clip.BakeFingerprint != fingerprint)
            {
                return NativeResult(
                    prefabPath,
                    new GpuParticleFailure(
                        GpuParticleFailureCode.StaleBakeFingerprint,
                        "Prefab source, dependencies or bake settings changed after this clip was generated."));
            }

            return new GpuParticleValidationResult(prefabPath, GpuParticleBakeStatus.GpuReady, GpuParticleFailure.None, binding.Clip);
        }

        public static void RevertToNative(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            GpuParticleBakerSettings settings = GpuParticleProjectSettings.LoadOrCreate();
            string folder = $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
            string runtimePrefabPath = $"{folder}/{prefab.name}_Runtime.prefab";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(runtimePrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(runtimePrefabPath);
            }
        }

        private static void DeleteGeneratedFolder(GameObject prefab, GpuParticleBakerSettings settings)
        {
            string folder = $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void AddGroupPlayer(GameObject prefab)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<GpuParticleGroupPlayer>() == null)
                {
                    root.AddComponent<GpuParticleGroupPlayer>();
                }

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GpuParticleValidationResult WriteNativeBinding(
            GameObject prefab,
            string prefabPath,
            GpuParticleFailure failure)
        {
            GpuParticleBindingWriter.WriteBinding(prefab, GpuParticleBakeStatus.Native, null, failure.Code.ToString());
            return NativeResult(prefabPath, failure);
        }

        private static GpuParticleValidationResult NativeResult(string prefabPath, GpuParticleFailure failure)
        {
            return new GpuParticleValidationResult(prefabPath, GpuParticleBakeStatus.Native, failure, null!);
        }

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

        private static (GpuParticleNativeSystemState[] systems, GpuParticleNativeRendererState[] renderers)
            CaptureAllNativeStates(GameObject root)
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);

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

        private static bool AnalyzeSupported(ParticleSystem[] systems, GpuParticleBakeReport report)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                ParticleSystem.CollisionModule collision = system.collision;
                if (collision.enabled)
                {
                    report.Fail(GpuParticleFailureCode.DynamicWorldInput, "Collision module depends on runtime world input.", GetTransformPath(system.transform));
                    return false;
                }

                ParticleSystem.TriggerModule trigger = system.trigger;
                if (trigger.enabled)
                {
                    report.Fail(GpuParticleFailureCode.DynamicWorldInput, "Trigger module depends on runtime world input.", GetTransformPath(system.transform));
                    return false;
                }

                ParticleSystem.ExternalForcesModule externalForces = system.externalForces;
                if (externalForces.enabled)
                {
                    report.Fail(GpuParticleFailureCode.DynamicWorldInput, "External Forces module depends on runtime world input.", GetTransformPath(system.transform));
                    return false;
                }
            }

            return true;
        }

        private static void PrepareSystems(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                system.useAutoRandomSeed = false;
                if (system.randomSeed == 0)
                {
                    system.randomSeed = (uint)(1009 + i * 7919);
                }
            }

            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].Clear(true);
            }
        }

        private static float EstimateDuration(ParticleSystem[] systems, float maxDuration, out bool loop)
        {
            loop = false;
            float duration = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                loop |= main.loop;
                float systemDuration = ReadCurveMax(main.startDelay) + main.duration + ReadCurveMax(main.startLifetime);
                duration = Mathf.Max(duration, systemDuration);
            }

            if (loop)
            {
                return Mathf.Min(Mathf.Max(duration, 1f), maxDuration);
            }

            return duration;
        }

        private static float ReadCurveMax(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return curve.constantMax;
                case ParticleSystemCurveMode.Curve:
                    return curve.curveMultiplier;
                case ParticleSystemCurveMode.TwoCurves:
                    return curve.curveMultiplier;
                default:
                    return 0f;
            }
        }

        private readonly struct VatCaptureData
        {
            public readonly GpuParticleRenderMode RenderMode;
            public readonly GpuParticleAlignment Alignment;
            public readonly Material SourceMaterial;
            public readonly Mesh SourceMesh;
            public readonly int MaxParticles;
            public readonly IReadOnlyList<GpuParticleBlobParticleState[]> Frames;

            public VatCaptureData(
                GpuParticleRenderMode renderMode,
                GpuParticleAlignment alignment,
                Material sourceMaterial,
                Mesh sourceMesh,
                int maxParticles,
                IReadOnlyList<GpuParticleBlobParticleState[]> frames)
            {
                RenderMode = renderMode;
                Alignment = alignment;
                SourceMaterial = sourceMaterial;
                SourceMesh = sourceMesh;
                MaxParticles = maxParticles;
                Frames = frames;
            }
        }

        private static VatCaptureData? CaptureVatData(
            GameObject root,
            ParticleSystem[] systems,
            ParticleSystemRenderer[] renderers,
            float sampleRate,
            float duration,
            GpuParticleBakeReport report)
        {
            Dictionary<ParticleSystemRenderer, VatCaptureData> allData = CaptureAllRenderers(
                root, systems, renderers, sampleRate, duration, report);

            if (allData.Count == 0)
            {
                report.Fail(GpuParticleFailureCode.MissingGeometry, "No visible particle geometry was captured.", AssetDatabase.GetAssetPath(root));
                return null;
            }

            ParticleSystemRenderer[] orderedRenderers = SortRenderersForPlayback(root.transform, renderers);
            foreach (ParticleSystemRenderer renderer in orderedRenderers)
            {
                if (renderer != null && allData.TryGetValue(renderer, out VatCaptureData data))
                {
                    return data;
                }
            }

            report.Fail(GpuParticleFailureCode.MissingGeometry, "No supported particle renderer found.", AssetDatabase.GetAssetPath(root));
            return null;
        }

        private static Dictionary<ParticleSystemRenderer, VatCaptureData> CaptureAllRenderers(
            GameObject root,
            ParticleSystem[] systems,
            ParticleSystemRenderer[] renderers,
            float sampleRate,
            float duration,
            GpuParticleBakeReport report)
        {
            var result = new Dictionary<ParticleSystemRenderer, VatCaptureData>();
            var validRenderers = new List<ParticleSystemRenderer>();
            var framesPerRenderer = new Dictionary<ParticleSystemRenderer, List<GpuParticleBlobParticleState[]>>();

            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null || renderer.renderMode == ParticleSystemRenderMode.None)
                {
                    continue;
                }

                if (renderer.GetComponent<ParticleSystem>() == null)
                {
                    continue;
                }

                validRenderers.Add(renderer);
                framesPerRenderer[renderer] = new List<GpuParticleBlobParticleState[]>();
            }

            if (validRenderers.Count == 0)
            {
                report.Fail(GpuParticleFailureCode.MissingGeometry, "No supported particle renderer found.", AssetDatabase.GetAssetPath(root));
                return result;
            }

            int frameCount = Mathf.CeilToInt(duration * sampleRate) + 1;
            float dt = 1f / sampleRate;
            ParticleSystem[] rootSystems = GetRootSystems(root.transform, systems);

            ResetAndPlay(rootSystems);
            for (int frame = 0; frame < frameCount; frame++)
            {
                for (int rendererIndex = 0; rendererIndex < validRenderers.Count; rendererIndex++)
                {
                    ParticleSystemRenderer renderer = validRenderers[rendererIndex];
                    ParticleSystem system = renderer.GetComponent<ParticleSystem>();
                    int maxParticles = system.main.maxParticles;
                    GpuParticleBlobParticleState[] states = CaptureParticleStates(system, maxParticles);
                    framesPerRenderer[renderer].Add(states);
                }

                for (int systemIndex = 0; systemIndex < rootSystems.Length; systemIndex++)
                {
                    rootSystems[systemIndex].Simulate(dt, true, false, false);
                }
            }

            for (int i = 0; i < validRenderers.Count; i++)
            {
                ParticleSystemRenderer renderer = validRenderers[i];
                List<GpuParticleBlobParticleState[]> frames = framesPerRenderer[renderer];
                bool visible = false;
                for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    if (frames[frameIndex].Length > 0)
                    {
                        visible = true;
                        break;
                    }
                }

                if (!visible)
                {
                    continue;
                }

                ParticleSystem system = renderer.GetComponent<ParticleSystem>();
                GpuParticleRenderMode renderMode = MapRenderMode(renderer);
                GpuParticleAlignment alignment = MapAlignment(renderer);
                Material sourceMaterial = renderer.sharedMaterial;
                Mesh sourceMesh = renderMode == GpuParticleRenderMode.Mesh ? renderer.mesh : null!;
                if (renderMode == GpuParticleRenderMode.Mesh && sourceMesh == null)
                {
                    continue;
                }

                result[renderer] = new VatCaptureData(
                    renderMode,
                    alignment,
                    sourceMaterial,
                    sourceMesh,
                    system.main.maxParticles,
                    frames);
            }

            return result;
        }

        private static GpuParticleBlobParticleState[] CaptureParticleStates(ParticleSystem system, int maxParticles)
        {
            if (system == null || maxParticles <= 0)
            {
                return Array.Empty<GpuParticleBlobParticleState>();
            }

            var particles = new ParticleSystem.Particle[maxParticles];
            int count = system.GetParticles(particles);
            if (count <= 0)
            {
                return Array.Empty<GpuParticleBlobParticleState>();
            }

            Array.Sort(particles, 0, count, Comparer<ParticleSystem.Particle>.Create((a, b) => a.randomSeed.CompareTo(b.randomSeed)));

            Transform systemTransform = system.transform;
            Quaternion systemRotationInv = Quaternion.Inverse(systemTransform.rotation);
            var states = new GpuParticleBlobParticleState[count];
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = particles[i];
                Quaternion rotation = Quaternion.Euler(p.rotation3D);
                Quaternion localRotation = systemRotationInv * rotation;
                states[i] = new GpuParticleBlobParticleState
                {
                    Position = systemTransform.InverseTransformPoint(p.position),
                    Velocity = systemTransform.InverseTransformDirection(p.velocity),
                    Size = p.GetCurrentSize3D(system).x,
                    Rotation = new Vector4(localRotation.x, localRotation.y, localRotation.z, localRotation.w),
                    Color = p.GetCurrentColor(system),
                    Lifetime = 1f - p.remainingLifetime / Mathf.Max(p.startLifetime, 0.0001f),
                    Seed = p.randomSeed,
                };
            }

            return states;
        }

        private static GpuParticleRenderMode MapRenderMode(ParticleSystemRenderer renderer)
        {
            return renderer.renderMode switch
            {
                ParticleSystemRenderMode.Billboard => GpuParticleRenderMode.Billboard,
                ParticleSystemRenderMode.Stretch => GpuParticleRenderMode.StretchedBillboard,
                ParticleSystemRenderMode.Mesh => GpuParticleRenderMode.Mesh,
                _ => GpuParticleRenderMode.Billboard,
            };
        }

        private static GpuParticleAlignment MapAlignment(ParticleSystemRenderer renderer)
        {
            return renderer.alignment switch
            {
                ParticleSystemRenderSpace.View => GpuParticleAlignment.View,
                ParticleSystemRenderSpace.Facing => GpuParticleAlignment.Facing,
                ParticleSystemRenderSpace.World => GpuParticleAlignment.World,
                ParticleSystemRenderSpace.Local => GpuParticleAlignment.Local,
                _ => GpuParticleAlignment.View,
            };
        }

        private static Mesh BuildVatMesh(VatCaptureData data)
        {
            if (data.RenderMode == GpuParticleRenderMode.Mesh && data.SourceMesh != null)
            {
                return GpuParticleVatMeshBuilder.BuildFromSource(data.SourceMesh, data.MaxParticles);
            }

            return GpuParticleVatMeshBuilder.Build(data.MaxParticles);
        }

        private static Material CreateVatMaterial(VatCaptureData data)
        {
            string shaderName = data.RenderMode switch
            {
                GpuParticleRenderMode.StretchedBillboard => "GpuParticle/VatStretch",
                GpuParticleRenderMode.Mesh => "GpuParticle/VatMesh",
                _ => "GpuParticle/VatBillboard",
            };

            Shader shader = Shader.Find(shaderName);
            Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));

            if (data.SourceMaterial != null)
            {
                Texture mainTex = data.SourceMaterial.GetTexture("_MainTex");
                if (mainTex != null)
                {
                    material.SetTexture("_MainTex", mainTex);
                }

                material.renderQueue = data.SourceMaterial.renderQueue;
            }

            if (data.RenderMode == GpuParticleRenderMode.StretchedBillboard)
            {
                material.SetFloat("_StretchScale", 0.1f);
            }

            if (data.RenderMode == GpuParticleRenderMode.Billboard || data.RenderMode == GpuParticleRenderMode.StretchedBillboard)
            {
                string keyword = data.Alignment switch
                {
                    GpuParticleAlignment.Facing => "ALIGNMENT_FACING",
                    GpuParticleAlignment.World => "ALIGNMENT_WORLD",
                    GpuParticleAlignment.Local => "ALIGNMENT_LOCAL",
                    _ => "ALIGNMENT_VIEW",
                };
                material.EnableKeyword(keyword);
            }

            return material;
        }

        private static GpuParticleClip WriteVatAssets(
            GameObject prefab,
            GpuParticleBakerSettings settings,
            float duration,
            bool loop,
            Bounds bounds,
            VatCaptureData data,
            GpuParticleVatTextureBuilder.Result textures,
            Mesh mesh,
            Material material)
        {
            return WriteVatAssets(prefab, settings, duration, loop, bounds, data, textures, mesh, material, string.Empty);
        }

        private static GpuParticleClip WriteVatAssets(
            GameObject prefab,
            GpuParticleBakerSettings settings,
            float duration,
            bool loop,
            Bounds bounds,
            VatCaptureData data,
            GpuParticleVatTextureBuilder.Result textures,
            Mesh mesh,
            Material material,
            string systemName)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            string safeSystemName = SanitizeFileName(systemName);
            bool hasSystemName = !string.IsNullOrEmpty(safeSystemName);

            string folder = hasSystemName
                ? $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}/{safeSystemName}"
                : $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
            GpuParticleProjectSettings.EnsureFolder(folder);

            string filePrefix = hasSystemName ? safeSystemName : prefab.name;
            string clipPath = $"{folder}/{filePrefix}.gpuparticle.asset";
            string vatPrefabPath = $"{folder}/{filePrefix}_VAT.prefab";
            string legacyPayloadPath = $"{folder}/{filePrefix}.gpuparticle.bytes";

            if (AssetDatabase.LoadAssetAtPath<GpuParticleClip>(clipPath) != null)
            {
                AssetDatabase.DeleteAsset(clipPath);
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(legacyPayloadPath) != null)
            {
                AssetDatabase.DeleteAsset(legacyPayloadPath);
            }

            DeleteAssetIfExists(vatPrefabPath);

            string posPath = $"{folder}/{filePrefix}_PositionSize.asset";
            string colorPath = $"{folder}/{filePrefix}_Color.asset";
            string rotPath = $"{folder}/{filePrefix}_Rotation.asset";
            string velPath = $"{folder}/{filePrefix}_VelocityLifetime.asset";
            string meshPath = $"{folder}/{filePrefix}_VATMesh.asset";
            string materialPath = $"{folder}/{filePrefix}_VATMaterial.mat";

            DeleteAssetIfExists(posPath);
            DeleteAssetIfExists(colorPath);
            DeleteAssetIfExists(rotPath);
            DeleteAssetIfExists(velPath);
            DeleteAssetIfExists(meshPath);
            DeleteAssetIfExists(materialPath);

            AssetDatabase.CreateAsset(textures.PositionSize, posPath);
            AssetDatabase.CreateAsset(textures.Color, colorPath);
            AssetDatabase.CreateAsset(textures.Rotation, rotPath);
            AssetDatabase.CreateAsset(textures.VelocityLifetime, velPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            ImportAssetIfExists(posPath);
            ImportAssetIfExists(colorPath);
            ImportAssetIfExists(rotPath);
            ImportAssetIfExists(velPath);
            ImportAssetIfExists(meshPath);
            ImportAssetIfExists(materialPath);

            GpuParticleClip clip = ScriptableObject.CreateInstance<GpuParticleClip>();
            AssetDatabase.CreateAsset(clip, clipPath);
            clip.Configure(
                prefabGuid,
                GpuParticleSourceHasher.ComputePrefabHash(prefab),
                GpuParticleSourceHasher.ComputeFingerprint(prefab, settings),
                GpuParticleBakeStatus.GpuReady,
                duration,
                settings.SampleRate,
                loop,
                bounds,
                GpuParticleCapability.ComputePlayback,
                null!,
                null,
                Array.Empty<GpuParticleGeometryTrack>());
            AssetDatabase.SaveAssets();

            Mesh persistedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Material persistedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            GameObject vatPrefab = GpuParticlePrefabBuilder.Build(vatPrefabPath, persistedMesh, persistedMaterial, clip);

            clip.ConfigureVat(
                vatPrefab,
                duration,
                data.Frames.Count,
                data.MaxParticles,
                bounds,
                textures.PositionSize,
                textures.Color,
                textures.Rotation,
                textures.VelocityLifetime);

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
            return clip;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '/' || c == '\\' || System.Array.IndexOf(invalid, c) >= 0)
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void ImportAssetIfExists(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        private static ParticleSystemRenderer[] SortRenderersForPlayback(
            Transform root,
            ParticleSystemRenderer[] renderers)
        {
            ParticleSystemRenderer[] ordered = (ParticleSystemRenderer[])renderers.Clone();
            Array.Sort(
                ordered,
                (left, right) =>
                {
                    if (left == right)
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return 1;
                    }

                    if (right == null)
                    {
                        return -1;
                    }

                    int compare = SortingLayer.GetLayerValueFromID(left.sortingLayerID)
                        .CompareTo(SortingLayer.GetLayerValueFromID(right.sortingLayerID));
                    if (compare != 0)
                    {
                        return compare;
                    }

                    compare = left.sortingOrder.CompareTo(right.sortingOrder);
                    if (compare != 0)
                    {
                        return compare;
                    }

                    compare = GetMaterialRenderQueue(left).CompareTo(GetMaterialRenderQueue(right));
                    if (compare != 0)
                    {
                        return compare;
                    }

                    return string.Compare(
                        GetTransformPath(root, left.transform),
                        GetTransformPath(root, right.transform),
                        StringComparison.Ordinal);
                });
            return ordered;
        }

        private static ParticleSystem[] GetRootSystems(Transform prefabRoot, ParticleSystem[] systems)
        {
            List<ParticleSystem> roots = new List<ParticleSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                Transform parent = system.transform.parent;
                bool parentHasParticleSystem = false;
                while (parent != null && parent != prefabRoot.parent)
                {
                    if (parent.GetComponent<ParticleSystem>() != null)
                    {
                        parentHasParticleSystem = true;
                        break;
                    }

                    if (parent == prefabRoot)
                    {
                        break;
                    }

                    parent = parent.parent;
                }

                if (!parentHasParticleSystem)
                {
                    roots.Add(system);
                }
            }

            return roots.ToArray();
        }

        private static void ResetAndPlay(ParticleSystem[] rootSystems)
        {
            for (int i = 0; i < rootSystems.Length; i++)
            {
                rootSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rootSystems[i].Clear(true);
                rootSystems[i].Play(true);
            }
        }

        private static int GetMaterialRenderQueue(ParticleSystemRenderer renderer)
        {
            Material material = renderer.sharedMaterial;
            return material == null ? 3000 : material.renderQueue;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            return transform.parent == null
                ? transform.name
                : GetTransformPath(transform.parent) + "/" + transform.name;
        }

        private static string GetTransformPath(Transform root, Transform transform)
        {
            if (root == null || transform == null || transform == root)
            {
                return string.Empty;
            }

            Stack<string> parts = new Stack<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts.ToArray());
        }
    }
}
