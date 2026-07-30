using System;
using System.Collections.Generic;
using UnityEngine;

namespace GpuParticle.Runtime
{
    internal sealed class GpuParticleBufferCache : IDisposable
    {
        private readonly Dictionary<(GpuParticleClip clip, int frameIndex, int count), ComputeBuffer> buffers = new();

        public ComputeBuffer GetOrCreateParticleStateBuffer(GpuParticleClip clip, int frameIndex, int count)
        {
            var key = (clip, frameIndex, count);
            if (!buffers.TryGetValue(key, out var buffer) || buffer.count != count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(count, 64);
                buffers[key] = buffer;
            }

            return buffer;
        }

        public ComputeBuffer GetOrCreateTrailStateBuffer(GpuParticleClip clip, int frameIndex, int count)
        {
            var key = (clip, frameIndex, count + 1000000000); // offset key to avoid collision
            if (!buffers.TryGetValue(key, out var buffer) || buffer.count != count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(count, 32);
                buffers[key] = buffer;
            }

            return buffer;
        }

        public ComputeBuffer GetOrCreateMeshTransformBuffer(GpuParticleClip clip, int frameIndex, int count)
        {
            var key = (clip, frameIndex, count + 2000000000);
            if (!buffers.TryGetValue(key, out var buffer) || buffer.count != count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(count, 48);
                buffers[key] = buffer;
            }

            return buffer;
        }

        // Backward compatibility shims for callers that still use the old ref-counting API.
        // The new cache owns buffer lifecycle by key; explicit per-clip ref counting is no longer required.
        public void Acquire(GpuParticleClip clip)
        {
        }

        public void Release(GpuParticleClip clip)
        {
        }

        public void ClearUnused()
        {
        }

        public void Clear()
        {
            foreach (var buffer in buffers.Values)
            {
                buffer.Release();
            }

            buffers.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
