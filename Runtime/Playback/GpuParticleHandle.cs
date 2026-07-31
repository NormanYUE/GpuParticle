using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public readonly struct GpuParticleHandle : IEquatable<GpuParticleHandle>
    {
        public static readonly GpuParticleHandle Invalid = new GpuParticleHandle(null!);

        public GpuParticleHandle(GameObject target)
        {
            Target = target;
        }

        public GameObject Target { get; }
        public bool IsValid => Target != null;

        public bool Equals(GpuParticleHandle other)
        {
            return ReferenceEquals(Target, other.Target);
        }

        public override bool Equals(object obj)
        {
            return obj is GpuParticleHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Target != null ? Target.GetInstanceID() : 0;
        }

        public override string ToString()
        {
            return IsValid ? $"Vat:{Target.name}" : "Invalid";
        }
    }
}
