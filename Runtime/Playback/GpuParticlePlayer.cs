using UnityEngine;

namespace GpuParticle.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GpuParticlePlayer : MonoBehaviour
    {
        [SerializeField] private GpuParticleBinding binding = null!;

        private GpuParticleHandle handle = GpuParticleHandle.Invalid;
        private bool isPaused;
        private GpuParticleFailure pendingFailure;
        private GpuParticleNativeRestoreState pendingRestoreState;

        public bool IsPlaying => handle.IsValid || GpuParticleNativeFallback.IsAnyAlive(CurrentBinding);
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
            GpuParticleStartResult result = TryPlayGpu(parameters, out GpuParticleHandle gpuHandle);
            if (result == GpuParticleStartResult.GpuStarted)
            {
                return gpuHandle;
            }

            GpuParticleNativeFallback.Play(CurrentBinding, parameters.TimeScale, parameters.SeedVariant);
            return GpuParticleHandle.Invalid;
        }

        public GpuParticleStartResult TryPlayGpu(
            in GpuParticlePlayParams parameters,
            out GpuParticleHandle gpuHandle)
        {
            gpuHandle = GpuParticleHandle.Invalid;
            GpuParticleBinding current = CurrentBinding;
            if (current == null || !current.CanAttemptGpuPlayback)
            {
                pendingFailure = new GpuParticleFailure(GpuParticleFailureCode.NativeRequired, "Binding is not GPU ready.");
                return GpuParticleStartResult.NativeRequired;
            }

            if (!current.Clip.TryValidateRuntime(out GpuParticleFailure validationFailure))
            {
                current.MarkRuntimeFailure(validationFailure);
                pendingFailure = validationFailure;
                return GpuParticleStartResult.NativeRequired;
            }

            if (!GpuParticleRuntime.TryPlay(current.Clip, this, parameters, out gpuHandle, out GpuParticleFailure startFailure))
            {
                current.MarkRuntimeFailure(startFailure);
                pendingFailure = startFailure;
                return GpuParticleStartResult.NativeRequired;
            }

            Stop(clear: true);
            handle = gpuHandle;
            isPaused = false;
            pendingFailure = GpuParticleFailure.None;
            current.SuppressNativeRenderers();
            return GpuParticleStartResult.GpuStarted;
        }

        public void Stop(bool clear = true)
        {
            if (handle.IsValid)
            {
                GpuParticleRuntime.Stop(handle);
                handle = GpuParticleHandle.Invalid;
            }

            if (clear)
            {
                GpuParticleNativeFallback.Stop(CurrentBinding, clear);
            }
        }

        public void Pause()
        {
            isPaused = true;
            if (handle.IsValid)
            {
                GpuParticleRuntime.SetPaused(handle, true);
            }
            else
            {
                GpuParticleNativeFallback.Pause(CurrentBinding);
            }
        }

        public void Resume()
        {
            isPaused = false;
            if (handle.IsValid)
            {
                GpuParticleRuntime.SetPaused(handle, false);
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
                GpuParticleRuntime.SetTransform(handle, localToWorld);
            }
        }

        public void Prewarm()
        {
            GpuParticleBinding current = CurrentBinding;
            if (current != null && current.CanAttemptGpuPlayback)
            {
                GpuParticleRuntime.AcquirePrewarm(current.Clip).Dispose();
            }
        }

        public bool TryConsumeNativeFallbackRequest(
            out GpuParticleFailure failure,
            out GpuParticleNativeRestoreState restoreState)
        {
            if (pendingFailure.IsFailure)
            {
                failure = pendingFailure;
                restoreState = pendingRestoreState;
                pendingFailure = GpuParticleFailure.None;
                pendingRestoreState = default;
                return true;
            }

            failure = GpuParticleFailure.None;
            restoreState = default;
            return false;
        }

        internal void RequestNativeFallback(GpuParticleFailure failure, float elapsedClipTime, uint seedVariant, float timeScale)
        {
            if (!handle.IsValid)
            {
                return;
            }

            GpuParticleBinding current = CurrentBinding;
            current?.MarkRuntimeFailure(failure);
            pendingFailure = failure;
            pendingRestoreState = new GpuParticleNativeRestoreState(elapsedClipTime, seedVariant, timeScale, isPaused);
            GpuParticleRuntime.Stop(handle);
            handle = GpuParticleHandle.Invalid;
        }

        internal void NotifyGpuStopped(GpuParticleHandle completedHandle)
        {
            if (handle.Equals(completedHandle))
            {
                handle = GpuParticleHandle.Invalid;
            }
        }

        private void OnDisable()
        {
            Stop(clear: true);
        }
    }
}
