using System;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticlePrewarmLease : IDisposable
    {
        private GpuParticleClip clip = null!;
        private bool disposed;

        internal GpuParticlePrewarmLease(GpuParticleClip sourceClip)
        {
            clip = sourceClip;
        }

        public GpuParticleClip Clip => clip;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            GpuParticleRuntime.ReleasePrewarm(clip);
        }
    }
}
