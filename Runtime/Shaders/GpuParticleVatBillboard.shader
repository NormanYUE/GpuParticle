Shader "GpuParticle/VatBillboard"
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
            #pragma multi_compile_local _ RENDERMODE_HORIZONTAL RENDERMODE_VERTICAL

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

                float2 quadUv = v.uv0;
                float3 axisX;
                float3 axisY;

#if defined(RENDERMODE_HORIZONTAL)
                axisX = float3(1, 0, 0);
                axisY = float3(0, 0, 1);
#elif defined(RENDERMODE_VERTICAL)
                float3 worldUp = float3(0, 1, 0);
                float3 toCamera = normalize(_WorldSpaceCameraPos.xyz - s.worldPosition);
                float3 forwardHorizontal = normalize(toCamera - dot(toCamera, worldUp) * worldUp);
                axisX = normalize(cross(worldUp, forwardHorizontal));
                axisY = worldUp;
#else
                axisX = normalize(UNITY_MATRIX_I_V._11_21_31);
                axisY = normalize(UNITY_MATRIX_I_V._12_22_32);
#endif

                float3 corner = s.worldPosition
                    + axisX * (quadUv.x - 0.5) * s.size
                    + axisY * (quadUv.y - 0.5) * s.size;

                v2f o;
                o.positionCS = TransformWorldToHClip(corner);
                o.color = s.color;
                o.uv = quadUv;
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
