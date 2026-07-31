using UnityEngine;

namespace GpuParticle.Runtime
{
    // Retained for prefab compatibility and editor preview.
    // Runtime playback is now handled by GpuParticleVatRenderSystem for instanced batching.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [System.Obsolete("Use GpuParticleVatRenderSystem for runtime playback. This component is kept for prefab compatibility.")]
    public sealed class GpuParticleVatRenderer : MonoBehaviour
    {
        private static readonly int ElapsedTimeId = Shader.PropertyToID("_ElapsedTime");
        private static readonly int LocalToWorldId = Shader.PropertyToID("_LocalToWorld");
        private static readonly int PositionSizeTexId = Shader.PropertyToID("_PositionSizeTex");
        private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");
        private static readonly int RotationTexId = Shader.PropertyToID("_RotationTex");
        private static readonly int VelocityLifetimeTexId = Shader.PropertyToID("_VelocityLifetimeTex");
        private static readonly int TexelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int DurationId = Shader.PropertyToID("_Duration");
        private static readonly int FrameCountId = Shader.PropertyToID("_FrameCount");

        [SerializeField] private GpuParticleClip clip = null!;
        [SerializeField] private bool loop = true;
        [SerializeField] private float timeScale = 1f;

        private float elapsed;
        private MeshRenderer meshRenderer = null!;
        private MaterialPropertyBlock mpb = null!;
        private bool texturesBound;

        public GpuParticleClip Clip => clip;
        public bool Loop { get => loop; set => loop = value; }
        public float TimeScale { get => timeScale; set => timeScale = value; }
        public float Elapsed => elapsed;

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            mpb = new MaterialPropertyBlock();
            BindTextures();
        }

        private void OnEnable()
        {
            elapsed = 0f;
        }

        private void Update()
        {
            if (clip == null || meshRenderer == null)
            {
                return;
            }

            if (!texturesBound)
            {
                BindTextures();
            }

            elapsed += Time.deltaTime * timeScale;
            if (loop && clip.Duration > 0f)
            {
                elapsed %= clip.Duration;
            }

            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ElapsedTimeId, elapsed);
            mpb.SetMatrix(LocalToWorldId, transform.localToWorldMatrix);
            meshRenderer.SetPropertyBlock(mpb);
        }

        private void BindTextures()
        {
            if (clip == null || meshRenderer == null)
            {
                return;
            }

            meshRenderer.GetPropertyBlock(mpb);

            if (clip.PositionSizeTexture != null)
            {
                mpb.SetTexture(PositionSizeTexId, clip.PositionSizeTexture);
            }

            if (clip.ColorTexture != null)
            {
                mpb.SetTexture(ColorTexId, clip.ColorTexture);
            }

            if (clip.RotationTexture != null)
            {
                mpb.SetTexture(RotationTexId, clip.RotationTexture);
            }

            if (clip.VelocityLifetimeTexture != null)
            {
                mpb.SetTexture(VelocityLifetimeTexId, clip.VelocityLifetimeTexture);
            }

            if (clip.PositionSizeTexture != null)
            {
                Vector2 texelSize = new Vector2(
                    1f / Mathf.Max(1, clip.PositionSizeTexture.width),
                    1f / Mathf.Max(1, clip.PositionSizeTexture.height));
                mpb.SetVector(TexelSizeId, new Vector4(texelSize.x, texelSize.y, clip.PositionSizeTexture.width, clip.PositionSizeTexture.height));
            }

            mpb.SetFloat(DurationId, clip.Duration);
            mpb.SetFloat(FrameCountId, clip.FrameCount);
            meshRenderer.SetPropertyBlock(mpb);
            texturesBound = true;
        }

        public void Stop() => enabled = false;
        public void Play() => enabled = true;
        public void SetTime(float time) => elapsed = time;
    }
}
