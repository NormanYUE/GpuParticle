using System;
using System.Buffers.Binary;

namespace GpuParticle.Runtime
{
    public static class GpuParticleBlobReader
    {
        public static bool TryRead(byte[]? bytes, out GpuParticleBlob blob, out GpuParticleFailure failure)
        {
            blob = null!;

            if (bytes == null || bytes.Length < GpuParticleBlobFormat.HeaderSize)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadTooSmall,
                    "Payload is missing or smaller than the GPU particle header.");
                return false;
            }

            ReadOnlySpan<byte> span = bytes;
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
            if (magic != GpuParticleBlobFormat.Magic)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadMagicMismatch,
                    "Payload magic is not HLGP.");
                return false;
            }

            int schemaVersion = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
            if (schemaVersion != GpuParticleBlobFormat.SchemaVersion)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadSchemaMismatch,
                    $"Payload schema {schemaVersion} is not supported.");
                return false;
            }

            int totalLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
            if (totalLength != bytes.Length || totalLength % 16 != 0)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadLengthMismatch,
                    "Payload total length does not match the byte array length or is not 16-byte aligned.");
                return false;
            }

            uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(GpuParticleBlobFormat.CrcOffset, 4));
            uint actualCrc = GpuParticleCrc32.Compute(
                bytes,
                GpuParticleBlobFormat.CrcOffset,
                GpuParticleBlobFormat.CrcSize);
            if (storedCrc != actualCrc)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadCrcMismatch,
                    "Payload CRC mismatch.");
                return false;
            }

            float sampleRate = ReadSingleLittleEndian(span.Slice(16, 4));
            float duration = ReadSingleLittleEndian(span.Slice(20, 4));
            int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(24, 4));
            int sectionTableOffset = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(28, 4));

            if (sampleRate <= 0f || float.IsNaN(sampleRate) || duration < 0f || float.IsNaN(duration) || sectionCount < 0)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadSectionTableInvalid,
                    "Payload header contains invalid timing or section values.");
                return false;
            }

            if (sectionTableOffset < GpuParticleBlobFormat.HeaderSize ||
                sectionTableOffset > totalLength ||
                sectionTableOffset % 16 != 0)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadSectionTableInvalid,
                    "Payload section table offset is invalid.");
                return false;
            }

            int sectionTableEnd = sectionTableOffset + sectionCount * GpuParticleBlobFormat.SectionRecordSize;
            if (sectionTableEnd > totalLength || sectionTableEnd % 16 != 0)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadSectionTableInvalid,
                    "Payload section table length is invalid.");
                return false;
            }

            GpuParticleBlobSection[] sections = new GpuParticleBlobSection[sectionCount];
            for (int i = 0; i < sectionCount; i++)
            {
                int recordOffset = sectionTableOffset + i * GpuParticleBlobFormat.SectionRecordSize;
                int offset = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(recordOffset, 4));
                int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(recordOffset + 4, 4));
                int type = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(recordOffset + 8, 4));
                int flags = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(recordOffset + 12, 4));

                if (offset < GpuParticleBlobFormat.HeaderSize || length < 0 || offset > totalLength || totalLength - offset < length)
                {
                    failure = new GpuParticleFailure(
                        GpuParticleFailureCode.PayloadSectionOutOfRange,
                        $"Payload section {i} points outside the payload.");
                    return false;
                }

                if (offset % 16 != 0 || length % 16 != 0)
                {
                    failure = new GpuParticleFailure(
                        GpuParticleFailureCode.PayloadSectionMisaligned,
                        $"Payload section {i} is not 16-byte aligned.");
                    return false;
                }

                sections[i] = new GpuParticleBlobSection(offset, length, type, flags);
            }

            GpuParticleBlobHeader header = new GpuParticleBlobHeader(
                schemaVersion,
                totalLength,
                storedCrc,
                sampleRate,
                duration,
                sectionCount,
                sectionTableOffset);
            blob = new GpuParticleBlob(bytes, header, sections);
            failure = GpuParticleFailure.None;
            return true;
        }

        private static float ReadSingleLittleEndian(ReadOnlySpan<byte> bytes)
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            return BitConverter.Int32BitsToSingle(value);
        }
    }

    public static class GpuParticleCrc32
    {
        private const uint Polynomial = 0xEDB88320u;

        public static uint Compute(byte[] bytes, int zeroOffset = -1, int zeroLength = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return Compute((ReadOnlySpan<byte>)bytes, zeroOffset, zeroLength);
        }

        public static uint Compute(ReadOnlySpan<byte> bytes, int zeroOffset = -1, int zeroLength = 0)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = i >= zeroOffset && i < zeroOffset + zeroLength ? (byte)0 : bytes[i];
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = (uint)-(int)(crc & 1u);
                    crc = (crc >> 1) ^ (Polynomial & mask);
                }
            }

            return ~crc;
        }
    }
}
