Shader "Custom/Effects/SolidLineInstanced"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0, 1, 1, 0.8) // 青色半透明默认
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing // 🔥 核心：开启实例化支持

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // 🔥 核心
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // 🔥 核心
            };

            sampler2D _MainTex;
            float4 _BaseColor; // 全局颜色，所有实例共享

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);

                // 直接使用全局颜色，忽略纹理
                half4 col = _BaseColor;

                // 🔥 关键：丢弃完全透明的部分，避免透明部分写入深度
                clip(col.a - 0.1);

                return col;
            }
            ENDHLSL
        }
    }
}