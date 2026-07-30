using GpuParticle.Runtime;

namespace GpuParticle.Editor
{
    public sealed class GpuParticleValidationResult
    {
        public GpuParticleValidationResult(
            string prefabPath,
            GpuParticleBakeStatus status,
            GpuParticleFailure failure,
            GpuParticleClip? clip)
        {
            PrefabPath = prefabPath;
            Status = status;
            Failure = failure;
            Clip = clip;
        }

        public string PrefabPath { get; }
        public GpuParticleBakeStatus Status { get; }
        public GpuParticleFailure Failure { get; }
        public GpuParticleClip? Clip { get; }
        public bool IsGpuReady => Status == GpuParticleBakeStatus.GpuReady;
    }
}
