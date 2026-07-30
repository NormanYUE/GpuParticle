using UnityEngine;

namespace GpuParticle.Runtime
{
    public static class GpuParticleRuntime
    {
        private static GpuParticleWorld world = null!;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            EnsureWorld();
        }

        public static GpuParticlePrewarmLease AcquirePrewarm(GpuParticleClip clip)
        {
            EnsureWorld().AcquireClip(clip);
            return new GpuParticlePrewarmLease(clip);
        }

        internal static void ReleasePrewarm(GpuParticleClip clip)
        {
            if (world != null)
            {
                world.ReleaseClip(clip);
            }
        }

        public static bool TryPlay(
            GpuParticleClip clip,
            GpuParticlePlayer owner,
            in GpuParticlePlayParams parameters,
            out GpuParticleHandle handle,
            out GpuParticleFailure failure)
        {
            return EnsureWorld().TryPlay(clip, owner, parameters, out handle, out failure);
        }

        public static bool Stop(GpuParticleHandle handle)
        {
            return world != null && world.Stop(handle);
        }

        public static bool SetPaused(GpuParticleHandle handle, bool paused)
        {
            return world != null && world.SetPaused(handle, paused);
        }

        public static bool SetTransform(GpuParticleHandle handle, Matrix4x4 localToWorld)
        {
            return world != null && world.SetTransform(handle, localToWorld);
        }

        private static GpuParticleWorld EnsureWorld()
        {
            if (world != null)
            {
                return world;
            }

            GameObject host = new GameObject("GpuParticleRuntime");
            host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(host);
            world = host.AddComponent<GpuParticleWorld>();
            return world;
        }
    }
}
