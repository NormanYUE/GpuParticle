using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public static class GpuParticleStateInterpolation
    {
        public static GpuParticleBlobParticleState Lerp(
            GpuParticleBlobParticleState a,
            GpuParticleBlobParticleState b,
            float t)
        {
            return new GpuParticleBlobParticleState
            {
                Position = LerpVector3(a.Position, b.Position, t),
                Velocity = LerpVector3(a.Velocity, b.Velocity, t),
                Size = LerpFloat(a.Size, b.Size, t),
                Rotation = LerpRotation(a.Rotation, b.Rotation, t),
                Color = LerpColor32(a.Color, b.Color, t),
                Lifetime = LerpFloat(a.Lifetime, b.Lifetime, t),
                Seed = a.Seed,
            };
        }

        private static float LerpFloat(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static Vector3 LerpVector3(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(
                LerpFloat(a.x, b.x, t),
                LerpFloat(a.y, b.y, t),
                LerpFloat(a.z, b.z, t));
        }

        private static Vector4 LerpRotation(Vector4 a, Vector4 b, float t)
        {
            // Normalized quaternion linear interpolation (nlerp).
            // Avoids Unity engine internal calls so this works in plain .NET tests.
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            if (dot < 0f)
            {
                b = -b;
                dot = -dot;
            }

            Vector4 result = t <= 0f ? a : t >= 1f ? b : a + (b - a) * t;
            float magnitude = MathF.Sqrt(result.x * result.x + result.y * result.y + result.z * result.z + result.w * result.w);
            if (magnitude > 0f)
            {
                result /= magnitude;
            }

            return result;
        }

        private static Color32 LerpColor32(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(int)MathF.Round(LerpFloat(a.r, b.r, t)),
                (byte)(int)MathF.Round(LerpFloat(a.g, b.g, t)),
                (byte)(int)MathF.Round(LerpFloat(a.b, b.b, t)),
                (byte)(int)MathF.Round(LerpFloat(a.a, b.a, t)));
        }
    }
}
