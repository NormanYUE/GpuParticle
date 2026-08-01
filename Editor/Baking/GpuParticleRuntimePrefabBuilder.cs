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

            // Load the source prefab contents into an isolated editing context (same technique
            // the old binding writer used). Modify directly inside this context, then save the
            // modified contents to a new prefab path. This avoids scene-instantiation quirks and
            // nested-prefab override issues.
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            if (root == null)
            {
                Debug.LogError($"[GpuParticle] Failed to load prefab contents from {sourcePath}.");
                return null;
            }

            try
            {
                StripParticleSystems(root);

                int boundCount = 0;
                for (int i = 0; i < bakedSystems.Count; i++)
                {
                    if (WriteSystemBinding(root, bakedSystems[i]))
                    {
                        boundCount++;
                    }
                }

                Debug.Log($"[GpuParticle] Runtime prefab built with {boundCount}/{bakedSystems.Count} system bindings.");

                EnsureGroupPlayer(root);

                GameObject runtimePrefab = PrefabUtility.SaveAsPrefabAsset(root, runtimePrefabPath);
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
                PrefabUtility.UnloadPrefabContents(root);
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

        private static bool WriteSystemBinding(GameObject root, BakedSystemEntry entry)
        {
            Transform targetTransform = FindTransform(root.transform, entry.TransformPath);
            if (targetTransform == null)
            {
                Debug.LogWarning(
                    $"[GpuParticle] Could not find transform '{entry.TransformPath}' in runtime prefab; " +
                    $"available children: {string.Join(", ", GetChildNames(root.transform))}. Skipping binding.");
                return false;
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
            Debug.Log($"[GpuParticle] Wrote binding to '{target.name}' at path '{entry.TransformPath}'.");
            return true;
        }

        private static Transform FindTransform(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return root;
            }

            return root.Find(path);
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
