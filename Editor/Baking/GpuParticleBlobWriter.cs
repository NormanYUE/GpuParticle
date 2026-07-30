using System;
using System.Buffers.Binary;
using GpuParticle.Runtime;

namespace GpuParticle.Editor
{
    internal static class GpuParticleBlobWriter
    {
        public static byte[] CreateHeaderOnlyBlob(float sampleRate, float duration, int trackCount)
        {
            trackCount = Math.Max(0, trackCount);
            int trackDataOffset = GpuParticleBlobFormat.HeaderSize;
            int sectionTableOffset = trackDataOffset + trackCount * GpuParticleBlobFormat.TrackSectionSize;
            int totalLength = sectionTableOffset + trackCount * GpuParticleBlobFormat.SectionRecordSize;
            byte[] bytes = new byte[totalLength];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), GpuParticleBlobFormat.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), GpuParticleBlobFormat.SchemaVersion);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), totalLength);
            WriteSingleLittleEndian(bytes.AsSpan(16, 4), sampleRate);
            WriteSingleLittleEndian(bytes.AsSpan(20, 4), duration);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24, 4), trackCount);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), sectionTableOffset);

            for (int i = 0; i < trackCount; i++)
            {
                int sectionOffset = sectionTableOffset + i * GpuParticleBlobFormat.SectionRecordSize;
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan(sectionOffset, 4),
                    trackDataOffset + i * GpuParticleBlobFormat.TrackSectionSize);
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan(sectionOffset + 4, 4),
                    GpuParticleBlobFormat.TrackSectionSize);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sectionOffset + 8, 4), i + 1);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sectionOffset + 12, 4), 0);
            }

            uint crc = GpuParticleCrc32.Compute(bytes, GpuParticleBlobFormat.CrcOffset, GpuParticleBlobFormat.CrcSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(GpuParticleBlobFormat.CrcOffset, 4), crc);
            return bytes;
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(raw);
            }

            raw.CopyTo(destination);
        }
    }
}
