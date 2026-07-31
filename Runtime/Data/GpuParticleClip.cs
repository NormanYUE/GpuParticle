using System;
using UnityEngine;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleClip : ScriptableObject
    {
        // Legacy fields (kept to avoid asset deserialization issues during migration)
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

        // VAT + MeshRenderer Prefab fields
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private int frameCount;
        [SerializeField] private int maxParticles;
        [SerializeField] private Texture2D positionSizeTexture = null!;
        [SerializeField] private Texture2D colorTexture = null!;
        [SerializeField] private Texture2D rotationTexture = null!;
        [SerializeField] private Texture2D velocityLifetimeTexture = null!;

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

        public GameObject Prefab => prefab;
        public int FrameCount => frameCount;
        public int MaxParticles => maxParticles;
        public Texture2D PositionSizeTexture => positionSizeTexture;
        public Texture2D ColorTexture => colorTexture;
        public Texture2D RotationTexture => rotationTexture;
        public Texture2D VelocityLifetimeTexture => velocityLifetimeTexture;

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

        public void ConfigureVat(
            GameObject prefabAsset,
            float clipDuration,
            int clipFrameCount,
            int clipMaxParticles,
            Bounds bounds,
            Texture2D posSizeTex,
            Texture2D colorTex,
            Texture2D rotTex,
            Texture2D velLifeTex)
        {
            prefab = prefabAsset;
            duration = Mathf.Max(0f, clipDuration);
            frameCount = Mathf.Max(1, clipFrameCount);
            maxParticles = Mathf.Max(0, clipMaxParticles);
            localBounds = bounds;
            positionSizeTexture = posSizeTex;
            colorTexture = colorTex;
            rotationTexture = rotTex;
            velocityLifetimeTexture = velLifeTex;
            status = prefabAsset != null ? GpuParticleBakeStatus.GpuReady : GpuParticleBakeStatus.Native;
        }

        public bool TryValidateRuntime(out GpuParticleFailure failure)
        {
            if (prefab != null)
            {
                failure = GpuParticleFailure.None;
                return true;
            }

            // Legacy validation path (will be removed once migration is complete)
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
