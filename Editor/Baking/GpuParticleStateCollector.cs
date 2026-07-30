using System;
using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Editor
{
    internal sealed class GpuParticleStateCollector
    {
        private readonly List<GpuParticleBlobParticleState> particleStates = new();
        private readonly List<GpuParticleBlobTrailState> trailStates = new();
        private readonly List<GpuParticleBlobMeshTransform> meshTransforms = new();

        public GpuParticleBlobParticleState[] ParticleStates => particleStates.ToArray();
        public GpuParticleBlobTrailState[] TrailStates => trailStates.ToArray();
        public GpuParticleBlobMeshTransform[] MeshTransforms => meshTransforms.ToArray();

        public int AppendParticleStates(GpuParticleBlobParticleState[] states)
        {
            int offset = particleStates.Count;
            particleStates.AddRange(states);
            return offset;
        }

        public int AppendTrailStates(GpuParticleBlobTrailState[] states)
        {
            int offset = trailStates.Count;
            trailStates.AddRange(states);
            return offset;
        }

        public int AppendMeshTransforms(GpuParticleBlobMeshTransform[] transforms)
        {
            int offset = meshTransforms.Count;
            meshTransforms.AddRange(transforms);
            return offset;
        }

        public byte[] CreateBlob(float sampleRate, float duration, int trackCount)
        {
            return GpuParticleBlobWriter.CreateBlob(
                ParticleStates,
                TrailStates,
                MeshTransforms,
                sampleRate,
                duration,
                trackCount);
        }
    }
}
