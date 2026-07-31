using System;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Tests
{
    internal static class StateInterpolationTests
    {
        public static void RunAll()
        {
            Run("position interpolation", PositionInterpolation);
            Run("velocity interpolation", VelocityInterpolation);
            Run("size interpolation", SizeInterpolation);
            Run("color interpolation", ColorInterpolation);
            Run("lifetime interpolation", LifetimeInterpolation);
            Run("seed carried over unchanged", SeedCarriedOverUnchanged);
            Run("lerp at t=0 returns state A", LerpAtZeroReturnsStateA);
            Run("lerp at t=1 returns state B", LerpAtOneReturnsStateB);
        }

        private static void PositionInterpolation()
        {
            var a = new GpuParticleBlobParticleState { Position = Vector3.zero };
            var b = new GpuParticleBlobParticleState { Position = new Vector3(2f, 4f, 6f) };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(new Vector3(1f, 2f, 3f), result.Position, "position");
        }

        private static void VelocityInterpolation()
        {
            var a = new GpuParticleBlobParticleState { Velocity = new Vector3(1f, 0f, -1f) };
            var b = new GpuParticleBlobParticleState { Velocity = new Vector3(3f, 2f, 1f) };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(new Vector3(2f, 1f, 0f), result.Velocity, "velocity");
        }

        private static void SizeInterpolation()
        {
            var a = new GpuParticleBlobParticleState { Size = 1f };
            var b = new GpuParticleBlobParticleState { Size = 3f };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(2f, result.Size, "size");
        }

        private static void ColorInterpolation()
        {
            var a = new GpuParticleBlobParticleState { Color = new Color32(0, 0, 0, 255) };
            var b = new GpuParticleBlobParticleState { Color = new Color32(2, 0, 0, 255) };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(new Color32(1, 0, 0, 255), result.Color, "color");
        }

        private static void LifetimeInterpolation()
        {
            var a = new GpuParticleBlobParticleState { Lifetime = 0f };
            var b = new GpuParticleBlobParticleState { Lifetime = 2f };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(1f, result.Lifetime, "lifetime");
        }

        private static void SeedCarriedOverUnchanged()
        {
            var a = new GpuParticleBlobParticleState { Seed = 12345u };
            var b = new GpuParticleBlobParticleState { Seed = 99999u };
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0.5f);
            AssertEqual(12345u, result.Seed, "seed");
        }

        private static void LerpAtZeroReturnsStateA()
        {
            var a = CreateStateA();
            var b = CreateStateB();
            var result = GpuParticleStateInterpolation.Lerp(a, b, 0f);
            AssertEqual(a.Position, result.Position, "position");
            AssertEqual(a.Velocity, result.Velocity, "velocity");
            AssertEqual(a.Size, result.Size, "size");
            AssertEqual(a.Rotation, result.Rotation, "rotation");
            AssertEqual(a.Color, result.Color, "color");
            AssertEqual(a.Lifetime, result.Lifetime, "lifetime");
            AssertEqual(a.Seed, result.Seed, "seed");
        }

        private static void LerpAtOneReturnsStateB()
        {
            var a = CreateStateA();
            var b = CreateStateB();
            var result = GpuParticleStateInterpolation.Lerp(a, b, 1f);
            AssertEqual(b.Position, result.Position, "position");
            AssertEqual(b.Velocity, result.Velocity, "velocity");
            AssertEqual(b.Size, result.Size, "size");
            AssertEqual(b.Rotation, result.Rotation, "rotation");
            AssertEqual(b.Color, result.Color, "color");
            AssertEqual(b.Lifetime, result.Lifetime, "lifetime");
            AssertEqual(a.Seed, result.Seed, "seed");
        }

        private static GpuParticleBlobParticleState CreateStateA()
        {
            return new GpuParticleBlobParticleState
            {
                Position = new Vector3(1f, 2f, 3f),
                Velocity = new Vector3(0f, 1f, 0f),
                Size = 1.5f,
                Rotation = new Vector4(0f, 0f, 0f, 1f),
                Color = new Color32(255, 128, 64, 255),
                Lifetime = 0.5f,
                Seed = 12345u,
            };
        }

        private static GpuParticleBlobParticleState CreateStateB()
        {
            return new GpuParticleBlobParticleState
            {
                Position = new Vector3(4f, 5f, 6f),
                Velocity = new Vector3(2f, 0f, -1f),
                Size = 2.5f,
                Rotation = new Vector4(0.5f, 0.5f, 0.5f, 0.5f),
                Color = new Color32(64, 128, 255, 200),
                Lifetime = 1.5f,
                Seed = 99999u,
            };
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}");
                Console.Error.WriteLine(ex);
                Environment.Exit(1);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new Exception($"{name}: expected {expected}, got {actual}");
            }
        }
    }
}
