using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public static class GpuParticleBlobWriter
    {
        public const int ParticleStateStride = 64;
        public const int TrailStateStride = 32;
        public const int MeshTransformStride = 48;

        public static byte[] CreateBlob(
            GpuParticleBlobParticleState[] particleStates,
            GpuParticleBlobTrailState[] trailStates,
            GpuParticleBlobMeshTransform[] meshTransforms,
            float sampleRate,
            float duration,
            int trackCount)
        {
            particleStates ??= Array.Empty<GpuParticleBlobParticleState>();
            trailStates ??= Array.Empty<GpuParticleBlobTrailState>();
            meshTransforms ??= Array.Empty<GpuParticleBlobMeshTransform>();

            byte[] particleBytes = SerializeParticleStates(particleStates);
            byte[] trailBytes = SerializeTrailStates(trailStates);
            byte[] meshBytes = SerializeMeshTransforms(meshTransforms);

            int sectionCount = 0;
            if (particleBytes.Length > 0) sectionCount++;
            if (trailBytes.Length > 0) sectionCount++;
            if (meshBytes.Length > 0) sectionCount++;

            int sectionTableOffset = GpuParticleBlobFormat.HeaderSize;
            int dataOffset = Align16(sectionTableOffset + sectionCount * GpuParticleBlobFormat.SectionRecordSize);

            var sections = new System.Collections.Generic.List<(GpuParticleSectionType type, byte[] bytes)>();
            var sectionRecords = new System.Collections.Generic.List<GpuParticleBlobSection>();

            if (particleBytes.Length > 0)
            {
                sectionRecords.Add(new GpuParticleBlobSection(dataOffset, particleBytes.Length, (int)GpuParticleSectionType.ParticleState, 0));
                sections.Add((GpuParticleSectionType.ParticleState, particleBytes));
                dataOffset = Align16(dataOffset + particleBytes.Length);
            }

            if (trailBytes.Length > 0)
            {
                sectionRecords.Add(new GpuParticleBlobSection(dataOffset, trailBytes.Length, (int)GpuParticleSectionType.TrailState, 0));
                sections.Add((GpuParticleSectionType.TrailState, trailBytes));
                dataOffset = Align16(dataOffset + trailBytes.Length);
            }

            if (meshBytes.Length > 0)
            {
                sectionRecords.Add(new GpuParticleBlobSection(dataOffset, meshBytes.Length, (int)GpuParticleSectionType.MeshTransform, 0));
                sections.Add((GpuParticleSectionType.MeshTransform, meshBytes));
                dataOffset = Align16(dataOffset + meshBytes.Length);
            }

            int totalLength = dataOffset;
            byte[] blob = new byte[totalLength];

            // Header
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(0, 4), GpuParticleBlobFormat.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(4, 4), GpuParticleBlobFormat.SchemaVersion);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(8, 4), totalLength);
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(12, 4), 0); // CRC placeholder
            WriteSingle(blob, 16, sampleRate);
            WriteSingle(blob, 20, duration);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(24, 4), sectionCount);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(28, 4), sectionTableOffset);

            // Section table
            for (int i = 0; i < sectionRecords.Count; i++)
            {
                GpuParticleBlobSection section = sectionRecords[i];
                int recordOffset = sectionTableOffset + i * GpuParticleBlobFormat.SectionRecordSize;
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(recordOffset, 4), section.Offset);
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(recordOffset + 4, 4), section.Length);
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(recordOffset + 8, 4), section.Type);
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(recordOffset + 12, 4), section.Flags);
            }

            // Section data
            int writeOffset = sectionTableOffset + sectionCount * GpuParticleBlobFormat.SectionRecordSize;
            writeOffset = Align16(writeOffset);
            foreach (var (_, bytes) in sections)
            {
                bytes.CopyTo(blob, writeOffset);
                writeOffset = Align16(writeOffset + bytes.Length);
            }

            // CRC
            uint crc = GpuParticleCrc32.Compute(blob, GpuParticleBlobFormat.CrcOffset, GpuParticleBlobFormat.CrcSize);
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(GpuParticleBlobFormat.CrcOffset, 4), crc);

            return blob;
        }

        private static byte[] SerializeParticleStates(GpuParticleBlobParticleState[] states)
        {
            byte[] bytes = new byte[states.Length * ParticleStateStride];
            for (int i = 0; i < states.Length; i++)
            {
                WriteParticleState(bytes, i * ParticleStateStride, states[i]);
            }
            return bytes;
        }

        private static void WriteParticleState(byte[] bytes, int offset, GpuParticleBlobParticleState state)
        {
            WriteVector3(bytes, offset + 0, state.Position);
            WriteVector3(bytes, offset + 12, state.Velocity);
            WriteSingle(bytes, offset + 24, state.Size);
            WriteVector4(bytes, offset + 32, state.Rotation);
            WriteColor32(bytes, offset + 48, state.Color);
            WriteSingle(bytes, offset + 52, state.Lifetime);
            WriteUInt32(bytes, offset + 56, state.Seed);
            // bytes 60-63 padding
        }

        private static byte[] SerializeTrailStates(GpuParticleBlobTrailState[] states)
        {
            byte[] bytes = new byte[states.Length * TrailStateStride];
            for (int i = 0; i < states.Length; i++)
            {
                WriteTrailState(bytes, i * TrailStateStride, states[i]);
            }
            return bytes;
        }

        private static void WriteTrailState(byte[] bytes, int offset, GpuParticleBlobTrailState state)
        {
            WriteVector3(bytes, offset + 0, state.Position);
            WriteSingle(bytes, offset + 12, state.Width);
            WriteColor32(bytes, offset + 16, state.Color);
            WriteUInt32(bytes, offset + 20, state.ParticleId);
            // bytes 24-31 padding
        }

        private static byte[] SerializeMeshTransforms(GpuParticleBlobMeshTransform[] transforms)
        {
            byte[] bytes = new byte[transforms.Length * MeshTransformStride];
            for (int i = 0; i < transforms.Length; i++)
            {
                WriteMeshTransform(bytes, i * MeshTransformStride, transforms[i]);
            }
            return bytes;
        }

        private static void WriteMeshTransform(byte[] bytes, int offset, GpuParticleBlobMeshTransform transform)
        {
            WriteVector3(bytes, offset + 0, transform.Position);
            WriteQuaternion(bytes, offset + 16, transform.Rotation);
            WriteVector3(bytes, offset + 32, transform.Scale);
            WriteColor32(bytes, offset + 44, transform.Color);
        }

        private static void WriteVector3(byte[] bytes, int offset, Vector3 value)
        {
            WriteSingle(bytes, offset + 0, value.x);
            WriteSingle(bytes, offset + 4, value.y);
            WriteSingle(bytes, offset + 8, value.z);
        }

        private static void WriteVector4(byte[] bytes, int offset, Vector4 value)
        {
            WriteSingle(bytes, offset + 0, value.x);
            WriteSingle(bytes, offset + 4, value.y);
            WriteSingle(bytes, offset + 8, value.z);
            WriteSingle(bytes, offset + 12, value.w);
        }

        private static void WriteQuaternion(byte[] bytes, int offset, Quaternion value)
        {
            WriteSingle(bytes, offset + 0, value.x);
            WriteSingle(bytes, offset + 4, value.y);
            WriteSingle(bytes, offset + 8, value.z);
            WriteSingle(bytes, offset + 12, value.w);
        }

        private static void WriteColor32(byte[] bytes, int offset, Color32 color)
        {
            bytes[offset + 0] = color.r;
            bytes[offset + 1] = color.g;
            bytes[offset + 2] = color.b;
            bytes[offset + 3] = color.a;
        }

        private static void WriteSingle(byte[] bytes, int offset, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        }

        public static int Align16(int value)
        {
            return (value + 15) & ~15;
        }
    }
}
