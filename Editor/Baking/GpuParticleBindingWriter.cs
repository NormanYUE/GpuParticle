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
                GameObject target = root;
                if (!string.IsNullOrEmpty(transformPath))
                {
                    Transform child = root.transform.Find(transformPath);
                    if (child != null)
                    {
                        target = child.gameObject;
                    }
                }

                GpuParticleBinding binding = target.GetComponent<GpuParticleBinding>();
                if (binding == null)
                {
                    binding = target.AddComponent<GpuParticleBinding>();
                }

                if (addPlayer && target.GetComponent<GpuParticlePlayer>() == null)
                {
                    target.AddComponent<GpuParticlePlayer>();
                }

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

                string clipPath = clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty;
                GpuParticleClip persistedClip = !string.IsNullOrEmpty(clipPath)
                    ? AssetDatabase.LoadAssetAtPath<GpuParticleClip>(clipPath)
                    : clip;
                binding.Configure(status, persistedClip, systemStates, rendererStates, failureCode);
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
