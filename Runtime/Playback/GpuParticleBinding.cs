using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class GpuParticleBinding : MonoBehaviour
    {
        [SerializeField] private GpuParticleBakeStatus status = GpuParticleBakeStatus.Native;
        [SerializeField] private GpuParticleClip clip = null!;
        [SerializeField] private GpuParticleNativeSystemState[] nativeSystemStates = Array.Empty<GpuParticleNativeSystemState>();
        [SerializeField] private GpuParticleNativeRendererState[] nativeRendererStates = Array.Empty<GpuParticleNativeRendererState>();
        [SerializeField] private string lastFailureCode = string.Empty;

        public GpuParticleBakeStatus Status => status;
        public GpuParticleClip Clip => clip;
        public GpuParticleNativeSystemState[] NativeSystemStates => nativeSystemStates;
        public GpuParticleNativeRendererState[] NativeRendererStates => nativeRendererStates;
        public string LastFailureCode => lastFailureCode;
        public bool CanAttemptGpuPlayback => status == GpuParticleBakeStatus.GpuReady && clip != null;

        public void Configure(
            GpuParticleBakeStatus bakeStatus,
            GpuParticleClip? particleClip,
            GpuParticleNativeSystemState[] systemStates,
            GpuParticleNativeRendererState[] rendererStates,
            string failureCode)
        {
            status = bakeStatus;
            clip = particleClip == null ? null! : particleClip;
            nativeSystemStates = systemStates ?? Array.Empty<GpuParticleNativeSystemState>();
            nativeRendererStates = rendererStates ?? Array.Empty<GpuParticleNativeRendererState>();
            lastFailureCode = failureCode ?? string.Empty;
        }

        public void MarkRuntimeFailure(GpuParticleFailure failure)
        {
            lastFailureCode = failure.Code.ToString();
        }

        public void SuppressNativeRenderers()
        {
            for (int i = 0; i < nativeRendererStates.Length; i++)
            {
                ParticleSystemRenderer renderer = nativeRendererStates[i].Renderer;
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            for (int i = 0; i < nativeSystemStates.Length; i++)
            {
                ParticleSystem system = nativeSystemStates[i].System;
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Clear(true);
                }
            }
        }

        public void RestoreNativeState()
        {
            for (int i = 0; i < nativeSystemStates.Length; i++)
            {
                GpuParticleNativeSystemState state = nativeSystemStates[i];
                ParticleSystem system = state.System;
                if (system == null)
                {
                    continue;
                }

                system.gameObject.SetActive(state.GameObjectActive);
                system.useAutoRandomSeed = state.UseAutoRandomSeed;
                system.randomSeed = state.RandomSeed;
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = state.PlayOnAwake;
                main.simulationSpeed = state.SimulationSpeed;
            }

            for (int i = 0; i < nativeRendererStates.Length; i++)
            {
                GpuParticleNativeRendererState state = nativeRendererStates[i];
                ParticleSystemRenderer renderer = state.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                renderer.gameObject.SetActive(state.GameObjectActive);
                renderer.enabled = state.Enabled;
            }
        }
    }
}
