using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    public static class GpuParticleVatTextureBuilder
    {
        public sealed class Result
        {
            public Texture2D PositionSize = null!;
            public Texture2D Color = null!;
            public Texture2D Rotation = null!;
            public Texture2D VelocityLifetime = null!;
        }

        public static Result Build(
            IReadOnlyList<GpuParticleBlobParticleState[]> frames,
            int maxParticles)
        {
            int frameCount = frames.Count;

            var posSizeTex = new Texture2D(maxParticles, frameCount, TextureFormat.RGBAFloat, false);
            var colorTex = new Texture2D(maxParticles, frameCount, TextureFormat.RGBA32, false);
            var rotTex = new Texture2D(maxParticles, frameCount, TextureFormat.RGBAHalf, false);
            var velTex = new Texture2D(maxParticles, frameCount, TextureFormat.RGBAHalf, false);

            var posSizeColors = new Color[maxParticles * frameCount];
            var colorColors = new Color32[maxParticles * frameCount];
            var rotColors = new Color[maxParticles * frameCount];
            var velColors = new Color[maxParticles * frameCount];

            for (int f = 0; f < frameCount; f++)
            {
                GpuParticleBlobParticleState[] states = frames[f];
                int count = Mathf.Min(states.Length, maxParticles);

                for (int i = 0; i < count; i++)
                {
                    int idx = f * maxParticles + i;
                    GpuParticleBlobParticleState s = states[i];

                    posSizeColors[idx] = new Color(s.Position.x, s.Position.y, s.Position.z, s.Size);
                    colorColors[idx] = s.Color;
                    rotColors[idx] = new Color(s.Rotation.x, s.Rotation.y, s.Rotation.z, s.Rotation.w);
                    velColors[idx] = new Color(s.Velocity.x, s.Velocity.y, s.Velocity.z, s.Lifetime);
                }

                for (int i = count; i < maxParticles; i++)
                {
                    int idx = f * maxParticles + i;
                    posSizeColors[idx] = new Color(0f, 0f, 0f, 0f);
                    colorColors[idx] = new Color32(0, 0, 0, 0);
                    rotColors[idx] = new Color(0f, 0f, 0f, 1f);
                    velColors[idx] = new Color(0f, 0f, 0f, 0f);
                }
            }

            posSizeTex.SetPixels(posSizeColors);
            colorTex.SetPixels32(colorColors);
            rotTex.SetPixels(rotColors);
            velTex.SetPixels(velColors);

            SetTextureImportSettings(posSizeTex);
            SetTextureImportSettings(colorTex);
            SetTextureImportSettings(rotTex);
            SetTextureImportSettings(velTex);

            posSizeTex.Apply(false, false);
            colorTex.Apply(false, false);
            rotTex.Apply(false, false);
            velTex.Apply(false, false);

            return new Result
            {
                PositionSize = posSizeTex,
                Color = colorTex,
                Rotation = rotTex,
                VelocityLifetime = velTex,
            };
        }

        private static void SetTextureImportSettings(Texture2D texture)
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
