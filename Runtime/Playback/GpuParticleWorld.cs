using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    internal sealed class GpuParticleWorld : MonoBehaviour
    {
        private GpuParticleInstancePool instances = null!;
        private GpuParticleBufferCache bufferCache = null!;
        private bool subscribed;
        private readonly Dictionary<GpuParticleClip, GpuParticleBlob> blobCache = new();

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

            if (!SupportsStatePlayback())
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.UnsupportedPlatform,
                    $"Graphics API {SystemInfo.graphicsDeviceType} does not support GPU state playback.");
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
            UploadCurrentFrameStates();
        }

        private void UploadCurrentFrameStates()
        {
            blobCache.Clear();
            var items = instances.ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                var instance = items.Array![items.Offset + i];
                if (instance.Paused)
                {
                    continue;
                }

                if (!TryGetBlob(instance.Clip, out GpuParticleBlob blob))
                {
                    continue;
                }

                UploadInstanceStates(ref instance, blob);
                instances.TryUpdateInstance(instance.Handle, instance);
            }
        }

        private bool TryGetBlob(GpuParticleClip clip, out GpuParticleBlob blob)
        {
            if (clip == null)
            {
                blob = null!;
                return false;
            }

            if (blobCache.TryGetValue(clip, out blob))
            {
                return true;
            }

            if (clip.Payload == null ||
                clip.Payload.bytes == null ||
                !GpuParticleBlobReader.TryRead(clip.Payload.bytes, out blob, out _))
            {
                blob = null!;
                return false;
            }

            blobCache[clip] = blob;
            return true;
        }

        private void UploadInstanceStates(ref GpuParticleInstancePool.Instance instance, GpuParticleBlob blob)
        {
            GpuParticleClip clip = instance.Clip;
            GpuParticleGeometryTrack[]? tracks = clip.GeometryTracks;
            if (tracks == null || tracks.Length == 0)
            {
                return;
            }

            ReadAllBlobArrays(blob,
                out GpuParticleBlobParticleState[] particleStates,
                out GpuParticleBlobTrailState[] trailStates,
                out GpuParticleBlobMeshTransform[] meshTransforms);

            int totalParticles = 0;
            int totalTrails = 0;
            int totalMeshes = 0;
            for (int t = 0; t < tracks.Length; t++)
            {
                GpuParticleGeometryFrame frameA = FindFrameA(tracks[t], instance.Elapsed);
                totalParticles += frameA.ParticleCount;
                totalTrails += frameA.TrailCount;
                totalMeshes += frameA.MeshTransformCount;
            }

            ShaderParticleState[]? interpolatedParticles = totalParticles > 0 ? new ShaderParticleState[totalParticles] : null;
            ShaderTrailState[]? interpolatedTrails = totalTrails > 0 ? new ShaderTrailState[totalTrails] : null;
            ShaderMeshTransform[]? interpolatedMeshes = totalMeshes > 0 ? new ShaderMeshTransform[totalMeshes] : null;

            int particleIndex = 0;
            int trailIndex = 0;
            int meshIndex = 0;
            for (int t = 0; t < tracks.Length; t++)
            {
                GpuParticleGeometryTrack track = tracks[t];
                (GpuParticleGeometryFrame frameA, GpuParticleGeometryFrame? frameB, float lerpT) =
                    FindSurroundingFrames(track, instance.Elapsed);

                if (frameA.ParticleCount > 0 && interpolatedParticles != null)
                {
                    InterpolateParticleStates(
                        particleStates,
                        frameA,
                        frameB,
                        lerpT,
                        interpolatedParticles,
                        ref particleIndex);
                }

                if (frameA.TrailCount > 0 && interpolatedTrails != null)
                {
                    InterpolateTrailStates(
                        trailStates,
                        frameA,
                        frameB,
                        lerpT,
                        interpolatedTrails,
                        ref trailIndex);
                }

                if (frameA.MeshTransformCount > 0 && interpolatedMeshes != null)
                {
                    InterpolateMeshTransforms(
                        meshTransforms,
                        frameA,
                        frameB,
                        lerpT,
                        interpolatedMeshes,
                        ref meshIndex);
                }
            }

            instance.ParticleStateBuffer = UploadParticleStates(instance.ParticleStateBuffer, interpolatedParticles)!;
            instance.TrailStateBuffer = UploadTrailStates(instance.TrailStateBuffer, interpolatedTrails)!;
            instance.MeshTransformBuffer = UploadMeshTransforms(instance.MeshTransformBuffer, interpolatedMeshes)!;
        }

        private static void ReadAllBlobArrays(
            GpuParticleBlob blob,
            out GpuParticleBlobParticleState[] particleStates,
            out GpuParticleBlobTrailState[] trailStates,
            out GpuParticleBlobMeshTransform[] meshTransforms)
        {
            particleStates = TryGetSection(blob, GpuParticleSectionType.ParticleState, out GpuParticleBlobSection particleSection)
                ? ReadParticleStates(blob, particleSection)
                : Array.Empty<GpuParticleBlobParticleState>();
            trailStates = TryGetSection(blob, GpuParticleSectionType.TrailState, out GpuParticleBlobSection trailSection)
                ? ReadTrailStates(blob, trailSection)
                : Array.Empty<GpuParticleBlobTrailState>();
            meshTransforms = TryGetSection(blob, GpuParticleSectionType.MeshTransform, out GpuParticleBlobSection meshSection)
                ? ReadMeshTransforms(blob, meshSection)
                : Array.Empty<GpuParticleBlobMeshTransform>();
        }

        private static bool TryGetSection(GpuParticleBlob blob, GpuParticleSectionType type, out GpuParticleBlobSection section)
        {
            foreach (GpuParticleBlobSection candidate in blob.Sections)
            {
                if (candidate.Type == (int)type)
                {
                    section = candidate;
                    return true;
                }
            }

            section = default;
            return false;
        }

        private static GpuParticleBlobParticleState[] ReadParticleStates(GpuParticleBlob blob, GpuParticleBlobSection section)
        {
            int stride = GpuParticleBlobWriter.ParticleStateStride;
            int count = section.Length / stride;
            var states = new GpuParticleBlobParticleState[count];
            for (int i = 0; i < count; i++)
            {
                int offset = section.Offset + i * stride;
                states[i] = new GpuParticleBlobParticleState
                {
                    Position = ReadVector3(blob.Bytes, offset + 0),
                    Velocity = ReadVector3(blob.Bytes, offset + 12),
                    Size = ReadSingle(blob.Bytes, offset + 24),
                    Rotation = ReadVector4(blob.Bytes, offset + 32),
                    Color = ReadColor32(blob.Bytes, offset + 48),
                    Lifetime = ReadSingle(blob.Bytes, offset + 52),
                    Seed = ReadUInt32(blob.Bytes, offset + 56),
                };
            }

            return states;
        }

        private static GpuParticleBlobTrailState[] ReadTrailStates(GpuParticleBlob blob, GpuParticleBlobSection section)
        {
            int stride = GpuParticleBlobWriter.TrailStateStride;
            int count = section.Length / stride;
            var states = new GpuParticleBlobTrailState[count];
            for (int i = 0; i < count; i++)
            {
                int offset = section.Offset + i * stride;
                states[i] = new GpuParticleBlobTrailState
                {
                    Position = ReadVector3(blob.Bytes, offset + 0),
                    Width = ReadSingle(blob.Bytes, offset + 12),
                    Color = ReadColor32(blob.Bytes, offset + 16),
                    ParticleId = ReadUInt32(blob.Bytes, offset + 20),
                };
            }

            return states;
        }

        private static GpuParticleBlobMeshTransform[] ReadMeshTransforms(GpuParticleBlob blob, GpuParticleBlobSection section)
        {
            int stride = GpuParticleBlobWriter.MeshTransformStride;
            int count = section.Length / stride;
            var transforms = new GpuParticleBlobMeshTransform[count];
            for (int i = 0; i < count; i++)
            {
                int offset = section.Offset + i * stride;
                transforms[i] = new GpuParticleBlobMeshTransform
                {
                    Position = ReadVector3(blob.Bytes, offset + 0),
                    Rotation = ReadQuaternion(blob.Bytes, offset + 16),
                    Scale = ReadVector3(blob.Bytes, offset + 32),
                    Color = ReadColor32(blob.Bytes, offset + 44),
                };
            }

            return transforms;
        }

        private static GpuParticleGeometryFrame FindFrameA(GpuParticleGeometryTrack track, float elapsed)
        {
            int index = track.FindFrameIndex(elapsed);
            GpuParticleGeometryFrame[] frames = track.Frames;
            if (index >= 0 && index < frames.Length)
            {
                return frames[index];
            }

            return frames.Length > 0 ? frames[0] : new GpuParticleGeometryFrame();
        }

        private static (GpuParticleGeometryFrame frameA, GpuParticleGeometryFrame? frameB, float lerpT)
            FindSurroundingFrames(GpuParticleGeometryTrack track, float elapsed)
        {
            GpuParticleGeometryFrame[] frames = track.Frames;
            if (frames.Length == 0)
            {
                return (new GpuParticleGeometryFrame(), null, 0f);
            }

            int indexA = track.FindFrameIndex(elapsed);
            if (indexA < 0)
            {
                indexA = 0;
            }

            GpuParticleGeometryFrame frameA = frames[indexA];
            if (indexA >= frames.Length - 1)
            {
                return (frameA, null, 0f);
            }

            GpuParticleGeometryFrame frameB = frames[indexA + 1];
            float duration = frameB.Time - frameA.Time;
            float lerpT = duration > Mathf.Epsilon ? Mathf.Clamp01((elapsed - frameA.Time) / duration) : 0f;
            return (frameA, frameB, lerpT);
        }

        private static void InterpolateParticleStates(
            GpuParticleBlobParticleState[] source,
            GpuParticleGeometryFrame frameA,
            GpuParticleGeometryFrame? frameB,
            float lerpT,
            ShaderParticleState[] destination,
            ref int destinationIndex)
        {
            int countA = frameA.ParticleCount;
            int countB = frameB?.ParticleCount ?? 0;
            int offsetA = frameA.ParticleStateOffset;
            int offsetB = frameB?.ParticleStateOffset ?? 0;

            for (int i = 0; i < countA; i++)
            {
                GpuParticleBlobParticleState stateA = source[offsetA + i];
                GpuParticleBlobParticleState stateB = i < countB ? source[offsetB + i] : stateA;
                GpuParticleBlobParticleState lerped = lerpT > 0f && frameB != null
                    ? GpuParticleStateInterpolation.Lerp(stateA, stateB, lerpT)
                    : stateA;
                destination[destinationIndex++] = new ShaderParticleState(lerped);
            }
        }

        private static void InterpolateTrailStates(
            GpuParticleBlobTrailState[] source,
            GpuParticleGeometryFrame frameA,
            GpuParticleGeometryFrame? frameB,
            float lerpT,
            ShaderTrailState[] destination,
            ref int destinationIndex)
        {
            int countA = frameA.TrailCount;
            int countB = frameB?.TrailCount ?? 0;
            int offsetA = frameA.TrailStateOffset;
            int offsetB = frameB?.TrailStateOffset ?? 0;

            for (int i = 0; i < countA; i++)
            {
                GpuParticleBlobTrailState stateA = source[offsetA + i];
                GpuParticleBlobTrailState stateB = i < countB ? source[offsetB + i] : stateA;
                GpuParticleBlobTrailState lerped = lerpT > 0f && frameB != null
                    ? Lerp(stateA, stateB, lerpT)
                    : stateA;
                destination[destinationIndex++] = new ShaderTrailState(lerped);
            }
        }

        private static void InterpolateMeshTransforms(
            GpuParticleBlobMeshTransform[] source,
            GpuParticleGeometryFrame frameA,
            GpuParticleGeometryFrame? frameB,
            float lerpT,
            ShaderMeshTransform[] destination,
            ref int destinationIndex)
        {
            int countA = frameA.MeshTransformCount;
            int countB = frameB?.MeshTransformCount ?? 0;
            int offsetA = frameA.MeshTransformOffset;
            int offsetB = frameB?.MeshTransformOffset ?? 0;

            for (int i = 0; i < countA; i++)
            {
                GpuParticleBlobMeshTransform stateA = source[offsetA + i];
                GpuParticleBlobMeshTransform stateB = i < countB ? source[offsetB + i] : stateA;
                GpuParticleBlobMeshTransform lerped = lerpT > 0f && frameB != null
                    ? Lerp(stateA, stateB, lerpT)
                    : stateA;
                destination[destinationIndex++] = new ShaderMeshTransform(lerped);
            }
        }

        private static ComputeBuffer? UploadParticleStates(
            ComputeBuffer? existing,
            ShaderParticleState[]? states)
        {
            if (states == null || states.Length == 0)
            {
                existing?.Release();
                return null;
            }

            ComputeBuffer buffer = GetOrResizeBuffer(existing, states.Length, 64);
            buffer.SetData(states);
            return buffer;
        }

        private static ComputeBuffer? UploadTrailStates(
            ComputeBuffer? existing,
            ShaderTrailState[]? states)
        {
            if (states == null || states.Length == 0)
            {
                existing?.Release();
                return null;
            }

            ComputeBuffer buffer = GetOrResizeBuffer(existing, states.Length, 32);
            buffer.SetData(states);
            return buffer;
        }

        private static ComputeBuffer? UploadMeshTransforms(
            ComputeBuffer? existing,
            ShaderMeshTransform[]? transforms)
        {
            if (transforms == null || transforms.Length == 0)
            {
                existing?.Release();
                return null;
            }

            ComputeBuffer buffer = GetOrResizeBuffer(existing, transforms.Length, 48);
            buffer.SetData(transforms);
            return buffer;
        }

        private static ComputeBuffer GetOrResizeBuffer(ComputeBuffer? existing, int count, int stride)
        {
            if (existing != null && existing.count == count)
            {
                return existing;
            }

            existing?.Release();
            return new ComputeBuffer(count, stride);
        }

        private static GpuParticleBlobTrailState Lerp(in GpuParticleBlobTrailState a, in GpuParticleBlobTrailState b, float t)
        {
            return new GpuParticleBlobTrailState
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Width = Mathf.Lerp(a.Width, b.Width, t),
                Color = Color32.Lerp(a.Color, b.Color, t),
                ParticleId = a.ParticleId,
            };
        }

        private static GpuParticleBlobMeshTransform Lerp(in GpuParticleBlobMeshTransform a, in GpuParticleBlobMeshTransform b, float t)
        {
            return new GpuParticleBlobMeshTransform
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Scale = Vector3.Lerp(a.Scale, b.Scale, t),
                Color = Color32.Lerp(a.Color, b.Color, t),
            };
        }

        private static uint PackColor(Color32 color)
        {
            return (uint)color.r |
                   ((uint)color.g << 8) |
                   ((uint)color.b << 16) |
                   ((uint)color.a << 24);
        }

        private static float ReadSingle(byte[] bytes, int offset)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
            return BitConverter.Int32BitsToSingle(bits);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        }

        private static Vector3 ReadVector3(byte[] bytes, int offset)
        {
            return new Vector3(
                ReadSingle(bytes, offset + 0),
                ReadSingle(bytes, offset + 4),
                ReadSingle(bytes, offset + 8));
        }

        private static Vector4 ReadVector4(byte[] bytes, int offset)
        {
            return new Vector4(
                ReadSingle(bytes, offset + 0),
                ReadSingle(bytes, offset + 4),
                ReadSingle(bytes, offset + 8),
                ReadSingle(bytes, offset + 12));
        }

        private static Quaternion ReadQuaternion(byte[] bytes, int offset)
        {
            return new Quaternion(
                ReadSingle(bytes, offset + 0),
                ReadSingle(bytes, offset + 4),
                ReadSingle(bytes, offset + 8),
                ReadSingle(bytes, offset + 12));
        }

        private static Color32 ReadColor32(byte[] bytes, int offset)
        {
            return new Color32(bytes[offset + 0], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
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

        private static bool SupportsStatePlayback()
        {
            return SystemInfo.supportsComputeShaders ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
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

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private readonly struct ShaderParticleState
        {
            [FieldOffset(0)] public readonly Vector3 Position;
            [FieldOffset(12)] public readonly Vector3 Velocity;
            [FieldOffset(24)] public readonly float Size;
            [FieldOffset(32)] public readonly Vector4 Rotation;
            [FieldOffset(48)] public readonly uint Color;
            [FieldOffset(52)] public readonly float Lifetime;
            [FieldOffset(56)] public readonly uint Seed;

            public ShaderParticleState(GpuParticleBlobParticleState state)
            {
                Position = state.Position;
                Velocity = state.Velocity;
                Size = state.Size;
                Rotation = state.Rotation;
                Color = PackColor(state.Color);
                Lifetime = state.Lifetime;
                Seed = state.Seed;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private readonly struct ShaderTrailState
        {
            [FieldOffset(0)] public readonly Vector3 Position;
            [FieldOffset(12)] public readonly float Width;
            [FieldOffset(16)] public readonly uint Color;
            [FieldOffset(20)] public readonly uint ParticleId;

            public ShaderTrailState(GpuParticleBlobTrailState state)
            {
                Position = state.Position;
                Width = state.Width;
                Color = PackColor(state.Color);
                ParticleId = state.ParticleId;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private readonly struct ShaderMeshTransform
        {
            [FieldOffset(0)] public readonly Vector3 Position;
            [FieldOffset(16)] public readonly Vector4 Rotation;
            [FieldOffset(32)] public readonly Vector3 Scale;
            [FieldOffset(44)] public readonly uint Color;

            public ShaderMeshTransform(GpuParticleBlobMeshTransform transform)
            {
                Position = transform.Position;
                Rotation = new Vector4(
                    transform.Rotation.x,
                    transform.Rotation.y,
                    transform.Rotation.z,
                    transform.Rotation.w);
                Scale = transform.Scale;
                Color = PackColor(transform.Color);
            }
        }
    }
}
