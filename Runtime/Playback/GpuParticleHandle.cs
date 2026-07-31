using System;

namespace GpuParticle.Runtime
{
    public readonly struct GpuParticleHandle : IEquatable<GpuParticleHandle>
    {
        public static readonly GpuParticleHandle Invalid = new GpuParticleHandle(-1, -1);

        public GpuParticleHandle(int slotIndex, int generation)
        {
            SlotIndex = slotIndex;
            Generation = generation;
        }

        public int SlotIndex { get; }
        public int Generation { get; }
        public bool IsValid => SlotIndex >= 0;

        public bool Equals(GpuParticleHandle other)
        {
            return SlotIndex == other.SlotIndex && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is GpuParticleHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, Generation);
        }

        public override string ToString()
        {
            return IsValid ? $"VatSlot:{SlotIndex}:{Generation}" : "Invalid";
        }
    }
}
