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
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                GpuParticleBinding binding = root.GetComponent<GpuParticleBinding>();
                if (binding == null)
                {
                    binding = root.AddComponent<GpuParticleBinding>();
                }

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

                binding.Configure(status, clip, systemStates, rendererStates, failureCode);
                EditorUtility.SetDirty(binding);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
