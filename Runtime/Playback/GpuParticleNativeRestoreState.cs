namespace GpuParticle.Runtime
{
    public readonly struct GpuParticleNativeRestoreState
    {
        public GpuParticleNativeRestoreState(
            float elapsedClipTime,
            uint seedVariant,
            float timeScale,
            bool isPaused)
        {
            ElapsedClipTime = elapsedClipTime;
            SeedVariant = seedVariant;
            TimeScale = timeScale;
            IsPaused = isPaused;
        }

        public float ElapsedClipTime { get; }
        public uint SeedVariant { get; }
        public float TimeScale { get; }
        public bool IsPaused { get; }
    }
}
