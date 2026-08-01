using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    internal readonly struct BakedSystemEntry
    {
        public readonly string TransformPath;
        public readonly GpuParticleClip Clip;
        public readonly GpuParticleNativeSystemState[] SystemStates;
        public readonly GpuParticleNativeRendererState[] RendererStates;

        public BakedSystemEntry(
            string transformPath,
            GpuParticleClip clip,
            GpuParticleNativeSystemState[] systemStates,
            GpuParticleNativeRendererState[] rendererStates)
        {
            TransformPath = transformPath;
            Clip = clip;
            SystemStates = systemStates;
            RendererStates = rendererStates;
        }
    }

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

            // Use GameObject.Instantiate instead of PrefabUtility.InstantiatePrefab so the copy is a
            // plain hierarchy, not a prefab instance connected to the source. This avoids nested-prefab
            // override issues when we strip components and add GPU playback components.
            GameObject sourceInstance = Object.Instantiate(sourcePrefab);
            if (sourceInstance == null)
            {
                Debug.LogError("[GpuParticle] Failed to instantiate source prefab for runtime prefab creation.");
                return null;
            }

            sourceInstance.name = sourcePrefab.name;

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
            Transform targetTransform = FindTransform(root.transform, entry.TransformPath);
            if (targetTransform == null)
            {
                Debug.LogWarning(
                    $"[GpuParticle] Could not find transform '{entry.TransformPath}' in runtime prefab; " +
                    $"available children: {string.Join(", ", GetChildNames(root.transform))}. Skipping binding.");
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

        private static Transform FindTransform(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return root;
            }

            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string part in parts)
            {
                Transform child = FindChildByName(current, part);
                if (child == null)
                {
                    return null;
                }

                current = child;
            }

            return current;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            // Transform.Find only searches active children; this scans all children.
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static string[] GetChildNames(Transform parent)
        {
            int childCount = parent.childCount;
            string[] names = new string[childCount];
            for (int i = 0; i < childCount; i++)
            {
                names[i] = parent.GetChild(i).name;
            }

            return names;
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
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
