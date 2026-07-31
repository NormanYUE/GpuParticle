using System;
using System.IO;
using GpuParticle.Editor;
using GpuParticle.Runtime;

namespace GpuParticle.Tests
{
    internal static class Program
    {
        public static int Main()
        {
            Run("valid blob header parses", ValidBlobHeaderParses);
            Run("crc mismatch is rejected", CrcMismatchIsRejected);
            Run("file content hash changes with file bytes", FileContentHashChangesWithFileBytes);
            BlobRoundtripTests.RunAll();
            StateInterpolationTests.RunAll();
            Console.WriteLine("All tests passed.");
            return 0;
        }

        private static void ValidBlobHeaderParses()
        {
            byte[] bytes = GpuParticleBlobTestData.CreateBlob(sectionCount: 2);

            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(GpuParticleBlobFormat.SchemaVersion, blob.Header.SchemaVersion, "schema");
            AssertEqual(2, blob.Header.TrackCount, "section count");
            AssertEqual(2, blob.Sections.Length, "section count");
            AssertEqual(64, blob.Sections[0].Offset, "section offset");
            AssertEqual(16, blob.Sections[0].Length, "section length");
        }

        private static void CrcMismatchIsRejected()
        {
            byte[] bytes = GpuParticleBlobTestData.CreateBlob(sectionCount: 0);
            bytes[20] ^= 0x7F;

            if (GpuParticleBlobReader.TryRead(bytes, out _, out GpuParticleFailure failure))
            {
                throw new Exception("Expected CRC mismatch to be rejected.");
            }

            AssertEqual(GpuParticleFailureCode.PayloadCrcMismatch, failure.Code, "failure code");
        }

        private static void FileContentHashChangesWithFileBytes()
        {
            string file = Path.Combine(Path.GetTempPath(), "gpuparticle-hash-test.txt");
            File.WriteAllText(file, "first");
            string first = GpuParticleSourceHasher.ComputeFileContentHashForTests(new[] { file });
            File.WriteAllText(file, "second");
            string second = GpuParticleSourceHasher.ComputeFileContentHashForTests(new[] { file });

            if (first == second)
            {
                throw new Exception("Expected content hash to change when file bytes change.");
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

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new Exception($"{name}: expected {expected}, got {actual}");
            }
        }
    }
}
