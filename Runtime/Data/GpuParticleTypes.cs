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

    internal enum GpuParticleTrackMode : byte
    {
        State = 0,
        Geometry = 1,
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
        [SerializeField] private bool cameraConstrained;
        [SerializeField] private Vector3 referenceCameraPosition;
        [SerializeField] private Quaternion referenceCameraRotation = Quaternion.identity;
        [SerializeField] private float referenceCameraFieldOfView;
        [SerializeField] private float referenceCameraAspect;
        [SerializeField] private float cameraPositionTolerance = 0.05f;
        [SerializeField] private float cameraRotationTolerance = 0.5f;
        [SerializeField] private float cameraFieldOfViewTolerance = 0.1f;
        [SerializeField] private float cameraAspectTolerance = 0.01f;

        public string TransformPath => transformPath;
        public int Layer => layer;
        public int SortingLayerId => sortingLayerId;
        public int SortingOrder => sortingOrder;
        public int RendererPriority => rendererPriority;
        public int RenderQueue => renderQueue;
        public ShadowCastingMode ShadowCastingMode => shadowCastingMode;
        public bool ReceiveShadows => receiveShadows;
        public bool CameraConstrained => cameraConstrained;

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

        public void SetCameraConstraint(
            Camera camera,
            float positionTolerance = 0.05f,
            float rotationTolerance = 0.5f,
            float fieldOfViewTolerance = 0.1f,
            float aspectTolerance = 0.01f)
        {
            if (camera == null)
            {
                cameraConstrained = false;
                return;
            }

            cameraConstrained = true;
            referenceCameraPosition = camera.transform.position;
            referenceCameraRotation = camera.transform.rotation;
            referenceCameraFieldOfView = camera.fieldOfView;
            referenceCameraAspect = camera.aspect;
            cameraPositionTolerance = Mathf.Max(0f, positionTolerance);
            cameraRotationTolerance = Mathf.Max(0f, rotationTolerance);
            cameraFieldOfViewTolerance = Mathf.Max(0f, fieldOfViewTolerance);
            cameraAspectTolerance = Mathf.Max(0f, aspectTolerance);
        }

        public bool IsCameraCompatible(Camera camera)
        {
            if (!cameraConstrained)
            {
                return true;
            }

            if (camera == null)
            {
                return false;
            }

            float positionDelta = Vector3.Distance(referenceCameraPosition, camera.transform.position);
            float rotationDelta = Quaternion.Angle(referenceCameraRotation, camera.transform.rotation);
            float fovDelta = Mathf.Abs(referenceCameraFieldOfView - camera.fieldOfView);
            float aspectDelta = Mathf.Abs(referenceCameraAspect - camera.aspect);
            return positionDelta <= cameraPositionTolerance &&
                   rotationDelta <= cameraRotationTolerance &&
                   fovDelta <= cameraFieldOfViewTolerance &&
                   aspectDelta <= cameraAspectTolerance;
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
        [SerializeField] private Mesh mesh = null!;
        [SerializeField] private Mesh trailMesh = null!;
        [SerializeField] private Bounds bounds;

        public float Time => time;
        public Mesh Mesh => mesh;
        public Mesh TrailMesh => trailMesh;
        public Bounds Bounds => bounds;

        public bool HasVisibleMesh =>
            (mesh != null && mesh.vertexCount > 0) ||
            (trailMesh != null && trailMesh.vertexCount > 0);

        public void Configure(float frameTime, Mesh? particleMesh, Mesh? particleTrailMesh, Bounds frameBounds)
        {
            time = frameTime;
            mesh = particleMesh == null ? null! : particleMesh;
            trailMesh = particleTrailMesh == null ? null! : particleTrailMesh;
            bounds = frameBounds;
        }
    }

    [Serializable]
    public sealed class GpuParticleGeometryTrack
    {
        [SerializeField] private string transformPath = string.Empty;
        [SerializeField] private GpuParticleRendererRecipe rendererRecipe = new GpuParticleRendererRecipe();
        [SerializeField] private GpuParticleMaterialRecipe[] materialRecipes = Array.Empty<GpuParticleMaterialRecipe>();
        [SerializeField] private GpuParticleMaterialRecipe[] trailMaterialRecipes = Array.Empty<GpuParticleMaterialRecipe>();
        [SerializeField] private GpuParticleGeometryFrame[] frames = Array.Empty<GpuParticleGeometryFrame>();

        public string TransformPath => transformPath;
        public GpuParticleRendererRecipe RendererRecipe => rendererRecipe;
        public GpuParticleMaterialRecipe[] MaterialRecipes => materialRecipes;
        public GpuParticleMaterialRecipe[] TrailMaterialRecipes => trailMaterialRecipes;
        public GpuParticleGeometryFrame[] Frames => frames;

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
            GpuParticleRendererRecipe recipe,
            GpuParticleMaterialRecipe[] materials,
            GpuParticleMaterialRecipe[] trailMaterials,
            GpuParticleGeometryFrame[] geometryFrames)
        {
            transformPath = path ?? string.Empty;
            rendererRecipe = recipe ?? new GpuParticleRendererRecipe();
            materialRecipes = materials ?? Array.Empty<GpuParticleMaterialRecipe>();
            trailMaterialRecipes = trailMaterials ?? Array.Empty<GpuParticleMaterialRecipe>();
            frames = geometryFrames ?? Array.Empty<GpuParticleGeometryFrame>();
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
