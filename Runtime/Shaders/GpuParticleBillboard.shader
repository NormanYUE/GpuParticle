Shader "GpuParticle/Billboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ ALIGNMENT_VIEW ALIGNMENT_FACING ALIGNMENT_WORLD ALIGNMENT_LOCAL

            #include "UnityCG.cginc"

            struct ParticleState
            {
                float3 position;
                float3 velocity;
                float size;
                float4 rotation;
                uint color;
                float lifetime;
                uint seed;
            };

            StructuredBuffer<ParticleState> _ParticleStates;
            sampler2D _MainTex;
            float4x4 _LocalToWorld;
            float3 _CameraRight;
            float3 _CameraUp;
            float3 _CameraForward;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float2 QuadUV(uint id)
            {
                uint quadIndex = id / 6;
                uint vertexInQuad = id % 6;
                // 0,1,2,2,3,0
                float2 baseUV = float2(0,0);
                if (vertexInQuad == 1 || vertexInQuad == 4) baseUV = float2(1,0);
                if (vertexInQuad == 2 || vertexInQuad == 3) baseUV = float2(1,1);
                if (vertexInQuad == 5) baseUV = float2(0,1);
                return baseUV;
            }

            float4 UnpackColor32(uint c)
            {
                return float4(
                    ((c >> 0) & 0xFF) / 255.0,
                    ((c >> 8) & 0xFF) / 255.0,
                    ((c >> 16) & 0xFF) / 255.0,
                    ((c >> 24) & 0xFF) / 255.0);
            }

            v2f vert(uint id : SV_VertexID, uint instId : SV_InstanceID)
            {
                ParticleState p = _ParticleStates[instId];
                float3 center = mul(_LocalToWorld, float4(p.position, 1)).xyz;
                float2 uv = QuadUV(id);

                float3 right = _CameraRight;
                float3 up = _CameraUp;

                #if ALIGNMENT_WORLD || ALIGNMENT_LOCAL
                float4 q = p.rotation;
                float3 axis = normalize(q.xyz);
                float angle = q.w * 2.0;
                // 简化：用 rotation 直接作为欧拉角或四元数
                // 这里先用 rotation.xyz 作为旋转轴，rotation.w 作为角度的一半
                #endif

                float3 corner = center + right * uv.x * p.size + up * uv.y * p.size;

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(corner, 1));
                o.color = UnpackColor32(p.color);
                o.uv = uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
