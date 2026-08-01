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
