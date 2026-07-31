Shader "GpuParticle/Mesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "UnityCG.cginc"

            struct MeshTransform
            {
                float3 position;
                float4 rotation;
                float3 scale;
                uint color;
            };

            StructuredBuffer<MeshTransform> _MeshTransforms;
            float4x4 _LocalToWorld;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float4 UnpackColor32(uint c)
            {
                return float4(
                    ((c >> 0) & 0xFF) / 255.0,
                    ((c >> 8) & 0xFF) / 255.0,
                    ((c >> 16) & 0xFF) / 255.0,
                    ((c >> 24) & 0xFF) / 255.0);
            }

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // instance ID 由 Unity 设置
                #endif
            }

            float3 QuaternionRotate(float4 q, float3 v)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                MeshTransform t = _MeshTransforms[instanceID];
                float3 pos = QuaternionRotate(t.rotation, v.vertex.xyz * t.scale) + t.position;

                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, mul(_LocalToWorld, float4(pos, 1)));
                o.color = UnpackColor32(t.color);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDHLSL
        }
    }
}
