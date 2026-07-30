using System;
using System.Collections.Generic;
using System.IO;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
            GpuParticleStateCollector stateCollector = new GpuParticleStateCollector();
            GpuParticleGeometryTrack[] tracks = CaptureGeometryTracks(
                instance,
                systems,
                renderers,
                previewScene.Camera,
                settings.SampleRate,
                duration,
                stateCollector,
                report);

            if (report.HasFailure || tracks.Length == 0)
            {
                GpuParticleFailure failure = report.HasFailure
                    ? report.Failure
                    : new GpuParticleFailure(GpuParticleFailureCode.MissingGeometry, "No visible particle geometry was captured.", prefabPath);
                return WriteNativeBinding(prefab, prefabPath, failure);
            }

            Bounds bounds = CalculateBounds(tracks);
            GpuParticleClip clip = WriteClipAsset(prefab, settings, duration, loop, bounds, tracks, stateCollector);
            GpuParticleBindingWriter.WriteBinding(prefab, GpuParticleBakeStatus.GpuReady, clip, string.Empty);
            return new GpuParticleValidationResult(prefabPath, GpuParticleBakeStatus.GpuReady, GpuParticleFailure.None, clip);
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

            GpuParticleClip? clip = prefab.GetComponent<GpuParticleBinding>()?.Clip;
            GpuParticleBindingWriter.WriteBinding(prefab, GpuParticleBakeStatus.Native, clip, "RevertedToNative");
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

        private static GpuParticleGeometryTrack[] CaptureGeometryTracks(
            GameObject root,
            ParticleSystem[] systems,
            ParticleSystemRenderer[] renderers,
            Camera camera,
            float sampleRate,
            float duration,
            GpuParticleStateCollector stateCollector,
            GpuParticleBakeReport report)
        {
            int frameCount = Mathf.CeilToInt(duration * sampleRate) + 1;
            ParticleSystemRenderer[] orderedRenderers = SortRenderersForPlayback(root.transform, renderers);
            List<GpuParticleGeometryTrack> tracks = new List<GpuParticleGeometryTrack>(orderedRenderers.Length);
            ParticleSystem[] rootSystems = GetRootSystems(root.transform, systems);
            float dt = 1f / sampleRate;

            for (int rendererIndex = 0; rendererIndex < orderedRenderers.Length; rendererIndex++)
            {
                ParticleSystemRenderer renderer = orderedRenderers[rendererIndex];
                if (renderer == null || renderer.renderMode == ParticleSystemRenderMode.None)
                {
                    continue;
                }

                GpuParticleRenderMode renderMode = MapRenderMode(renderer);
                GpuParticleAlignment alignment = MapAlignment(renderer);
                GpuParticleRendererRecipe rendererRecipe = BuildRendererRecipe(root.transform, renderer);
                GpuParticleMaterialRecipe[] materialRecipes = BuildMaterialRecipes(renderer);
                GpuParticleMaterialRecipe[] trailMaterialRecipes = BuildTrailMaterialRecipes(renderer);
                if (materialRecipes.Length == 0)
                {
                    report.Fail(GpuParticleFailureCode.UnsupportedShader, "Particle renderer has no material.", GetTransformPath(renderer.transform));
                    return Array.Empty<GpuParticleGeometryTrack>();
                }

                ParticleSystem system = renderer.GetComponent<ParticleSystem>();
                Mesh sharedMesh = renderMode == GpuParticleRenderMode.Mesh ? renderer.mesh : null!;
                List<GpuParticleGeometryFrame> frames = new List<GpuParticleGeometryFrame>(frameCount);
                bool visible = false;
                Dictionary<uint, List<GpuParticleBlobTrailState>> trailHistory = new Dictionary<uint, List<GpuParticleBlobTrailState>>();
                ResetAndPlay(rootSystems);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float time = frame * dt;
                    Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                    bool hasBounds = false;

                    int particleStateOffset = 0;
                    int particleCount = 0;
                    int meshTransformOffset = 0;
                    int meshTransformCount = 0;
                    int trailStateOffset = 0;
                    int trailCount = 0;

                    if (renderMode == GpuParticleRenderMode.Mesh)
                    {
                        GpuParticleBlobMeshTransform[] transforms = CaptureMeshTransforms(system, renderer, ref bounds, ref hasBounds);
                        meshTransformOffset = stateCollector.AppendMeshTransforms(transforms);
                        meshTransformCount = transforms.Length;
                    }
                    else
                    {
                        GpuParticleBlobParticleState[] states = CaptureParticleStates(system, renderer, renderMode, ref bounds, ref hasBounds);
                        particleStateOffset = stateCollector.AppendParticleStates(states);
                        particleCount = states.Length;
                    }

                    if (system != null && system.trails.enabled)
                    {
                        GpuParticleBlobTrailState[] trails = CaptureTrailStates(system, renderer, trailHistory, ref bounds, ref hasBounds);
                        trailStateOffset = stateCollector.AppendTrailStates(trails);
                        trailCount = trails.Length;
                    }

                    visible |= hasBounds;
                    GpuParticleGeometryFrame geometryFrame = new GpuParticleGeometryFrame();
                    geometryFrame.Configure(
                        time,
                        particleCount,
                        particleStateOffset,
                        meshTransformCount,
                        meshTransformOffset,
                        trailCount,
                        trailStateOffset,
                        hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero));
                    frames.Add(geometryFrame);

                    for (int systemIndex = 0; systemIndex < rootSystems.Length; systemIndex++)
                    {
                        rootSystems[systemIndex].Simulate(dt, true, false, false);
                    }
                }

                if (!visible)
                {
                    continue;
                }

                GpuParticleGeometryTrack track = new GpuParticleGeometryTrack();
                track.Configure(
                    GetTransformPath(root.transform, renderer.transform),
                    renderMode,
                    alignment,
                    rendererRecipe,
                    materialRecipes,
                    trailMaterialRecipes.Length > 0 ? trailMaterialRecipes : materialRecipes,
                    sharedMesh,
                    frames.ToArray(),
                    CalculateTrackBounds(frames));
                tracks.Add(track);
            }

            return tracks.ToArray();
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

        private static GpuParticleBlobParticleState[] CaptureParticleStates(
            ParticleSystem system,
            ParticleSystemRenderer renderer,
            GpuParticleRenderMode renderMode,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            if (system == null)
            {
                return Array.Empty<GpuParticleBlobParticleState>();
            }

            int maxParticles = system.main.maxParticles;
            var particles = new ParticleSystem.Particle[maxParticles];
            int count = system.GetParticles(particles);

            var states = new GpuParticleBlobParticleState[count];
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = particles[i];
                states[i] = new GpuParticleBlobParticleState
                {
                    Position = p.position,
                    Velocity = p.velocity,
                    Size = renderMode == GpuParticleRenderMode.StretchedBillboard
                        ? p.GetCurrentSize3D(renderer).x
                        : p.GetCurrentSize3D(renderer).x,
                    Rotation = new Vector4(p.rotation3D.x, p.rotation3D.y, p.rotation3D.z, 1f),
                    Color = p.GetCurrentColor(system),
                    Lifetime = 1f - p.remainingLifetime / Mathf.Max(p.startLifetime, 0.0001f),
                    Seed = p.randomSeed,
                };

                Vector3 size = Vector3.one * states[i].Size;
                Bounds particleBounds = new Bounds(states[i].Position, size);
                if (hasBounds)
                {
                    bounds.Encapsulate(particleBounds);
                }
                else
                {
                    bounds = particleBounds;
                    hasBounds = true;
                }
            }

            return states;
        }

        private static GpuParticleBlobMeshTransform[] CaptureMeshTransforms(
            ParticleSystem system,
            ParticleSystemRenderer renderer,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            if (system == null)
            {
                return Array.Empty<GpuParticleBlobMeshTransform>();
            }

            int maxParticles = system.main.maxParticles;
            var particles = new ParticleSystem.Particle[maxParticles];
            int count = system.GetParticles(particles);

            var transforms = new GpuParticleBlobMeshTransform[count];
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = particles[i];
                transforms[i] = new GpuParticleBlobMeshTransform
                {
                    Position = p.position,
                    Rotation = Quaternion.Euler(p.rotation3D),
                    Scale = Vector3.one * p.GetCurrentSize3D(renderer).x,
                    Color = p.GetCurrentColor(system),
                };

                Bounds transformBounds = new Bounds(transforms[i].Position, transforms[i].Scale);
                if (hasBounds)
                {
                    bounds.Encapsulate(transformBounds);
                }
                else
                {
                    bounds = transformBounds;
                    hasBounds = true;
                }
            }

            return transforms;
        }

        private static GpuParticleBlobTrailState[] CaptureTrailStates(
            ParticleSystem system,
            ParticleSystemRenderer renderer,
            Dictionary<uint, List<GpuParticleBlobTrailState>> history,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            if (system == null || !system.trails.enabled)
            {
                return Array.Empty<GpuParticleBlobTrailState>();
            }

            int maxParticles = system.main.maxParticles;
            var particles = new ParticleSystem.Particle[maxParticles];
            int count = system.GetParticles(particles);

            var result = new List<GpuParticleBlobTrailState>();
            for (int i = 0; i < count; i++)
            {
                uint id = particles[i].randomSeed;
                if (!history.TryGetValue(id, out var points))
                {
                    points = new List<GpuParticleBlobTrailState>();
                    history[id] = points;
                }

                points.Insert(0, new GpuParticleBlobTrailState
                {
                    Position = particles[i].position,
                    Width = system.trails.widthOverTrail.Evaluate(0f),
                    Color = particles[i].GetCurrentColor(system),
                    ParticleId = id,
                });

                int maxHistory = Mathf.CeilToInt(system.trails.lifetime * 120f) + 2;
                while (points.Count > maxHistory)
                {
                    points.RemoveAt(points.Count - 1);
                }

                for (int p = 0; p < points.Count; p++)
                {
                    result.Add(points[p]);
                    Bounds pointBounds = new Bounds(points[p].Position, Vector3.one * points[p].Width);
                    if (hasBounds)
                    {
                        bounds.Encapsulate(pointBounds);
                    }
                    else
                    {
                        bounds = pointBounds;
                        hasBounds = true;
                    }
                }
            }

            return result.ToArray();
        }

        private static Bounds CalculateTrackBounds(List<GpuParticleGeometryFrame> frames)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < frames.Count; i++)
            {
                Bounds frameBounds = frames[i].FrameLocalBounds;
                if (frameBounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = frameBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(frameBounds);
                }
            }

            return bounds;
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

        private static GpuParticleRendererRecipe BuildRendererRecipe(Transform root, ParticleSystemRenderer renderer)
        {
            int queue = 3000;
            Material material = renderer.sharedMaterial;
            if (material != null)
            {
                queue = material.renderQueue;
            }

            GpuParticleRendererRecipe recipe = new GpuParticleRendererRecipe();
            recipe.Configure(
                GetTransformPath(root, renderer.transform),
                renderer.gameObject.layer,
                renderer.sortingLayerID,
                renderer.sortingOrder,
                ComputeRendererPriority(renderer),
                queue,
                renderer.shadowCastingMode,
                renderer.receiveShadows);
            return recipe;
        }

        private static int ComputeRendererPriority(ParticleSystemRenderer renderer)
        {
            int layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
            long priority = (long)layerValue * 1000L + renderer.sortingOrder;
            if (priority > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (priority < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)priority;
        }

        private static int GetMaterialRenderQueue(ParticleSystemRenderer renderer)
        {
            Material material = renderer.sharedMaterial;
            return material == null ? 3000 : material.renderQueue;
        }

        private static GpuParticleMaterialRecipe[] BuildMaterialRecipes(ParticleSystemRenderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return Array.Empty<GpuParticleMaterialRecipe>();
            }

            GpuParticleMaterialRecipe[] recipes = new GpuParticleMaterialRecipe[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                GpuParticleMaterialRecipe recipe = new GpuParticleMaterialRecipe();
                recipe.Configure(materials[i], i, materials[i] != null && materials[i].enableInstancing);
                recipes[i] = recipe;
            }

            return recipes;
        }

        private static GpuParticleMaterialRecipe[] BuildTrailMaterialRecipes(ParticleSystemRenderer renderer)
        {
            ParticleSystem system = renderer.GetComponent<ParticleSystem>();
            if (system == null || !system.trails.enabled || renderer.trailMaterial == null)
            {
                return Array.Empty<GpuParticleMaterialRecipe>();
            }

            GpuParticleMaterialRecipe recipe = new GpuParticleMaterialRecipe();
            recipe.Configure(renderer.trailMaterial, 0, renderer.trailMaterial.enableInstancing);
            return new[] { recipe };
        }

        private static Bounds CalculateBounds(GpuParticleGeometryTrack[] tracks)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int t = 0; t < tracks.Length; t++)
            {
                Bounds trackBounds = tracks[t].LocalBounds;
                if (trackBounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = trackBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(trackBounds);
                }
            }

            return bounds;
        }

        private static GpuParticleClip WriteClipAsset(
            GameObject prefab,
            GpuParticleBakerSettings settings,
            float duration,
            bool loop,
            Bounds bounds,
            GpuParticleGeometryTrack[] tracks,
            GpuParticleStateCollector stateCollector)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            string folder = $"{settings.OutputRoot.TrimEnd('/')}/{prefab.name}";
            GpuParticleProjectSettings.EnsureFolder(folder);

            string clipPath = $"{folder}/{prefab.name}.gpuparticle.asset";
            string payloadPath = $"{folder}/{prefab.name}.gpuparticle.bytes";

            byte[] payloadBytes = stateCollector.CreateBlob(settings.SampleRate, duration, tracks.Length);
            File.WriteAllBytes(payloadPath, payloadBytes);
            AssetDatabase.ImportAsset(payloadPath, ImportAssetOptions.ForceSynchronousImport);
            TextAsset payload = AssetDatabase.LoadAssetAtPath<TextAsset>(payloadPath);

            if (AssetDatabase.LoadAssetAtPath<GpuParticleClip>(clipPath) != null)
            {
                AssetDatabase.DeleteAsset(clipPath);
            }

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
                payload,
                null,
                tracks);

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GpuParticleClip>(clipPath);
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
