using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleVatRenderSystem : IDisposable
    {
        private static GpuParticleVatRenderSystem? instance;

        public static GpuParticleVatRenderSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GpuParticleVatRenderSystem();
                }

                return instance;
            }
        }

        private readonly GpuParticleInstancePool pool = new GpuParticleInstancePool();
        private readonly Dictionary<int, BatchData> batches = new Dictionary<int, BatchData>();
        private bool subscribed;

        private GpuParticleVatRenderSystem()
        {
            Subscribe();
        }

        public int Register(
            GpuParticleClip clip,
            Matrix4x4 localToWorld,
            float timeScale,
            uint seedVariant,
            bool loop)
        {
            int index = pool.Allocate(clip, localToWorld, timeScale, seedVariant, loop);
            if (index < 0)
            {
                return -1;
            }

            return index;
        }

        public void Unregister(int slotIndex, int generation)
        {
            pool.Free(slotIndex, generation);
        }

        public void SetTransform(int slotIndex, int generation, Matrix4x4 localToWorld)
        {
            if (!pool.IsAlive(slotIndex, generation))
            {
                return;
            }

            ref GpuParticleInstanceSlot slot = ref pool.GetSlot(slotIndex, generation);
            slot.LocalToWorld = localToWorld;
        }

        public void SetTimeScale(int slotIndex, int generation, float timeScale)
        {
            if (!pool.IsAlive(slotIndex, generation))
            {
                return;
            }

            ref GpuParticleInstanceSlot slot = ref pool.GetSlot(slotIndex, generation);
            slot.TimeScale = timeScale;
        }

        public void Play(int slotIndex, int generation)
        {
            if (!pool.IsAlive(slotIndex, generation))
            {
                return;
            }

            ref GpuParticleInstanceSlot slot = ref pool.GetSlot(slotIndex, generation);
            slot.ElapsedTime = 0f;
        }

        public void Stop(int slotIndex, int generation)
        {
            Unregister(slotIndex, generation);
        }

        public int GetGeneration(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= pool.Capacity)
            {
                return -1;
            }

            return pool.GetGeneration(slotIndex);
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            subscribed = false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            Render();
        }

        private void Render()
        {
            pool.UpdateAll(Time.deltaTime);
            ReadOnlySpan<GpuParticleInstanceSlot> slots = pool.AliveSlots;
            if (slots.Length == 0)
            {
                return;
            }

            batches.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                GpuParticleInstanceSlot slot = slots[i];
                if (slot.Clip is null)
                {
                    continue;
                }

                int key = slot.Clip.GetInstanceID();
                if (!batches.TryGetValue(key, out BatchData batch))
                {
                    batch = new BatchData(slot.Clip);
                    batches.Add(key, batch);
                }

                batch.Add(slot);
            }

            foreach (BatchData batch in batches.Values)
            {
                batch.Draw();
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            foreach (BatchData batch in batches.Values)
            {
                batch.Dispose();
            }

            batches.Clear();
            instance = null;
        }

        private sealed class BatchData
        {
            private readonly GpuParticleClip clip;
            private readonly List<GpuParticleInstanceData> instanceData = new List<GpuParticleInstanceData>();
            private GraphicsBuffer? instanceBuffer;
            private Material? material;

            public BatchData(GpuParticleClip clip)
            {
                this.clip = clip ?? throw new ArgumentNullException(nameof(clip));
            }

            public void Add(in GpuParticleInstanceSlot slot)
            {
                instanceData.Add(new GpuParticleInstanceData(
                    slot.LocalToWorld,
                    slot.ElapsedTime,
                    slot.TimeScale,
                    slot.SeedVariant));
            }

            public void Draw()
            {
                int count = instanceData.Count;
                if (count == 0 || clip.Prefab is null)
                {
                    return;
                }

                Mesh mesh = GetMesh();
                Material mat = GetMaterial();
                if (mesh is null || mat is null)
                {
                    return;
                }

                EnsureBuffer(count);
                instanceBuffer!.SetData(instanceData);

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mpb.SetBuffer("_InstanceDataBuffer", instanceBuffer);

                Bounds bounds = TransformBounds(clip.LocalBounds, clip.Prefab.transform.localToWorldMatrix);

                Graphics.DrawMeshInstancedProcedural(
                    mesh,
                    0,
                    mat,
                    bounds,
                    count,
                    mpb,
                    ShadowCastingMode.Off,
                    false,
                    0,
                    null,
                    LightProbeUsage.Off);
            }

            private Mesh GetMesh()
            {
                if (clip.Prefab is null)
                {
                    return null!;
                }

                MeshFilter? filter = clip.Prefab.GetComponentInChildren<MeshFilter>();
                return filter is not null ? filter.sharedMesh : null!;
            }

            private Material GetMaterial()
            {
                if (material is null)
                {
                    MeshRenderer? renderer = clip.Prefab?.GetComponentInChildren<MeshRenderer>();
                    if (renderer is not null && renderer.sharedMaterial is not null)
                    {
                        material = new Material(renderer.sharedMaterial);
                        material.enableInstancing = true;

                        if (clip.PositionSizeTexture != null)
                        {
                            material.SetTexture("_PositionSizeTex", clip.PositionSizeTexture);
                        }

                        if (clip.ColorTexture != null)
                        {
                            material.SetTexture("_ColorTex", clip.ColorTexture);
                        }

                        if (clip.RotationTexture != null)
                        {
                            material.SetTexture("_RotationTex", clip.RotationTexture);
                        }

                        if (clip.VelocityLifetimeTexture != null)
                        {
                            material.SetTexture("_VelocityLifetimeTex", clip.VelocityLifetimeTexture);
                        }

                        if (clip.PositionSizeTexture != null)
                        {
                            Vector2 texelSize = new Vector2(
                                1f / Mathf.Max(1, clip.PositionSizeTexture.width),
                                1f / Mathf.Max(1, clip.PositionSizeTexture.height));
                            material.SetVector(
                                "_TexelSize",
                                new Vector4(texelSize.x, texelSize.y, clip.PositionSizeTexture.width, clip.PositionSizeTexture.height));
                        }

                        material.SetFloat("_Duration", clip.Duration);
                        material.SetFloat("_FrameCount", clip.FrameCount);
                    }
                }

                return material!;
            }

            private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
            {
                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;

                Vector3 min = matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, -extents.z));
                Vector3 max = min;

                Vector3[] corners =
                {
                    center + new Vector3(extents.x, -extents.y, -extents.z),
                    center + new Vector3(-extents.x, extents.y, -extents.z),
                    center + new Vector3(extents.x, extents.y, -extents.z),
                    center + new Vector3(-extents.x, -extents.y, extents.z),
                    center + new Vector3(extents.x, -extents.y, extents.z),
                    center + new Vector3(-extents.x, extents.y, extents.z),
                    center + new Vector3(extents.x, extents.y, extents.z),
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 worldCorner = matrix.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, worldCorner);
                    max = Vector3.Max(max, worldCorner);
                }

                return new Bounds((min + max) * 0.5f, max - min);
            }

            private void EnsureBuffer(int count)
            {
                if (instanceBuffer is not null && instanceBuffer.count >= count)
                {
                    return;
                }

                instanceBuffer?.Dispose();
                int size = Mathf.NextPowerOfTwo(Mathf.Max(count, 16));
                instanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    size,
                    System.Runtime.InteropServices.Marshal.SizeOf<GpuParticleInstanceData>());
            }

            public void Dispose()
            {
                instanceBuffer?.Dispose();
                instanceBuffer = null;
                if (material is not null)
                {
                    UnityEngine.Object.Destroy(material);
                    material = null;
                }
            }
        }
    }
}
