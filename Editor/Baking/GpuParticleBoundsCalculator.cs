using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    public static class GpuParticleBoundsCalculator
    {
        public static Bounds Calculate(IReadOnlyList<GpuParticleBlobParticleState[]> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            for (int f = 0; f < frames.Count; f++)
            {
                GpuParticleBlobParticleState[] states = frames[f];
                if (states == null)
                {
                    continue;
                }

                for (int i = 0; i < states.Length; i++)
                {
                    if (!hasBounds)
                    {
                        bounds = new Bounds(states[i].Position, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(states[i].Position);
                    }
                }
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            // Add a small margin for size expansion
            bounds.extents = bounds.extents + Vector3.one * 0.5f;
            return bounds;
        }
    }
}
