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
