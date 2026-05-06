Shader "Custom/UnitVAShader"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float2> _VAPositions;
            uint _VAVertexCount;
            float _VA_DisplayScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _VA_FrameOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _VA_FrameOffset2)
                UNITY_DEFINE_INSTANCED_PROP(float, _VA_BlendWeight)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                uint frameOffsetA = (uint)UNITY_ACCESS_INSTANCED_PROP(Props, _VA_FrameOffset);
                uint frameOffsetB = (uint)UNITY_ACCESS_INSTANCED_PROP(Props, _VA_FrameOffset2);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(Props, _VA_BlendWeight);

                float2 vaPosA = _VAPositions[frameOffsetA * _VAVertexCount + v.vertexID];
                float2 vaPosB = _VAPositions[frameOffsetB * _VAVertexCount + v.vertexID];
                float2 vaPos = lerp(vaPosA, vaPosB, blendWeight);

                // Slot draw-order via micro Z-offset.
                // Vertices are ordered by Spine slot draw order: later slots have larger
                // vertexID.  Subtle negative Z pushes later slots closer to camera so they
                // pass ZTest LEqual and composite correctly over earlier slots.
                float z = -(float)v.vertexID * 0.00001f;
                float3 localPos = float3(vaPos.x * _VA_DisplayScale, vaPos.y * _VA_DisplayScale, z);
                o.vertex = TransformObjectToHClip(localPos);
                o.uv = v.uv;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 texColor = tex2D(_MainTex, i.uv);
                clip(texColor.a - 0.1);
                return texColor;
            }
            ENDHLSL
        }
    }
}
