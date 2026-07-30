using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    public enum GpuParticleBakeStatus : byte
    {
        Native = 0,
        GpuReady = 1,
    }

    public enum GpuParticleRenderMode : byte
    {
        Billboard,
        StretchedBillboard,
        Mesh,
    }

    public enum GpuParticleAlignment : byte
    {
        View,
        Facing,
        World,
        Local,
    }

    [Flags]
    public enum GpuParticleCapability : uint
    {
        None = 0,
        GeometryPlayback = 1u << 0,
        ComputePlayback = 1u << 1,
        Vulkan = 1u << 8,
        OpenGLES3 = 1u << 9,
        Metal = 1u << 10,
        Direct3D11 = 1u << 11,
        Direct3D12 = 1u << 12,
    }

    public enum GpuParticleFailureCode : ushort
    {
        None = 0,
        NativeRequired = 1,
        ClipNative = 2,
        MissingClip = 3,
        MissingPayload = 4,
        MissingRuntimeResources = 5,
        MissingGeometry = 6,
        UnsupportedPlatform = 7,
        MissingRendererFeature = 8,
        UnsupportedShader = 9,
        DynamicWorldInput = 10,
        DynamicScriptMutation = 11,
        DynamicAnimationInput = 12,
        WorldSpaceEmitterHistory = 13,
        MovementHistoryRequired = 14,
        CameraDependentGeometry = 15,
        TransparentOrderUnrecoverable = 16,
        PayloadTooSmall = 100,
        PayloadMagicMismatch = 101,
        PayloadSchemaMismatch = 102,
        PayloadLengthMismatch = 103,
        PayloadCrcMismatch = 104,
        PayloadSectionTableInvalid = 105,
        PayloadSectionOutOfRange = 106,
        PayloadSectionMisaligned = 107,
        RuntimeGpuFailure = 200,
        StaleBakeFingerprint = 201,
    }

    public readonly struct GpuParticleFailure
    {
        public static readonly GpuParticleFailure None =
            new GpuParticleFailure(GpuParticleFailureCode.None, string.Empty, string.Empty);

        public GpuParticleFailure(GpuParticleFailureCode code, string message, string context = "")
        {
            Code = code;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
        }

        public GpuParticleFailureCode Code { get; }
        public string Message { get; }
        public string Context { get; }
        public bool IsFailure => Code != GpuParticleFailureCode.None;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Context)
                ? $"{Code}: {Message}"
                : $"{Code}: {Message} ({Context})";
        }
    }

    public struct GpuParticleBlobParticleState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Size;
        public Vector4 Rotation;
        public Color32 Color;
        public float Lifetime;
        public uint Seed;
    }

    public struct GpuParticleBlobTrailState
    {
        public Vector3 Position;
        public float Width;
        public Color32 Color;
        public uint ParticleId;
    }

    public struct GpuParticleBlobMeshTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Color32 Color;
    }

    public enum GpuParticleStartResult : byte
    {
        GpuStarted = 0,
        NativeRequired = 1,
    }

    [Serializable]
    public sealed class GpuParticleRendererRecipe
    {
        [SerializeField] private string transformPath = string.Empty;
        [SerializeField] private int layer;
        [SerializeField] private int sortingLayerId;
        [SerializeField] private int sortingOrder;
        [SerializeField] private int rendererPriority;
        [SerializeField] private int renderQueue = 3000;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool receiveShadows;

        public string TransformPath => transformPath;
        public int Layer => layer;
        public int SortingLayerId => sortingLayerId;
        public int SortingOrder => sortingOrder;
        public int RendererPriority => rendererPriority;
        public int RenderQueue => renderQueue;
        public ShadowCastingMode ShadowCastingMode => shadowCastingMode;
        public bool ReceiveShadows => receiveShadows;

        public void Configure(
            string path,
            int sourceLayer,
            int layerId,
            int order,
            int priority,
            int queue,
            ShadowCastingMode shadows,
            bool receives)
        {
            transformPath = path ?? string.Empty;
            layer = sourceLayer;
            sortingLayerId = layerId;
            sortingOrder = order;
            rendererPriority = priority;
            renderQueue = queue;
            shadowCastingMode = shadows;
            receiveShadows = receives;
        }
    }

    [Serializable]
    public sealed class GpuParticleMaterialRecipe
    {
        [SerializeField] private Material material = null!;
        [SerializeField] private int subMeshIndex;
        [SerializeField] private bool instancingAllowed;

        public Material Material => material;
        public int SubMeshIndex => subMeshIndex;
        public bool InstancingAllowed => instancingAllowed;

        public void Configure(Material? sourceMaterial, int sourceSubMeshIndex, bool allowInstancing)
        {
            material = sourceMaterial == null ? null! : sourceMaterial;
            subMeshIndex = sourceSubMeshIndex;
            instancingAllowed = allowInstancing;
        }
    }

    [Serializable]
    public sealed class GpuParticleGeometryFrame
    {
        [SerializeField] private float time;
        [SerializeField] private int particleCount;
        [SerializeField] private int particleStateOffset;
        [SerializeField] private int meshTransformCount;
        [SerializeField] private int meshTransformOffset;
        [SerializeField] private int trailCount;
        [SerializeField] private int trailStateOffset;
        [SerializeField] private Bounds frameLocalBounds;

        public float Time => time;
        public int ParticleCount => particleCount;
        public int ParticleStateOffset => particleStateOffset;
        public int MeshTransformCount => meshTransformCount;
        public int MeshTransformOffset => meshTransformOffset;
        public int TrailCount => trailCount;
        public int TrailStateOffset => trailStateOffset;
        public Bounds FrameLocalBounds => frameLocalBounds;

        public void Configure(
            float frameTime,
            int particleCountValue,
            int particleStateOffsetValue,
            int meshTransformCountValue,
            int meshTransformOffsetValue,
            int trailCountValue,
            int trailStateOffsetValue,
            Bounds bounds)
        {
            time = frameTime;
            particleCount = particleCountValue;
            particleStateOffset = particleStateOffsetValue;
            meshTransformCount = meshTransformCountValue;
            meshTransformOffset = meshTransformOffsetValue;
            trailCount = trailCountValue;
            trailStateOffset = trailStateOffsetValue;
            frameLocalBounds = bounds;
        }
    }

    [Serializable]
    public sealed class GpuParticleGeometryTrack
    {
        [SerializeField] private string transformPath = string.Empty;
        [SerializeField] private GpuParticleRenderMode renderMode = GpuParticleRenderMode.Billboard;
        [SerializeField] private GpuParticleAlignment alignment = GpuParticleAlignment.View;
        [SerializeField] private GpuParticleRendererRecipe rendererRecipe = new GpuParticleRendererRecipe();
        [SerializeField] private GpuParticleMaterialRecipe[] materialRecipes = Array.Empty<GpuParticleMaterialRecipe>();
        [SerializeField] private GpuParticleMaterialRecipe[] trailMaterialRecipes = Array.Empty<GpuParticleMaterialRecipe>();
        [SerializeField] private Mesh sharedMesh = null!;
        [SerializeField] private GpuParticleGeometryFrame[] frames = Array.Empty<GpuParticleGeometryFrame>();
        [SerializeField] private Bounds localBounds;

        public string TransformPath => transformPath;
        public GpuParticleRenderMode RenderMode => renderMode;
        public GpuParticleAlignment Alignment => alignment;
        public GpuParticleRendererRecipe RendererRecipe => rendererRecipe;
        public GpuParticleMaterialRecipe[] MaterialRecipes => materialRecipes;
        public GpuParticleMaterialRecipe[] TrailMaterialRecipes => trailMaterialRecipes;
        public Mesh SharedMesh => sharedMesh;
        public GpuParticleGeometryFrame[] Frames => frames;
        public Bounds LocalBounds => localBounds;

        public int FindFrameIndex(float clipTime)
        {
            if (frames.Length == 0)
            {
                return -1;
            }

            int low = 0;
            int high = frames.Length - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                if (frames[mid].Time <= clipTime)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Mathf.Clamp(high, 0, frames.Length - 1);
        }

        public void Configure(
            string path,
            GpuParticleRenderMode mode,
            GpuParticleAlignment alignmentValue,
            GpuParticleRendererRecipe recipe,
            GpuParticleMaterialRecipe[] materials,
            GpuParticleMaterialRecipe[] trailMaterials,
            Mesh mesh,
            GpuParticleGeometryFrame[] geometryFrames,
            Bounds bounds)
        {
            transformPath = path ?? string.Empty;
            renderMode = mode;
            alignment = alignmentValue;
            rendererRecipe = recipe ?? new GpuParticleRendererRecipe();
            materialRecipes = materials ?? Array.Empty<GpuParticleMaterialRecipe>();
            trailMaterialRecipes = trailMaterials ?? Array.Empty<GpuParticleMaterialRecipe>();
            sharedMesh = mesh == null ? null! : mesh;
            frames = geometryFrames ?? Array.Empty<GpuParticleGeometryFrame>();
            localBounds = bounds;
        }
    }

    [Serializable]
    public sealed class GpuParticleNativeSystemState
    {
        [SerializeField] private ParticleSystem system = null!;
        [SerializeField] private bool gameObjectActive;
        [SerializeField] private bool playOnAwake;
        [SerializeField] private bool useAutoRandomSeed;
        [SerializeField] private uint randomSeed;
        [SerializeField] private float simulationSpeed = 1f;

        public ParticleSystem System => system;
        public bool GameObjectActive => gameObjectActive;
        public bool PlayOnAwake => playOnAwake;
        public bool UseAutoRandomSeed => useAutoRandomSeed;
        public uint RandomSeed => randomSeed;
        public float SimulationSpeed => simulationSpeed;

        public void Capture(ParticleSystem source)
        {
            system = source;
            gameObjectActive = source != null && source.gameObject.activeSelf;
            if (source == null)
            {
                return;
            }

            ParticleSystem.MainModule main = source.main;
            playOnAwake = main.playOnAwake;
            simulationSpeed = main.simulationSpeed;
            useAutoRandomSeed = source.useAutoRandomSeed;
            randomSeed = source.randomSeed;
        }
    }

    [Serializable]
    public sealed class GpuParticleNativeRendererState
    {
        [SerializeField] private ParticleSystemRenderer renderer = null!;
        [SerializeField] private bool gameObjectActive;
        [SerializeField] private bool enabled;

        public ParticleSystemRenderer Renderer => renderer;
        public bool GameObjectActive => gameObjectActive;
        public bool Enabled => enabled;

        public void Capture(ParticleSystemRenderer source)
        {
            renderer = source;
            gameObjectActive = source != null && source.gameObject.activeSelf;
            enabled = source != null && source.enabled;
        }
    }
}
