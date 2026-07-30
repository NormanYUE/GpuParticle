using System;
using System.Buffers.Binary;
using GpuParticle.Runtime;

namespace GpuParticle.Tests
{
    internal static class GpuParticleBlobTestData
    {
        public static byte[] CreateBlob(int sectionCount)
        {
            int sectionTableOffset = GpuParticleBlobFormat.HeaderSize;
            int dataOffset = GpuParticleBlobWriter.Align16(sectionTableOffset + sectionCount * GpuParticleBlobFormat.SectionRecordSize);
            int totalLength = dataOffset + sectionCount * 16;
            byte[] bytes = new byte[totalLength];

            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), GpuParticleBlobFormat.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), GpuParticleBlobFormat.SchemaVersion);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), totalLength);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16, 4), 120f);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(20, 4), 1f);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24, 4), sectionCount);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), sectionTableOffset);

            for (int i = 0; i < sectionCount; i++)
            {
                int recordOffset = sectionTableOffset + i * GpuParticleBlobFormat.SectionRecordSize;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset, 4), dataOffset + i * 16);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 4, 4), 16);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 8, 4), i + 1);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(recordOffset + 12, 4), 0);
            }

            uint crc = GpuParticleCrc32.Compute(bytes, GpuParticleBlobFormat.CrcOffset, GpuParticleBlobFormat.CrcSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(GpuParticleBlobFormat.CrcOffset, 4), crc);
            return bytes;
        }
    }
}
