using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleClip : ScriptableObject
    {
        [SerializeField] private int schemaVersion = GpuParticleBlobFormat.SchemaVersion;
        [SerializeField] private string sourcePrefabGuid = string.Empty;
        [SerializeField] private string sourceContentHash = string.Empty;
        [SerializeField] private string bakeFingerprint = string.Empty;
        [SerializeField] private GpuParticleBakeStatus status = GpuParticleBakeStatus.Native;
        [SerializeField] private float duration;
        [SerializeField] private float sampleRate = 120f;
        [SerializeField] private bool loop;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private GpuParticleCapability requiredCapabilities = GpuParticleCapability.ComputePlayback;
        [SerializeField] private TextAsset payload = null!;
        [SerializeField] private GpuParticleRuntimeResources runtimeResources = null!;
        [SerializeField] private GpuParticleGeometryTrack[] geometryTracks = Array.Empty<GpuParticleGeometryTrack>();

        public int SchemaVersion => schemaVersion;
        public string SourcePrefabGuid => sourcePrefabGuid;
        public string SourceContentHash => sourceContentHash;
        public string BakeFingerprint => bakeFingerprint;
        public GpuParticleBakeStatus Status => status;
        public float Duration => duration;
        public float SampleRate => sampleRate;
        public bool Loop => loop;
        public Bounds LocalBounds => localBounds;
        public GpuParticleCapability RequiredCapabilities => requiredCapabilities;
        public TextAsset Payload => payload;
        public GpuParticleRuntimeResources RuntimeResources => runtimeResources;
        public GpuParticleGeometryTrack[] GeometryTracks => geometryTracks;
        public int GeometryTrackCount => geometryTracks?.Length ?? 0;

        public void Configure(
            string prefabGuid,
            string contentHash,
            string fingerprint,
            GpuParticleBakeStatus bakeStatus,
            float clipDuration,
            float clipSampleRate,
            bool isLooping,
            Bounds bounds,
            GpuParticleCapability capabilities,
            TextAsset payloadAsset,
            GpuParticleRuntimeResources? resources,
            GpuParticleGeometryTrack[] tracks)
        {
            schemaVersion = GpuParticleBlobFormat.SchemaVersion;
            sourcePrefabGuid = prefabGuid ?? string.Empty;
            sourceContentHash = contentHash ?? string.Empty;
            bakeFingerprint = fingerprint ?? string.Empty;
            status = bakeStatus;
            duration = Mathf.Max(0f, clipDuration);
            sampleRate = Mathf.Max(1f, clipSampleRate);
            loop = isLooping;
            localBounds = bounds;
            requiredCapabilities = capabilities;
            payload = payloadAsset;
            runtimeResources = resources == null ? null! : resources;
            geometryTracks = tracks ?? Array.Empty<GpuParticleGeometryTrack>();
        }

        public bool TryValidateRuntime(out GpuParticleFailure failure)
        {
            if (status != GpuParticleBakeStatus.GpuReady)
            {
                failure = new GpuParticleFailure(GpuParticleFailureCode.ClipNative, "Clip is marked Native.");
                return false;
            }

            if (payload == null)
            {
                failure = new GpuParticleFailure(GpuParticleFailureCode.MissingPayload, "Clip payload is missing.");
                return false;
            }

            if (!GpuParticleBlobReader.TryRead(payload.bytes, out GpuParticleBlob blob, out failure))
            {
                return false;
            }

            if (blob.Header.SchemaVersion != GpuParticleBlobFormat.SchemaVersion)
            {
                failure = new GpuParticleFailure(
                    GpuParticleFailureCode.PayloadSchemaMismatch,
                    $"Clip schema {blob.Header.SchemaVersion} does not match supported schema {GpuParticleBlobFormat.SchemaVersion}.");
                return false;
            }

            if (geometryTracks == null || geometryTracks.Length == 0)
            {
                failure = new GpuParticleFailure(GpuParticleFailureCode.MissingGeometry, "Clip has no playable geometry tracks.");
                return false;
            }

            failure = GpuParticleFailure.None;
            return true;
        }
    }
}
