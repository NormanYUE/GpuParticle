using System;

namespace GpuParticle.Runtime
{
    public static class GpuParticleBlobFormat
    {
        public const uint Magic = 0x50474C48;
        public const int SchemaVersion = 2;
        public const int HeaderSize = 32;
        public const int SectionRecordSize = 16;
        public const int TrackSectionSize = 16;
        public const int CrcOffset = 12;
        public const int CrcSize = 4;
    }

    public enum GpuParticleSectionType : int
    {
        ParticleState = 0,
        TrailState = 1,
        MeshTransform = 2,
    }

    public readonly struct GpuParticleBlobHeader
    {
        public GpuParticleBlobHeader(
            int schemaVersion,
            int totalLength,
            uint crc32,
            float sampleRate,
            float duration,
            int trackCount,
            int sectionTableOffset)
        {
            SchemaVersion = schemaVersion;
            TotalLength = totalLength;
            Crc32 = crc32;
            SampleRate = sampleRate;
            Duration = duration;
            TrackCount = trackCount;
            SectionTableOffset = sectionTableOffset;
        }

        public int SchemaVersion { get; }
        public int TotalLength { get; }
        public uint Crc32 { get; }
        public float SampleRate { get; }
        public float Duration { get; }
        public int TrackCount { get; }
        public int SectionTableOffset { get; }
    }

    public readonly struct GpuParticleBlobSection
    {
        public GpuParticleBlobSection(int offset, int length, int type, int flags)
        {
            Offset = offset;
            Length = length;
            Type = type;
            Flags = flags;
        }

        public int Offset { get; }
        public int Length { get; }
        public int Type { get; }
        public int Flags { get; }
    }

    public sealed class GpuParticleBlob
    {
        public GpuParticleBlob(byte[] bytes, GpuParticleBlobHeader header, GpuParticleBlobSection[] sections)
        {
            Bytes = bytes;
            Header = header;
            Sections = sections;
        }

        public byte[] Bytes { get; }
        public GpuParticleBlobHeader Header { get; }
        public GpuParticleBlobSection[] Sections { get; }
    }
}
