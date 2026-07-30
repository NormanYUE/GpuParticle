using System;
using GpuParticle.Runtime;
using UnityEngine;

namespace GpuParticle.Tests
{
    internal static class BlobRoundtripTests
    {
        public static void RunAll()
        {
            Run("particle state roundtrip", ParticleStateRoundtrip);
            Run("trail state roundtrip", TrailStateRoundtrip);
            Run("mesh transform roundtrip", MeshTransformRoundtrip);
            Run("empty sections roundtrip", EmptySectionsRoundtrip);
        }

        private static void ParticleStateRoundtrip()
        {
            var states = new[]
            {
                new GpuParticleBlobParticleState
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Velocity = new Vector3(0f, 1f, 0f),
                    Size = 1.5f,
                    Rotation = new Vector4(0f, 0f, 0f, 1f),
                    Color = new Color32(255, 128, 64, 255),
                    Lifetime = 0.5f,
                    Seed = 12345u,
                }
            };

            byte[] bytes = GpuParticleBlobWriter.CreateBlob(states, Array.Empty<GpuParticleBlobTrailState>(), Array.Empty<GpuParticleBlobMeshTransform>(), 60f, 1f, 1);
            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(1, blob.Sections.Length, "section count");
            AssertEqual((int)GpuParticleSectionType.ParticleState, blob.Sections[0].Type, "section type");
            AssertEqual(GpuParticleBlobWriter.ParticleStateStride, blob.Sections[0].Length, "section length");
        }

        private static void TrailStateRoundtrip()
        {
            var trails = new[]
            {
                new GpuParticleBlobTrailState
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Width = 0.5f,
                    Color = new Color32(255, 255, 255, 128),
                    ParticleId = 42u,
                }
            };

            byte[] bytes = GpuParticleBlobWriter.CreateBlob(Array.Empty<GpuParticleBlobParticleState>(), trails, Array.Empty<GpuParticleBlobMeshTransform>(), 60f, 1f, 1);
            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(1, blob.Sections.Length, "section count");
            AssertEqual((int)GpuParticleSectionType.TrailState, blob.Sections[0].Type, "section type");
            AssertEqual(GpuParticleBlobWriter.TrailStateStride, blob.Sections[0].Length, "section length");
        }

        private static void MeshTransformRoundtrip()
        {
            var transforms = new[]
            {
                new GpuParticleBlobMeshTransform
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                    Color = new Color32(255, 0, 0, 255),
                }
            };

            byte[] bytes = GpuParticleBlobWriter.CreateBlob(Array.Empty<GpuParticleBlobParticleState>(), Array.Empty<GpuParticleBlobTrailState>(), transforms, 60f, 1f, 1);
            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(1, blob.Sections.Length, "section count");
            AssertEqual((int)GpuParticleSectionType.MeshTransform, blob.Sections[0].Type, "section type");
            AssertEqual(GpuParticleBlobWriter.MeshTransformStride, blob.Sections[0].Length, "section length");
        }

        private static void EmptySectionsRoundtrip()
        {
            byte[] bytes = GpuParticleBlobWriter.CreateBlob(
                Array.Empty<GpuParticleBlobParticleState>(),
                Array.Empty<GpuParticleBlobTrailState>(),
                Array.Empty<GpuParticleBlobMeshTransform>(),
                60f, 1f, 0);

            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(0, blob.Sections.Length, "section count");
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
