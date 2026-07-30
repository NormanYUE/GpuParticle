using System;
using System.Collections.Generic;

namespace GpuParticle.Runtime
{
    internal sealed class GpuParticleBufferCache : IDisposable
    {
        private readonly Dictionary<GpuParticleClip, int> refCounts = new Dictionary<GpuParticleClip, int>();

        public void Acquire(GpuParticleClip clip)
        {
            if (clip == null)
            {
                return;
            }

            refCounts.TryGetValue(clip, out int count);
            refCounts[clip] = count + 1;
        }

        public void Release(GpuParticleClip clip)
        {
            if (clip == null || !refCounts.TryGetValue(clip, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                refCounts.Remove(clip);
            }
            else
            {
                refCounts[clip] = count - 1;
            }
        }

        public void ClearUnused()
        {
        }

        public void Dispose()
        {
            refCounts.Clear();
        }
    }
}
