using UnityEngine;

namespace GpuParticle.Runtime
{
    public readonly struct GpuParticlePlayParams
    {
        public GpuParticlePlayParams(
            Matrix4x4 localToWorld,
            float timeScale = 1f,
            bool loop = false,
            uint seedVariant = uint.MaxValue)
        {
            LocalToWorld = localToWorld;
            TimeScale = timeScale;
            Loop = loop;
            SeedVariant = seedVariant;
        }

        public Matrix4x4 LocalToWorld { get; }
        public float TimeScale { get; }
        public bool Loop { get; }
        public uint SeedVariant { get; }
    }
}
