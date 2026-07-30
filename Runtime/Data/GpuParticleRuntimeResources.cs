using UnityEngine;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleRuntimeResources : ScriptableObject
    {
        [SerializeField] private ComputeShader playbackCompute = null!;
        [SerializeField] private Shader billboardShader = null!;
        [SerializeField] private Shader meshShader = null!;
        [SerializeField] private Mesh defaultQuad = null!;
        [SerializeField] private ShaderVariantCollection shaderVariants = null!;

        public ComputeShader PlaybackCompute => playbackCompute;
        public Shader BillboardShader => billboardShader;
        public Shader MeshShader => meshShader;
        public Mesh DefaultQuad => defaultQuad;
        public ShaderVariantCollection ShaderVariants => shaderVariants;

        public bool HasStatePlaybackResources =>
            playbackCompute != null && billboardShader != null && meshShader != null && defaultQuad != null;

        public void Configure(
            ComputeShader compute,
            Shader billboard,
            Shader mesh,
            Mesh quad,
            ShaderVariantCollection variants)
        {
            playbackCompute = compute;
            billboardShader = billboard;
            meshShader = mesh;
            defaultQuad = quad;
            shaderVariants = variants;
        }
    }
}
