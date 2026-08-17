Shader "GpuParticle/VatStretch"
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
        _LengthScale("Length Scale", Float) = 0
        _VelocityScale("Velocity Scale", Float) = 0
        _ParticlePivot("Particle Pivot", Vector) = (0, 0, 0, 0)
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
            Name "GpuParticleVATStretch"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ ALIGNMENT_VIEW ALIGNMENT_FACING ALIGNMENT_WORLD ALIGNMENT_LOCAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GpuParticleVatInput.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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

                float instanceScale = GpuParticleInstanceScale(s.localToWorld);
                float size = s.size * instanceScale;
                float speed = length(s.worldVelocity);
                float3 stretchDir = normalize(s.worldVelocity + 0.0001);
                // Match Unity: stretchLen = size * lengthScale + speed * velocityScale.
                // Size already carries instanceScale so Hierarchy/Local scalingMode is honored;
                // speed is world-space so it is already scaled by the instance basis.
                float stretchLen = size * _LengthScale + speed * _VelocityScale;

                // Face the camera along the width axis while stretching along velocity.
                float3 viewDir = normalize(UNITY_MATRIX_I_V._31_32_33);
                float3 right = cross(viewDir, stretchDir);
                float rightLen = length(right);
                if (rightLen < 0.0001)
                {
                    right = normalize(UNITY_MATRIX_I_V._11_21_31);
                }
                else
                {
                    right /= rightLen;
                }

                float2 quadUv = v.uv0;
                float2 pivotUv = GpuParticlePivotOffset(quadUv);
                float3 corner = s.worldPosition
                    + stretchDir * pivotUv.y * stretchLen
                    + right * pivotUv.x * size;

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
