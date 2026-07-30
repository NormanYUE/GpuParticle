using System;

namespace GpuParticle.Runtime
{
    public readonly struct GpuParticleHandle : IEquatable<GpuParticleHandle>
    {
        public static readonly GpuParticleHandle Invalid = new GpuParticleHandle(-1, 0);

        public GpuParticleHandle(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public int Slot { get; }
        public uint Generation { get; }
        public bool IsValid => Slot >= 0 && Generation != 0;

        public bool Equals(GpuParticleHandle other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is GpuParticleHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Slot * 397) ^ (int)Generation;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{Slot}:{Generation}" : "Invalid";
        }
    }
}
