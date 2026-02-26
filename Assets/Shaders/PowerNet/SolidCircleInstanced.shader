Shader "Custom/Effects/SolidCircleInstanced"
{
    Properties
    {
        _MainTex ("贴图", 2D) = "white" {}
        _BaseColor("基础颜色", Color) = (1,1,1,1)
        _AlphaCap("Alpha上限", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off // 关闭深度写入，避免透明部分相互遮挡
        Cull Off

        // 单Pass Stencil增量判定架构
        // 解决渲染断裂和叠加失效问题
        // Stencil逻辑：Ref 2设定最大重叠次数，Comp Less确保当前Stencil值小于2时才能绘制
        // Pass IncrSat使绘制后Stencil值+1，实现重叠次数计数
        Pass
        {
            Stencil
            {
                Ref 2           // 设定最大重叠次数，例如2次
                Comp Greater    // 只要当前Stencil值小于2就能画
                Pass IncrSat    // 画完后当前像素的Stencil值+1
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off // 关闭深度写入，避免透明部分相互遮挡
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float _AlphaCap;

            // 实例化颜色缓冲区
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
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
                half4 texColor = tex2D(_MainTex, i.uv);
                half4 finalColor = texColor * color;

                // Alpha上限控制
                finalColor.a = min(finalColor.a, _AlphaCap);

                // 🔥 关键改动：丢弃完全透明的部分，避免贴图的透明方块区域浪费性能
                // 由于Stencil使用Comp Less，低Alpha边缘不再阻挡后续实例，彻底解决透明接缝问题
                clip(finalColor.a - 0.1);

                return finalColor;
            }
            ENDHLSL
        }
    }
}