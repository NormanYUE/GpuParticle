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

            GameObject instance = Object.Instantiate(current.Clip.Prefab);
            instance.transform.position = parameters.LocalToWorld.GetColumn(3);
            instance.transform.rotation = Quaternion.LookRotation(
                parameters.LocalToWorld.GetColumn(2),
                parameters.LocalToWorld.GetColumn(1));

            if (instance.TryGetComponent<GpuParticleVatRenderer>(out var vat))
            {
                vat.TimeScale = parameters.TimeScale;
                vat.Loop = parameters.Loop;
                vat.SetTime(0f);
                vat.Play();
            }

            current.SuppressNativeRenderers();
            handle = new GpuParticleHandle(instance);
            return handle;
        }

        public void Stop(bool clear = true)
        {
            if (handle.IsValid && handle.Target != null)
            {
                Object.Destroy(handle.Target);
                handle = GpuParticleHandle.Invalid;
            }

            if (clear)
            {
                GpuParticleNativeFallback.Stop(CurrentBinding, clear);
            }
        }

        public void Pause()
        {
            if (handle.IsValid && handle.Target != null && handle.Target.TryGetComponent<GpuParticleVatRenderer>(out var vat))
            {
                vat.Stop();
            }
            else
            {
                GpuParticleNativeFallback.Pause(CurrentBinding);
            }
        }

        public void Resume()
        {
            if (handle.IsValid && handle.Target != null && handle.Target.TryGetComponent<GpuParticleVatRenderer>(out var vat))
            {
                vat.Play();
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
            if (handle.IsValid && handle.Target != null)
            {
                handle.Target.transform.position = localToWorld.GetColumn(3);
                handle.Target.transform.rotation = Quaternion.LookRotation(
                    localToWorld.GetColumn(2),
                    localToWorld.GetColumn(1));
            }
        }

        public void Prewarm()
        {
            // VAT playback does not require prewarm; the clip assets are loaded on demand.
        }

        private void OnDisable()
        {
            Stop(clear: true);
        }
    }
}
