using UnityEngine;
using System.Collections.Generic;

namespace GpuParticle.Runtime
{
    public static class GpuParticleNativeFallback
    {
        public static void Play(GpuParticleBinding binding, float timeScale, uint seedVariant = uint.MaxValue)
        {
            if (binding == null)
            {
                return;
            }

            binding.RestoreNativeState();
            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            ParticleSystem[] roots = GetRootSystems(binding.transform, states);
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system == null)
                {
                    continue;
                }

                if (seedVariant != uint.MaxValue)
                {
                    system.useAutoRandomSeed = false;
                    system.randomSeed = seedVariant == 0 ? states[i].RandomSeed : seedVariant;
                }

                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = states[i].SimulationSpeed * Mathf.Max(0f, timeScale);
            }

            StopAndClear(states);
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].Play(true);
            }
        }

        public static void RestoreAndCatchUp(GpuParticleBinding binding, GpuParticleNativeRestoreState restoreState, float sampleRate)
        {
            if (binding == null)
            {
                return;
            }

            binding.RestoreNativeState();
            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            ParticleSystem[] roots = GetRootSystems(binding.transform, states);
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system == null)
                {
                    continue;
                }

                system.useAutoRandomSeed = false;
                system.randomSeed = restoreState.SeedVariant == uint.MaxValue
                    ? states[i].RandomSeed
                    : restoreState.SeedVariant;

                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = states[i].SimulationSpeed;
            }

            if (roots.Length == 0)
            {
                return;
            }

            StopAndClear(states);
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].Play(true);
            }

            float dt = sampleRate > 0f ? 1f / sampleRate : 1f / 120f;
            float remaining = Mathf.Max(0f, restoreState.ElapsedClipTime);
            while (remaining > dt)
            {
                SimulateRoots(roots, dt);
                remaining -= dt;
            }

            if (remaining > 0f)
            {
                SimulateRoots(roots, remaining);
            }

            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = states[i].SimulationSpeed * Mathf.Max(0f, restoreState.TimeScale);
                if (restoreState.IsPaused)
                {
                    system.Pause(true);
                }
            }
        }

        public static void Stop(GpuParticleBinding binding, bool clear)
        {
            if (binding == null)
            {
                return;
            }

            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system != null)
                {
                    system.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public static void Pause(GpuParticleBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            for (int i = 0; i < states.Length; i++)
            {
                states[i].System?.Pause(true);
            }
        }

        public static void Resume(GpuParticleBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            for (int i = 0; i < states.Length; i++)
            {
                states[i].System?.Play(true);
            }
        }

        public static void SetTimeScale(GpuParticleBinding binding, float timeScale)
        {
            if (binding == null)
            {
                return;
            }

            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = states[i].SimulationSpeed * Mathf.Max(0f, timeScale);
            }
        }

        public static bool IsAnyAlive(GpuParticleBinding binding)
        {
            if (binding == null)
            {
                return false;
            }

            GpuParticleNativeSystemState[] states = binding.NativeSystemStates;
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system != null && system.IsAlive(true))
                {
                    return true;
                }
            }

            return false;
        }

        private static void StopAndClear(GpuParticleNativeSystemState[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Clear(true);
                }
            }
        }

        private static void SimulateRoots(ParticleSystem[] roots, float dt)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].Simulate(dt, true, false, false);
            }
        }

        private static ParticleSystem[] GetRootSystems(Transform bindingRoot, GpuParticleNativeSystemState[] states)
        {
            List<ParticleSystem> roots = new List<ParticleSystem>();
            for (int i = 0; i < states.Length; i++)
            {
                ParticleSystem system = states[i].System;
                if (system == null)
                {
                    continue;
                }

                bool hasParticleAncestor = false;
                Transform parent = system.transform.parent;
                while (parent != null && parent != bindingRoot.parent)
                {
                    if (parent.GetComponent<ParticleSystem>() != null)
                    {
                        hasParticleAncestor = true;
                        break;
                    }

                    if (parent == bindingRoot)
                    {
                        break;
                    }

                    parent = parent.parent;
                }

                if (!hasParticleAncestor)
                {
                    roots.Add(system);
                }
            }

            return roots.ToArray();
        }
    }
}
