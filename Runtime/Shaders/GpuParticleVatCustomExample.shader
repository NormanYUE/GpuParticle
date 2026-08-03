// Example: a custom GPU-particle shader that uses the shared VAT input include.
// Copy this file into your project, rename it, and replace the fragment logic
// with your own special shader effects while keeping the vertex VAT sampling.
Shader "GpuParticle/CustomExample"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _EdgeColor("Edge Color", Color) = (1, 1, 1, 1)
        _DissolveThreshold("Dissolve Threshold", Range(0, 1)) = 0.5
        _EdgeWidth("Edge Width", Range(0, 1)) = 0.1
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
            Name "GpuParticleCustomExample"

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GpuParticleVatInput.hlsl"

            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float _DissolveThreshold;
                float _EdgeWidth;
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
                float sheetFrame : TEXCOORD1;
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                GpuParticleVatSample s = GpuParticleSampleVat(instanceID, v.uv1.x);

                // Simple view-facing billboard using VAT position and size.
                float3 viewRight = normalize(UNITY_MATRIX_I_V._11_21_31);
                float3 viewUp = normalize(UNITY_MATRIX_I_V._12_22_32);

                float2 quadUv = v.uv0;
                float3 corner = s.worldPosition
                    + viewRight * (quadUv.x - 0.5) * s.size
                    + viewUp * (quadUv.y - 0.5) * s.size;

                v2f o;
                o.positionCS = TransformWorldToHClip(corner);
                o.color = s.color;
                o.uv = quadUv;
                o.sheetFrame = s.sheetFrame;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Apply texture sheet animation before custom sampling.
                float2 uv = GpuParticleApplyTextureSheet(i.uv, i.sheetFrame);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half dissolve = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, uv).r;

                half edge = smoothstep(_DissolveThreshold - _EdgeWidth, _DissolveThreshold, dissolve);
                half alpha = step(_DissolveThreshold, dissolve);

                half4 col = tex * i.color;
                col.rgb += _EdgeColor.rgb * (1.0 - edge) * _EdgeColor.a;
                col.a *= alpha;

                return col;
            }
            ENDHLSL
        }
    }
}
