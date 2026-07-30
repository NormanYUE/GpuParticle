using System;
using System.Buffers.Binary;
using GpuParticle.Runtime;

namespace GpuParticle.Tests
{
    internal static class GpuParticleBlobTestData
    {
        public static byte[] CreateBlob(int trackCount, int sectionCount)
        {
            int payloadOffset = GpuParticleBlobFormat.HeaderSize;
            int sectionTableOffset = payloadOffset + sectionCount * GpuParticleBlobFormat.TrackSectionSize;
            int totalLength = sectionTableOffset + sectionCount * GpuParticleBlobFormat.SectionRecordSize;
            byte[] bytes = new byte[totalLength];

            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), GpuParticleBlobFormat.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), GpuParticleBlobFormat.SchemaVersion);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), totalLength);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16, 4), 120f);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(20, 4), 1f);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24, 4), trackCount);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), sectionTableOffset);

            for (int i = 0; i < sectionCount; i++)
            {
                int recordOffset = sectionTableOffset + i * GpuParticleBlobFormat.SectionRecordSize;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset, 4), payloadOffset + i * GpuParticleBlobFormat.TrackSectionSize);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 4, 4), GpuParticleBlobFormat.TrackSectionSize);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 8, 4), i + 1);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 12, 4), 0);
            }

            uint crc = GpuParticleCrc32.Compute(bytes, GpuParticleBlobFormat.CrcOffset, GpuParticleBlobFormat.CrcSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(GpuParticleBlobFormat.CrcOffset, 4), crc);
            return bytes;
        }
    }
}
