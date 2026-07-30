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
            Run("track count must match section count", TrackCountMustMatchSectionCount);
            Run("file content hash changes with file bytes", FileContentHashChangesWithFileBytes);
            Console.WriteLine("All tests passed.");
            return 0;
        }

        private static void ValidBlobHeaderParses()
        {
            byte[] bytes = GpuParticleBlobTestData.CreateBlob(trackCount: 2, sectionCount: 2);

            if (!GpuParticleBlobReader.TryRead(bytes, out GpuParticleBlob blob, out GpuParticleFailure failure))
            {
                throw new Exception($"Expected blob to parse, got {failure.Code}: {failure.Message}");
            }

            AssertEqual(GpuParticleBlobFormat.SchemaVersion, blob.Header.SchemaVersion, "schema");
            AssertEqual(2, blob.Header.TrackCount, "track count");
            AssertEqual(2, blob.Sections.Length, "section count");
            AssertEqual(32, blob.Sections[0].Offset, "section offset");
            AssertEqual(16, blob.Sections[0].Length, "section length");
        }

        private static void CrcMismatchIsRejected()
        {
            byte[] bytes = GpuParticleBlobTestData.CreateBlob(trackCount: 1, sectionCount: 0);
            bytes[20] ^= 0x7F;

            if (GpuParticleBlobReader.TryRead(bytes, out _, out GpuParticleFailure failure))
            {
                throw new Exception("Expected CRC mismatch to be rejected.");
            }

            AssertEqual(GpuParticleFailureCode.PayloadCrcMismatch, failure.Code, "failure code");
        }

        private static void TrackCountMustMatchSectionCount()
        {
            byte[] bytes = GpuParticleBlobTestData.CreateBlob(trackCount: 2, sectionCount: 1);

            if (GpuParticleBlobReader.TryRead(bytes, out _, out GpuParticleFailure failure))
            {
                throw new Exception("Expected track/section count mismatch to be rejected.");
            }

            AssertEqual(GpuParticleFailureCode.PayloadSectionTableInvalid, failure.Code, "failure code");
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
