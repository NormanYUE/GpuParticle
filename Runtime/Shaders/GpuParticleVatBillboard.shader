Shader "GpuParticle/VatBillboard"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _PositionSizeTex("Position + Size", 2D) = "white" {}
        _ColorTex("Color", 2D) = "white" {}
        _SheetFrameTex("Sheet Frame", 2D) = "white" {}
        _SheetTiles("Sheet Tiles", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GpuParticleVATBillboard"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ ALIGNMENT_VIEW ALIGNMENT_FACING ALIGNMENT_WORLD ALIGNMENT_LOCAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PositionSizeTex);
            SAMPLER(sampler_PositionSizeTex);
            TEXTURE2D(_ColorTex);
            SAMPLER(sampler_ColorTex);
            TEXTURE2D(_SheetFrameTex);
            SAMPLER(sampler_SheetFrameTex);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Duration;
                float _FrameCount;
                float4 _TexelSize;
                float4 _SheetTiles;
            CBUFFER_END

            struct InstanceData
            {
                float4x4 localToWorld;
                float elapsedTime;
                float timeScale;
                uint seedVariant;
            };

            StructuredBuffer<InstanceData> _InstanceDataBuffer;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float sheetFrame : TEXCOORD1;
            };

            float2 ParticleUv(uint particleIndex, uint frameIndex)
            {
                float u = (particleIndex + 0.5) * _TexelSize.x;
                float v = (frameIndex + 0.5) * _TexelSize.y;
                return float2(u, v);
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                InstanceData inst = _InstanceDataBuffer[instanceID];
                uint particleIndex = (uint)(v.uv1.x + 0.5);

                float nt = inst.elapsedTime / max(_Duration, 0.0001);
                float frameF = nt * (_FrameCount - 1);
                uint frameA = (uint)frameF;
                uint frameB = min(frameA + 1, (uint)_FrameCount - 1);
                float t = frameF - (float)frameA;

                float2 uvA = ParticleUv(particleIndex, frameA);
                float2 uvB = ParticleUv(particleIndex, frameB);

                float4 posSizeA = SAMPLE_TEXTURE2D_LOD(_PositionSizeTex, sampler_PositionSizeTex, uvA, 0);
                float4 posSizeB = SAMPLE_TEXTURE2D_LOD(_PositionSizeTex, sampler_PositionSizeTex, uvB, 0);
                float4 posSize = lerp(posSizeA, posSizeB, t);

                float4 colorA = SAMPLE_TEXTURE2D_LOD(_ColorTex, sampler_ColorTex, uvA, 0);
                float4 colorB = SAMPLE_TEXTURE2D_LOD(_ColorTex, sampler_ColorTex, uvB, 0);
                float4 color = lerp(colorA, colorB, t);

                float sheetFrameA = SAMPLE_TEXTURE2D_LOD(_SheetFrameTex, sampler_SheetFrameTex, uvA, 0).r;
                float sheetFrameB = SAMPLE_TEXTURE2D_LOD(_SheetFrameTex, sampler_SheetFrameTex, uvB, 0).r;
                float sheetFrame = round(lerp(sheetFrameA, sheetFrameB, t));

                float3 center = mul(inst.localToWorld, float4(posSize.xyz, 1)).xyz;
                float size = posSize.w;

                // World-space camera basis (URP)
                float3 viewRight = normalize(UNITY_MATRIX_I_V._11_21_31);
                float3 viewUp = normalize(UNITY_MATRIX_I_V._12_22_32);

                float2 quadUv = v.uv0;
                float3 corner = center
                    + viewRight * (quadUv.x - 0.5) * size
                    + viewUp * (quadUv.y - 0.5) * size;

                v2f o;
                o.positionCS = TransformWorldToHClip(corner);
                o.color = color;
                o.uv = quadUv;
                o.sheetFrame = sheetFrame;
                return o;
            }

            float2 ApplyTextureSheet(float2 quadUv, float sheetFrame)
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

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = ApplyTextureSheet(i.uv, i.sheetFrame);
                if (_SheetTiles.z > 0.0)
                {
                    float frameNorm = i.sheetFrame / _SheetTiles.z;
                    return half4(frameNorm, frameNorm, frameNorm, 1.0);
                }

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
