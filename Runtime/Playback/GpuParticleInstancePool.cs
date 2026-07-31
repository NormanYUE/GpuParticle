using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleInstancePool
    {
        public const int DefaultCapacity = 1024;
        public const int MaxCapacity = 64 * 1024;

        private GpuParticleInstanceSlot[] slots;
        private int[] freeList;
        private int freeCount;
        private int aliveCount;

        public GpuParticleInstancePool(int initialCapacity = DefaultCapacity)
        {
            int capacity = Mathf.Clamp(initialCapacity, 1, MaxCapacity);
            slots = new GpuParticleInstanceSlot[capacity];
            freeList = new int[capacity];
            freeCount = 0;
            aliveCount = 0;
        }

        public int Allocate(
            GpuParticleClip clip,
            Matrix4x4 localToWorld,
            float timeScale,
            uint seedVariant,
            bool loop)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            int index;
            if (freeCount > 0)
            {
                index = freeList[--freeCount];
            }
            else if (aliveCount < slots.Length)
            {
                index = aliveCount++;
            }
            else
            {
                return -1;
            }

            ref GpuParticleInstanceSlot slot = ref slots[index];
            slot.Generation++;
            slot.Clip = clip;
            slot.LocalToWorld = localToWorld;
            slot.ElapsedTime = 0f;
            slot.TimeScale = timeScale;
            slot.SeedVariant = seedVariant;
            slot.Loop = loop;
            slot.IsAlive = true;
            return index;
        }

        public void Free(int index, int generation)
        {
            if (index < 0 || index >= slots.Length)
            {
                return;
            }

            ref GpuParticleInstanceSlot slot = ref slots[index];
            if (!slot.IsAlive || slot.Generation != generation)
            {
                return;
            }

            slot.IsAlive = false;
            slot.Clip = null!;
            if (freeCount < freeList.Length)
            {
                freeList[freeCount++] = index;
            }
        }

        public ref GpuParticleInstanceSlot GetSlot(int index, int generation)
        {
            if (index < 0 || index >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            ref GpuParticleInstanceSlot slot = ref slots[index];
            if (!slot.IsAlive || slot.Generation != generation)
            {
                throw new InvalidOperationException("Slot is not alive or generation mismatch.");
            }

            return ref slot;
        }

        public bool IsAlive(int index, int generation)
        {
            return index >= 0 && index < slots.Length
                && slots[index].IsAlive
                && slots[index].Generation == generation;
        }

        public int GetGeneration(int index)
        {
            if (index < 0 || index >= slots.Length)
            {
                return -1;
            }

            return slots[index].Generation;
        }

        public void UpdateAll(float deltaTime)
        {
            for (int i = 0; i < aliveCount; i++)
            {
                ref GpuParticleInstanceSlot slot = ref slots[i];
                if (!slot.IsAlive || slot.Clip == null)
                {
                    continue;
                }

                slot.ElapsedTime += deltaTime * slot.TimeScale;
                if (slot.Loop && slot.Clip.Duration > 0f)
                {
                    slot.ElapsedTime %= slot.Clip.Duration;
                }
                else if (slot.ElapsedTime > slot.Clip.Duration)
                {
                    slot.ElapsedTime = slot.Clip.Duration;
                }
            }
        }

        public ReadOnlySpan<GpuParticleInstanceSlot> AliveSlots
        {
            get
            {
                if (aliveCount == 0)
                {
                    return ReadOnlySpan<GpuParticleInstanceSlot>.Empty;
                }

                return new ReadOnlySpan<GpuParticleInstanceSlot>(slots, 0, aliveCount);
            }
        }

        public int Capacity => slots.Length;
    }
}
