Shader "Custom/Effects/DashedLineInstancedPerInstance"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScrollSpeed ("Speed", Float) = 2.0
        _DashRatio ("Ratio", Range(0.1, 0.9)) = 0.5
        _TextureScale ("Texture Scale", Float) = 2.0
        _BaseColor ("Color", Color) = (1,1,1,1) // 默认颜色，会被实例化颜色覆盖
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
            float _DashRatio;
            float _ScrollSpeed;
            float _TextureScale;

            // 实例化颜色缓冲区
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _LineLength) // 连线物理长度
            UNITY_INSTANCING_BUFFER_END(Props)

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
                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float length = UNITY_ACCESS_INSTANCED_PROP(Props, _LineLength); // 获取连线长度

                // 使用物理长度乘以密度，确保长线短线的虚线一样大喵！
                float scrollingUV = (i.uv.y * length * _TextureScale) - (_Time.y * _ScrollSpeed);
                float isGap = step(_DashRatio, frac(scrollingUV));

                half4 col = color;
                col.a *= (1.0 - isGap);
                clip(col.a - 0.1); // 技霸之证
                return col;
            }
            ENDHLSL
        }
    }
}