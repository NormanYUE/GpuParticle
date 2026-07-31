using UnityEngine;

namespace GpuParticle.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GpuParticlePlayer : MonoBehaviour
    {
        [SerializeField] private GpuParticleBinding binding = null!;

        private GpuParticleHandle handle = GpuParticleHandle.Invalid;

        public bool IsPlaying => handle.IsValid;
        public bool IsUsingGpu => handle.IsValid;

        private GpuParticleBinding CurrentBinding
        {
            get
            {
                if (binding == null)
                {
                    binding = GetComponent<GpuParticleBinding>();
                }

                return binding;
            }
        }

        public GpuParticleHandle Play(in GpuParticlePlayParams parameters)
        {
            GpuParticleBinding current = CurrentBinding;
            if (current == null || current.Clip == null || current.Clip.Prefab == null)
            {
                if (current != null)
                {
                    GpuParticleNativeFallback.Play(current, parameters.TimeScale, parameters.SeedVariant);
                }
                return GpuParticleHandle.Invalid;
            }

            Stop(clear: true);
            current.SuppressNativeRenderers();

            int slot = GpuParticleVatRenderSystem.Instance.Register(
                current.Clip,
                parameters.LocalToWorld,
                parameters.TimeScale,
                parameters.SeedVariant,
                parameters.Loop);

            if (slot < 0)
            {
                GpuParticleNativeFallback.Play(current, parameters.TimeScale, parameters.SeedVariant);
                return GpuParticleHandle.Invalid;
            }

            // Re-fetch the generation assigned by the pool.
            int generation = GpuParticleVatRenderSystem.Instance.GetGeneration(slot);
            GpuParticleVatRenderSystem.Instance.Play(slot, generation);
            handle = new GpuParticleHandle(slot, generation);
            return handle;
        }

        public void Stop(bool clear = true)
        {
            if (handle.IsValid)
            {
                GpuParticleVatRenderSystem.Instance.Unregister(handle.SlotIndex, handle.Generation);
                handle = GpuParticleHandle.Invalid;
            }

            if (clear)
            {
                GpuParticleNativeFallback.Stop(CurrentBinding, clear);
            }
        }

        public void Pause()
        {
            // With the instanced pool, pause is implemented by setting timeScale to 0.
            if (handle.IsValid)
            {
                GpuParticleVatRenderSystem.Instance.SetTimeScale(handle.SlotIndex, handle.Generation, 0f);
            }
            else
            {
                GpuParticleNativeFallback.Pause(CurrentBinding);
            }
        }

        public void Resume()
        {
            if (handle.IsValid)
            {
                GpuParticleVatRenderSystem.Instance.SetTimeScale(handle.SlotIndex, handle.Generation, 1f);
            }
            else
            {
                GpuParticleNativeFallback.Resume(CurrentBinding);
            }
        }

        public void SetTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            SetTransform(Matrix4x4.TRS(position, rotation, scale));
        }

        public void SetTransform(Matrix4x4 localToWorld)
        {
            if (handle.IsValid)
            {
                GpuParticleVatRenderSystem.Instance.SetTransform(handle.SlotIndex, handle.Generation, localToWorld);
            }
        }

        public void Prewarm()
        {
            // VAT playback does not require prewarm; clip assets are loaded on demand.
        }

        private void OnDisable()
        {
            Stop(clear: true);
        }
    }
}
