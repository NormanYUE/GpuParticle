using UnityEngine;

namespace GpuParticle.Runtime
{
    public sealed class GpuParticleRuntimeResources : ScriptableObject
    {
        [SerializeField] private Shader billboardShader = null!;
        [SerializeField] private Shader meshShader = null!;
        [SerializeField] private Shader stretchShader = null!;
        [SerializeField] private Shader trailShader = null!;
        [SerializeField] private ShaderVariantCollection shaderVariants = null!;

        public Shader BillboardShader => billboardShader;
        public Shader MeshShader => meshShader;
        public Shader StretchShader => stretchShader;
        public Shader TrailShader => trailShader;
        public ShaderVariantCollection ShaderVariants => shaderVariants;

        public bool HasStatePlaybackResources =>
            billboardShader != null && meshShader != null && stretchShader != null && trailShader != null;

        public void Configure(
            Shader billboard,
            Shader mesh,
            Shader stretch,
            Shader trail,
            ShaderVariantCollection variants)
        {
            billboardShader = billboard;
            meshShader = mesh;
            stretchShader = stretch;
            trailShader = trail;
            shaderVariants = variants;
        }
    }
}
