using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    public static class GpuParticleMenu
    {
        [MenuItem("Tools/GPU Particle/Baker")]
        public static void OpenWindow()
        {
            GpuParticleBakerWindow.Open();
        }

        [MenuItem("Assets/GPU Particle/Bake Selected Prefabs", true)]
        [MenuItem("Tools/GPU Particle/Bake Selected Prefabs", true)]
        public static bool ValidateBakeSelected()
        {
            return GetSelectedPrefabs().Count > 0;
        }

        [MenuItem("Assets/GPU Particle/Bake Selected Prefabs")]
        [MenuItem("Tools/GPU Particle/Bake Selected Prefabs")]
        public static void BakeSelected()
        {
            List<GameObject> prefabs = GetSelectedPrefabs();
            GpuParticleBakerSettings settings = GpuParticleProjectSettings.LoadOrCreate();
            for (int i = 0; i < prefabs.Count; i++)
            {
                GpuParticleValidationResult result = GpuParticleBakePipeline.BakePrefabGroup(prefabs[i], settings);
                LogResult("Bake", result);
            }
        }

        [MenuItem("Assets/GPU Particle/Validate Selected Prefabs", true)]
        public static bool ValidateValidateSelected()
        {
            return GetSelectedPrefabs().Count > 0;
        }

        [MenuItem("Assets/GPU Particle/Validate Selected Prefabs")]
        public static void ValidateSelected()
        {
            List<GameObject> prefabs = GetSelectedPrefabs();
            for (int i = 0; i < prefabs.Count; i++)
            {
                LogResult("Validate", GpuParticleBakePipeline.ValidatePrefab(prefabs[i]));
            }
        }

        [MenuItem("Assets/GPU Particle/Clear Baked Runtime Prefab", true)]
        public static bool ValidateRevertSelected()
        {
            return GetSelectedPrefabs().Count > 0;
        }

        [MenuItem("Assets/GPU Particle/Clear Baked Runtime Prefab")]
        public static void RevertSelected()
        {
            List<GameObject> prefabs = GetSelectedPrefabs();
            for (int i = 0; i < prefabs.Count; i++)
            {
                GpuParticleBakePipeline.RevertToNative(prefabs[i]);
                Debug.Log($"GPU Particle Clear Baked Runtime Prefab: {AssetDatabase.GetAssetPath(prefabs[i])}");
            }
        }

        private static List<GameObject> GetSelectedPrefabs()
        {
            Object[] objects = Selection.objects;
            List<GameObject> prefabs = new List<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(objects[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                    for (int g = 0; g < guids.Length; g++)
                    {
                        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[g]);
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        if (prefab != null)
                        {
                            prefabs.Add(prefab);
                        }
                    }
                }
                else
                {
                    GameObject? prefab = objects[i] as GameObject;
                    if (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab)
                    {
                        prefabs.Add(prefab);
                    }
                }
            }

            return prefabs;
        }

        private static void LogResult(string action, GpuParticleValidationResult result)
        {
            if (result.IsGpuReady)
            {
                Debug.Log($"GPU Particle {action}: {result.PrefabPath} -> GPU 可用");
            }
            else
            {
                Debug.LogWarning($"GPU Particle {action}: {result.PrefabPath} -> 保留原生 ({result.Failure})");
            }
        }
    }
}
