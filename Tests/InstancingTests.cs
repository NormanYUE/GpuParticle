using System;
using System.Reflection;
using System.Runtime.Serialization;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Tests
{
    internal static class InstancingTests
    {
        public static void RunAll()
        {
            Run("allocate and free slot", AllocateAndFreeSlot);
            Run("generation prevents stale handle", GenerationPreventsStaleHandle);
            Run("pool capacity caps allocation", PoolCapacityCapsAllocation);
            Run("update advances elapsed time", UpdateAdvancesElapsedTime);
        }

        private static GpuParticleClip CreateTestClip(float duration)
        {
            var clip = (GpuParticleClip)FormatterServices.GetUninitializedObject(typeof(GpuParticleClip));
            FieldInfo durationField = typeof(GpuParticleClip).GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance)!;
            durationField.SetValue(clip, duration);
            return clip;
        }

        private static void AllocateAndFreeSlot()
        {
            var pool = new GpuParticleInstancePool(4);
            GpuParticleClip clip = CreateTestClip(1f);

            int index = pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            if (index < 0)
            {
                throw new Exception("Expected slot allocation to succeed.");
            }

            int generation = pool.GetGeneration(index);
            ref GpuParticleInstanceSlot slot = ref pool.GetSlot(index, generation);
            if (!slot.IsAlive)
            {
                throw new Exception("Expected slot to be alive.");
            }

            pool.Free(index, generation);
            if (pool.IsAlive(index, generation))
            {
                throw new Exception("Expected slot to be freed.");
            }
        }

        private static void GenerationPreventsStaleHandle()
        {
            var pool = new GpuParticleInstancePool(4);
            GpuParticleClip clip = CreateTestClip(1f);

            int index = pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            int gen = pool.GetGeneration(index);
            pool.Free(index, gen);
            int index2 = pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            if (index2 != index)
            {
                throw new Exception("Expected slot reuse.");
            }

            int newGen = pool.GetGeneration(index2);
            if (newGen == gen)
            {
                throw new Exception("Expected generation to increment.");
            }

            if (pool.IsAlive(index, gen))
            {
                throw new Exception("Old generation should not be alive.");
            }
        }

        private static void PoolCapacityCapsAllocation()
        {
            var pool = new GpuParticleInstancePool(2);
            GpuParticleClip clip = CreateTestClip(1f);

            pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            int index = pool.Allocate(clip, Matrix4x4.identity, 1f, 0u, true);
            if (index >= 0)
            {
                throw new Exception("Expected allocation to fail at capacity.");
            }
        }

        private static void UpdateAdvancesElapsedTime()
        {
            var pool = new GpuParticleInstancePool(4);
            GpuParticleClip clip = CreateTestClip(2f);

            int index = pool.Allocate(clip, Matrix4x4.identity, 2f, 0u, false);
            int gen = pool.GetGeneration(index);
            pool.UpdateAll(0.5f);
            ref GpuParticleInstanceSlot slot = ref pool.GetSlot(index, gen);
            if (Mathf.Abs(slot.ElapsedTime - 1f) > 0.001f)
            {
                throw new Exception($"Expected elapsed time 1.0, got {slot.ElapsedTime}.");
            }
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
    }
}
