Shader "GpuParticle/VatMesh"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _PositionSizeTex("Position + Size", 2D) = "white" {}
        _ColorTex("Color", 2D) = "white" {}
        _RotationTex("Rotation", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GpuParticleVATMesh"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PositionSizeTex);
            SAMPLER(sampler_PositionSizeTex);
            TEXTURE2D(_ColorTex);
            SAMPLER(sampler_ColorTex);
            TEXTURE2D(_RotationTex);
            SAMPLER(sampler_RotationTex);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _ElapsedTime;
                float _Duration;
                float _FrameCount;
                float4 _TexelSize;
                float4x4 _LocalToWorld;
            CBUFFER_END

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
            };

            float2 ParticleUv(uint particleIndex, uint frameIndex)
            {
                float u = (particleIndex + 0.5) * _TexelSize.x;
                float v = (frameIndex + 0.5) * _TexelSize.y;
                return float2(u, v);
            }

            float3 RotateVector(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert(appdata v)
            {
                uint particleIndex = (uint)(v.uv1.x + 0.5);

                float nt = _ElapsedTime / max(_Duration, 0.0001);
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

                float4 rotA = SAMPLE_TEXTURE2D_LOD(_RotationTex, sampler_RotationTex, uvA, 0);
                float4 rotB = SAMPLE_TEXTURE2D_LOD(_RotationTex, sampler_RotationTex, uvB, 0);
                float4 rot = normalize(lerp(rotA, rotB, t));

                float3 localPos = RotateVector(v.vertex.xyz * posSize.w, rot) + posSize.xyz;
                float3 worldPos = mul(_LocalToWorld, float4(localPos, 1)).xyz;

                v2f o;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.color = color;
                o.uv = v.uv0;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
