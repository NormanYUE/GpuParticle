#ifndef GPU_PARTICLE_VAT_INPUT_INCLUDED
#define GPU_PARTICLE_VAT_INPUT_INCLUDED

// Generated CGPROGRAM variants define GPU_PARTICLE_VAT_UNITYCG before including this
// file and supply UnityCG.cginc themselves; the default flavor is URP/HLSL.
#if !defined(GPU_PARTICLE_VAT_UNITYCG)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#endif

#if defined(GPU_PARTICLE_VAT_UNITYCG)
#define GPU_VAT_DECLARE_TEX(name, samp) sampler2D name;
#define GPU_VAT_SAMPLE_TEX(name, samp, uv) tex2Dlod(name, float4(uv, 0.0, 0.0))
#else
#define GPU_VAT_DECLARE_TEX(name, samp) TEXTURE2D(name); SAMPLER(samp);
#define GPU_VAT_SAMPLE_TEX(name, samp, uv) SAMPLE_TEXTURE2D_LOD(name, samp, uv, 0)
#endif

GPU_VAT_DECLARE_TEX(_PositionSizeTex, sampler_PositionSizeTex)
GPU_VAT_DECLARE_TEX(_ColorTex, sampler_ColorTex)
GPU_VAT_DECLARE_TEX(_RotationTex, sampler_RotationTex)
GPU_VAT_DECLARE_TEX(_VelocityLifetimeTex, sampler_VelocityLifetimeTex)
GPU_VAT_DECLARE_TEX(_SheetFrameTex, sampler_SheetFrameTex)
#if defined(GPU_PARTICLE_CUSTOM_DATA)
GPU_VAT_DECLARE_TEX(_CustomData1Tex, sampler_CustomData1Tex)
GPU_VAT_DECLARE_TEX(_CustomData2Tex, sampler_CustomData2Tex)
#endif
// _MainTex is deliberately NOT declared here: it belongs to the user shader wrapped into
// the variant, so the user declaration must survive to keep tex2D/SAMPLE_TEXTURE2D calls
// type-consistent. Built-in VAT shaders declare it themselves.

// Named GpuParticleVat instead of UnityPerMaterial: user shaders (and the generated
// variants wrapping them) keep their own UnityPerMaterial cbuffer, and HLSLcc rejects
// duplicate cbuffer declarations on Metal/GLES. Property binding is by name either way.
CBUFFER_START(GpuParticleVat)
    float _Duration;
    float _FrameCount;
    float4 _TexelSize;
    float4 _SheetTiles;
    float _LengthScale;
    float _VelocityScale;
CBUFFER_END

struct InstanceData
{
    float4x4 localToWorld;
    float elapsedTime;
    float timeScale;
    uint seedVariant;
};

StructuredBuffer<InstanceData> _InstanceDataBuffer;

struct GpuParticleVatSample
{
    float3 localPosition;
    float3 worldPosition;
    float size;
    float4 color;
    float4 rotation;
    float3 localVelocity;
    float3 worldVelocity;
    float lifetime;
    float sheetFrame;
    float4 custom1;
    float4 custom2;
    float4x4 localToWorld;
};

float2 GpuParticleParticleUv(uint particleIndex, uint frameIndex)
{
    float u = (particleIndex + 0.5) * _TexelSize.x;
    float v = (frameIndex + 0.5) * _TexelSize.y;
    return float2(u, v);
}

GpuParticleVatSample GpuParticleSampleVat(uint instanceID, float particleIndexPacked)
{
    InstanceData inst = _InstanceDataBuffer[instanceID];
    uint particleIndex = (uint)(particleIndexPacked + 0.5);

    float nt = inst.elapsedTime / max(_Duration, 0.0001);
    float frameF = nt * (_FrameCount - 1);
    uint frameA = (uint)frameF;
    uint frameB = min(frameA + 1, (uint)_FrameCount - 1);
    float t = frameF - (float)frameA;

    float2 uvA = GpuParticleParticleUv(particleIndex, frameA);
    float2 uvB = GpuParticleParticleUv(particleIndex, frameB);

    float4 posSizeA = GPU_VAT_SAMPLE_TEX(_PositionSizeTex, sampler_PositionSizeTex, uvA);
    float4 posSizeB = GPU_VAT_SAMPLE_TEX(_PositionSizeTex, sampler_PositionSizeTex, uvB);
    float4 posSize = lerp(posSizeA, posSizeB, t);

    float4 colorA = GPU_VAT_SAMPLE_TEX(_ColorTex, sampler_ColorTex, uvA);
    float4 colorB = GPU_VAT_SAMPLE_TEX(_ColorTex, sampler_ColorTex, uvB);
    float4 color = lerp(colorA, colorB, t);

    float4 rotA = GPU_VAT_SAMPLE_TEX(_RotationTex, sampler_RotationTex, uvA);
    float4 rotB = GPU_VAT_SAMPLE_TEX(_RotationTex, sampler_RotationTex, uvB);
    float4 rot = normalize(lerp(rotA, rotB, t));

    float4 velLifeA = GPU_VAT_SAMPLE_TEX(_VelocityLifetimeTex, sampler_VelocityLifetimeTex, uvA);
    float4 velLifeB = GPU_VAT_SAMPLE_TEX(_VelocityLifetimeTex, sampler_VelocityLifetimeTex, uvB);
    float4 velLife = lerp(velLifeA, velLifeB, t);

    float sheetFrameA = GPU_VAT_SAMPLE_TEX(_SheetFrameTex, sampler_SheetFrameTex, uvA).r;
    float sheetFrameB = GPU_VAT_SAMPLE_TEX(_SheetFrameTex, sampler_SheetFrameTex, uvB).r;
    // Baked frames store integer tile indices; floor (not round) keeps the tile stable
    // until the next sampled frame, matching native tile selection.
    float sheetFrame = floor(lerp(sheetFrameA, sheetFrameB, t));

#if defined(GPU_PARTICLE_CUSTOM_DATA)
    float4 custom1A = GPU_VAT_SAMPLE_TEX(_CustomData1Tex, sampler_CustomData1Tex, uvA);
    float4 custom1B = GPU_VAT_SAMPLE_TEX(_CustomData1Tex, sampler_CustomData1Tex, uvB);
    float4 custom1 = lerp(custom1A, custom1B, t);

    float4 custom2A = GPU_VAT_SAMPLE_TEX(_CustomData2Tex, sampler_CustomData2Tex, uvA);
    float4 custom2B = GPU_VAT_SAMPLE_TEX(_CustomData2Tex, sampler_CustomData2Tex, uvB);
    float4 custom2 = lerp(custom2A, custom2B, t);
#endif

    GpuParticleVatSample s;
    s.localPosition = posSize.xyz;
    s.worldPosition = mul(inst.localToWorld, float4(posSize.xyz, 1)).xyz;
    s.size = posSize.w;
    s.color = color;
    s.rotation = rot;
    s.localVelocity = velLife.xyz;
    s.worldVelocity = mul(inst.localToWorld, float4(velLife.xyz, 0)).xyz;
    s.lifetime = velLife.w;
    s.sheetFrame = sheetFrame;
#if defined(GPU_PARTICLE_CUSTOM_DATA)
    s.custom1 = custom1;
    s.custom2 = custom2;
#else
    s.custom1 = 0;
    s.custom2 = 0;
#endif
    s.localToWorld = inst.localToWorld;
    return s;
}

float3 GpuParticleRotateVector(float3 v, float4 q)
{
    float3 t = 2.0 * cross(q.xyz, v);
    return v + q.w * t + cross(q.xyz, t);
}

float2 GpuParticleApplyTextureSheet(float2 quadUv, float sheetFrame)
{
    if (_SheetTiles.z <= 0.0)
    {
        return quadUv;
    }

    float2 tileCount = _SheetTiles.xy;
    float2 tileSize = 1.0 / tileCount;
    float2 tileOffset = float2(fmod(sheetFrame, tileCount.x), floor(sheetFrame / tileCount.x)) * tileSize;
    return quadUv * tileSize + tileOffset;
}

#endif
