Shader "GpuParticle/VatMesh"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _PositionSizeTex("Position + Size", 2D) = "white" {}
        _ColorTex("Color", 2D) = "white" {}
        _RotationTex("Rotation", 2D) = "white" {}
        _VelocityLifetimeTex("Velocity + Lifetime", 2D) = "white" {}
        _SheetFrameTex("Sheet Frame", 2D) = "white" {}
        _SheetTiles("Sheet Tiles", Vector) = (0, 0, 0, 0)
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GpuParticleVatInput.hlsl"

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

                float3 localPos = GpuParticleRotateVector(v.vertex.xyz * s.size, s.rotation) + s.localPosition;
                float3 worldPos = mul(s.localToWorld, float4(localPos, 1)).xyz;

                v2f o;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.color = s.color;
                o.uv = v.uv0;
                o.sheetFrame = s.sheetFrame;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = GpuParticleApplyTextureSheet(i.uv, i.sheetFrame);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
