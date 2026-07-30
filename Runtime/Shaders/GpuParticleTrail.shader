Shader "GpuParticle/Trail"
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

            struct TrailState
            {
                float3 position;
                float width;
                uint color;
                uint particleId;
            };

            StructuredBuffer<TrailState> _TrailStates;
            float4x4 _LocalToWorld;

            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            float4 UnpackColor32(uint c)
            {
                return float4(((c >> 0) & 0xFF) / 255.0, ((c >> 8) & 0xFF) / 255.0,
                              ((c >> 16) & 0xFF) / 255.0, ((c >> 24) & 0xFF) / 255.0);
            }

            v2f vert(uint id : SV_VertexID, uint instId : SV_InstanceID)
            {
                // 每个 trail 控制点扩展为 6 个顶点（2 个三角形组成一个朝向相机的矩形）
                TrailState t = _TrailStates[instId];
                float3 center = mul(_LocalToWorld, float4(t.position, 1)).xyz;
                float halfWidth = t.width * 0.5;

                // 0: left-bottom, 1: right-bottom, 2: right-top
                // 3: left-bottom, 4: right-top, 5: left-top
                float2 uv;
                float2 corner;
                switch (id % 6)
                {
                    case 0: uv = float2(0, 0); corner = float2(-halfWidth, -halfWidth); break;
                    case 1: uv = float2(1, 0); corner = float2(halfWidth, -halfWidth); break;
                    case 2: uv = float2(1, 1); corner = float2(halfWidth, halfWidth); break;
                    case 3: uv = float2(0, 0); corner = float2(-halfWidth, -halfWidth); break;
                    case 4: uv = float2(1, 1); corner = float2(halfWidth, halfWidth); break;
                    default: uv = float2(0, 1); corner = float2(-halfWidth, halfWidth); break;
                }

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(center + float3(corner.x, corner.y, 0), 1));
                o.color = UnpackColor32(t.color);
                o.uv = uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDHLSL
        }
    }
}
