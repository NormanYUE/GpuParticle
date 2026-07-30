using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    internal sealed class GpuParticleWorld : MonoBehaviour
    {
        private GpuParticleInstancePool instances = null!;
        private GpuParticleBufferCache bufferCache = null!;
        private bool subscribed;

        private void Awake()
        {
            instances = new GpuParticleInstancePool(64);
            bufferCache = new GpuParticleBufferCache();
            Subscribe();
        }

        public void AcquireClip(GpuParticleClip clip)
        {
            if (clip != null)
            {
                bufferCache.Acquire(clip);
            }
        }

        public void ReleaseClip(GpuParticleClip clip)
        {
            if (clip != null)
            {
                bufferCache.Release(clip);
            }
        }

        public bool TryPlay(
            GpuParticleClip clip,
            GpuParticlePlayer owner,
            in GpuParticlePlayParams parameters,
            out GpuParticleHandle handle,
            out GpuParticleFailure failure)
        {
            handle = GpuParticleHandle.Invalid;

            if (clip == null)
            {
                failure = new GpuParticleFailure(GpuParticleFailureCode.MissingClip, "Clip is missing.");
                return false;
            }

            if (!clip.TryValidateRuntime(out failure))
            {
                return false;
            }

            if (!IsGraphicsApiSupported(clip.RequiredCapabilities))
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.UnsupportedPlatform,
                    $"Graphics API {SystemInfo.graphicsDeviceType} is not allowed by this clip.");
                return false;
            }

            bufferCache.Acquire(clip);
            handle = instances.Allocate(clip, owner, parameters);
            failure = GpuParticleFailure.None;
            return true;
        }

        public bool Stop(GpuParticleHandle handle)
        {
            if (instances.Release(handle, out GpuParticleClip clip))
            {
                bufferCache.Release(clip);
                return true;
            }

            return false;
        }

        public bool SetPaused(GpuParticleHandle handle, bool paused)
        {
            return instances.SetPaused(handle, paused);
        }

        public bool SetTransform(GpuParticleHandle handle, Matrix4x4 localToWorld)
        {
            return instances.SetTransform(handle, localToWorld);
        }

        private void Update()
        {
            instances.Update(Time.deltaTime, OnInstanceCompleted);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            GpuParticleGeometryRenderer.Render(instances.ActiveItems, camera);
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Application.lowMemory += OnLowMemory;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Application.lowMemory -= OnLowMemory;
            subscribed = false;
        }

        private void OnLowMemory()
        {
            bufferCache.ClearUnused();
        }

        private void OnInstanceCompleted(GpuParticleClip clip)
        {
            bufferCache.Release(clip);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            instances?.Clear();
            bufferCache?.Dispose();
        }

        private static bool IsGraphicsApiSupported(GpuParticleCapability capabilities)
        {
            GraphicsDeviceType type = SystemInfo.graphicsDeviceType;
            switch (type)
            {
                case GraphicsDeviceType.Vulkan:
                    return (capabilities & GpuParticleCapability.Vulkan) != 0 || !RequiresSpecificApi(capabilities);
                case GraphicsDeviceType.OpenGLES3:
                    return (capabilities & GpuParticleCapability.OpenGLES3) != 0 || !RequiresSpecificApi(capabilities);
                case GraphicsDeviceType.Metal:
                    return (capabilities & GpuParticleCapability.Metal) != 0 || !RequiresSpecificApi(capabilities);
                case GraphicsDeviceType.Direct3D11:
                    return (capabilities & GpuParticleCapability.Direct3D11) != 0 || !RequiresSpecificApi(capabilities);
                case GraphicsDeviceType.Direct3D12:
                    return (capabilities & GpuParticleCapability.Direct3D12) != 0 || !RequiresSpecificApi(capabilities);
                default:
                    return !RequiresSpecificApi(capabilities);
            }
        }

        private static bool RequiresSpecificApi(GpuParticleCapability capabilities)
        {
            const GpuParticleCapability apiMask =
                GpuParticleCapability.Vulkan |
                GpuParticleCapability.OpenGLES3 |
                GpuParticleCapability.Metal |
                GpuParticleCapability.Direct3D11 |
                GpuParticleCapability.Direct3D12;
            return (capabilities & apiMask) != 0;
        }
    }
}
