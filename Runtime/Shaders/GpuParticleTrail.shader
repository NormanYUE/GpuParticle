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
                // 简化：每个 trail 控制点扩展为 2 个顶点，id 为偶数/奇数决定左右偏移
                TrailState t = _TrailStates[instId];
                float3 center = mul(_LocalToWorld, float4(t.position, 1)).xyz;
                float3 offset = (id % 2 == 0 ? -1 : 1) * float3(0, t.width * 0.5, 0);

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(center + offset, 1));
                o.color = UnpackColor32(t.color);
                o.uv = float2((float)(id % 2), (float)instId);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDHLSL
        }
    }
}
