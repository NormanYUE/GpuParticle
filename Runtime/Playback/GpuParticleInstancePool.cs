using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    internal sealed class GpuParticleInstancePool
    {
        private Instance[] slots;
        private int[] freeStack;
        private int freeCount;
        private Instance[] activeItems;
        private int activeCount;

        public GpuParticleInstancePool(int initialCapacity)
        {
            int capacity = Mathf.Max(4, initialCapacity);
            slots = new Instance[capacity];
            freeStack = new int[capacity];
            activeItems = new Instance[capacity * 4];
            for (int i = capacity - 1; i >= 0; i--)
            {
                freeStack[freeCount++] = i;
            }
        }

        public ArraySegment<Instance> ActiveItems
        {
            get
            {
                activeCount = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Active)
                    {
                        if (activeCount >= activeItems.Length)
                        {
                            break;
                        }

                        activeItems[activeCount++] = slots[i];
                    }
                }

                return new ArraySegment<Instance>(activeItems, 0, activeCount);
            }
        }

        public GpuParticleHandle Allocate(GpuParticleClip clip, GpuParticlePlayer owner, in GpuParticlePlayParams parameters)
        {
            if (freeCount == 0)
            {
                Grow();
            }

            int slot = freeStack[--freeCount];
            uint generation = slots[slot].Generation + 1u;
            if (generation == 0)
            {
                generation = 1;
            }

            GpuParticleHandle handle = new GpuParticleHandle(slot, generation);
            slots[slot] = new Instance
            {
                Active = true,
                Clip = clip,
                Owner = owner,
                LocalToWorld = parameters.LocalToWorld,
                TimeScale = Mathf.Approximately(parameters.TimeScale, 0f) ? 0f : parameters.TimeScale,
                Loop = parameters.Loop || clip.Loop,
                SeedVariant = parameters.SeedVariant,
                Generation = generation,
                Elapsed = 0f,
                Paused = false,
                Handle = handle,
            };

            return handle;
        }

        public bool Release(GpuParticleHandle handle, out GpuParticleClip clip)
        {
            clip = null!;
            if (!TryGetIndex(handle, out int index))
            {
                return false;
            }

            clip = slots[index].Clip;
            ReleaseIndex(index);
            return true;
        }

        public bool SetPaused(GpuParticleHandle handle, bool paused)
        {
            if (!TryGetIndex(handle, out int index))
            {
                return false;
            }

            slots[index].Paused = paused;
            return true;
        }

        public bool SetTransform(GpuParticleHandle handle, Matrix4x4 localToWorld)
        {
            if (!TryGetIndex(handle, out int index))
            {
                return false;
            }

            slots[index].LocalToWorld = localToWorld;
            return true;
        }

        public bool TryUpdateInstance(GpuParticleHandle handle, in Instance value)
        {
            if (!TryGetIndex(handle, out int index))
            {
                return false;
            }

            slots[index] = value;
            return true;
        }

        public void Update(float deltaTime, Action<GpuParticleClip> onCompleted)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Active || slots[i].Paused)
                {
                    continue;
                }

                Instance instance = slots[i];
                instance.Elapsed += Mathf.Max(0f, deltaTime) * instance.TimeScale;
                if (instance.Clip.Duration > 0f && instance.Elapsed > instance.Clip.Duration)
                {
                    if (instance.Loop)
                    {
                        instance.Elapsed %= instance.Clip.Duration;
                    }
                    else
                    {
                        GpuParticleHandle completedHandle = new GpuParticleHandle(i, instance.Generation);
                        GpuParticleClip completedClip = instance.Clip;
                        GpuParticlePlayer completedOwner = instance.Owner;
                        ReleaseIndex(i);
                        onCompleted?.Invoke(completedClip);
                        completedOwner?.NotifyGpuStopped(completedHandle);
                        continue;
                    }
                }

                slots[i] = instance;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Active)
                {
                    ReleaseInstanceBuffers(ref slots[i]);
                }
            }

            freeCount = 0;
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                slots[i].Active = false;
                freeStack[freeCount++] = i;
            }
        }

        private bool TryGetIndex(GpuParticleHandle handle, out int index)
        {
            index = handle.Slot;
            return handle.IsValid &&
                   index >= 0 &&
                   index < slots.Length &&
                   slots[index].Active &&
                   slots[index].Generation == handle.Generation;
        }

        private void Grow()
        {
            int oldLength = slots.Length;
            int newLength = oldLength * 2;
            Array.Resize(ref slots, newLength);
            Array.Resize(ref freeStack, newLength);
            Array.Resize(ref activeItems, newLength);
            for (int i = newLength - 1; i >= oldLength; i--)
            {
                freeStack[freeCount++] = i;
            }
        }

        private void ReleaseIndex(int index)
        {
            ReleaseInstanceBuffers(ref slots[index]);
            slots[index].Active = false;
            slots[index].Clip = null!;
            slots[index].Owner = null!;
            freeStack[freeCount++] = index;
        }

        private static void ReleaseInstanceBuffers(ref Instance instance)
        {
            instance.ParticleStateBuffer?.Release();
            instance.TrailStateBuffer?.Release();
            instance.MeshTransformBuffer?.Release();
            instance.ParticleStateBuffer = null!;
            instance.TrailStateBuffer = null!;
            instance.MeshTransformBuffer = null!;
        }

        internal struct Instance
        {
            public bool Active;
            public GpuParticleClip Clip;
            public GpuParticlePlayer Owner;
            public Matrix4x4 LocalToWorld;
            public float Elapsed;
            public float TimeScale;
            public bool Loop;
            public bool Paused;
            public uint SeedVariant;
            public uint Generation;
            public GpuParticleHandle Handle;

            public ComputeBuffer ParticleStateBuffer;
            public ComputeBuffer TrailStateBuffer;
            public ComputeBuffer MeshTransformBuffer;
        }
    }
}
