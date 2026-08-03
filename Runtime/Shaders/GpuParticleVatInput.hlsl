#ifndef GPU_PARTICLE_VAT_INPUT_INCLUDED
#define GPU_PARTICLE_VAT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_PositionSizeTex);
SAMPLER(sampler_PositionSizeTex);
TEXTURE2D(_ColorTex);
SAMPLER(sampler_ColorTex);
TEXTURE2D(_RotationTex);
SAMPLER(sampler_RotationTex);
TEXTURE2D(_VelocityLifetimeTex);
SAMPLER(sampler_VelocityLifetimeTex);
TEXTURE2D(_SheetFrameTex);
SAMPLER(sampler_SheetFrameTex);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
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

    float4 posSizeA = SAMPLE_TEXTURE2D_LOD(_PositionSizeTex, sampler_PositionSizeTex, uvA, 0);
    float4 posSizeB = SAMPLE_TEXTURE2D_LOD(_PositionSizeTex, sampler_PositionSizeTex, uvB, 0);
    float4 posSize = lerp(posSizeA, posSizeB, t);

    float4 colorA = SAMPLE_TEXTURE2D_LOD(_ColorTex, sampler_ColorTex, uvA, 0);
    float4 colorB = SAMPLE_TEXTURE2D_LOD(_ColorTex, sampler_ColorTex, uvB, 0);
    float4 color = lerp(colorA, colorB, t);

    float4 rotA = SAMPLE_TEXTURE2D_LOD(_RotationTex, sampler_RotationTex, uvA, 0);
    float4 rotB = SAMPLE_TEXTURE2D_LOD(_RotationTex, sampler_RotationTex, uvB, 0);
    float4 rot = normalize(lerp(rotA, rotB, t));

    float4 velLifeA = SAMPLE_TEXTURE2D_LOD(_VelocityLifetimeTex, sampler_VelocityLifetimeTex, uvA, 0);
    float4 velLifeB = SAMPLE_TEXTURE2D_LOD(_VelocityLifetimeTex, sampler_VelocityLifetimeTex, uvB, 0);
    float4 velLife = lerp(velLifeA, velLifeB, t);

    float sheetFrameA = SAMPLE_TEXTURE2D_LOD(_SheetFrameTex, sampler_SheetFrameTex, uvA, 0).r;
    float sheetFrameB = SAMPLE_TEXTURE2D_LOD(_SheetFrameTex, sampler_SheetFrameTex, uvB, 0).r;
    float sheetFrame = round(lerp(sheetFrameA, sheetFrameB, t));

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
