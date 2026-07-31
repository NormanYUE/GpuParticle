Shader "GpuParticle/Stretch"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
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
            float _StretchScale;

            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            float2 QuadUV(uint id)
            {
                uint vertexInQuad = id % 6;
                if (vertexInQuad == 1 || vertexInQuad == 4) return float2(1,0);
                if (vertexInQuad == 2 || vertexInQuad == 3) return float2(1,1);
                if (vertexInQuad == 5) return float2(0,1);
                return float2(0,0);
            }

            float4 UnpackColor32(uint c)
            {
                return float4(((c >> 0) & 0xFF) / 255.0, ((c >> 8) & 0xFF) / 255.0,
                              ((c >> 16) & 0xFF) / 255.0, ((c >> 24) & 0xFF) / 255.0);
            }

            v2f vert(uint id : SV_VertexID, uint instId : SV_InstanceID)
            {
                ParticleState p = _ParticleStates[instId];
                float3 center = mul(_LocalToWorld, float4(p.position, 1)).xyz;
                float3 worldVel = mul(_LocalToWorld, float4(p.velocity, 0)).xyz;
                float3 stretchDir = normalize(worldVel + 0.0001);
                float stretchLen = length(worldVel) * _StretchScale;
                float2 uv = QuadUV(id);
                float3 corner = center + stretchDir * (uv.y - 0.5) * stretchLen + _CameraRight * (uv.x - 0.5) * p.size;

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(corner, 1));
                o.color = UnpackColor32(p.color);
                o.uv = uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDHLSL
        }
    }
}
