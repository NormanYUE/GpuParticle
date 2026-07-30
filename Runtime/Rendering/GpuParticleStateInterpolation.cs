using UnityEngine;

namespace GpuParticle.Runtime
{
    internal static class GpuParticleStateInterpolation
    {
        public static GpuParticleBlobParticleState Lerp(
            in GpuParticleBlobParticleState a,
            in GpuParticleBlobParticleState b,
            float t)
        {
            return new GpuParticleBlobParticleState
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Velocity = Vector3.Lerp(a.Velocity, b.Velocity, t),
                Size = Mathf.Lerp(a.Size, b.Size, t),
                Rotation = Vector4.Lerp(a.Rotation, b.Rotation, t), // quaternion interpolation to be revised later
                Color = Color32.Lerp(a.Color, b.Color, t),
                Lifetime = Mathf.Lerp(a.Lifetime, b.Lifetime, t),
                Seed = a.Seed,
            };
        }
    }
}
