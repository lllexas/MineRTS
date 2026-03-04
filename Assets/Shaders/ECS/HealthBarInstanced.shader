Shader "Custom/ECS/HealthBarInstanced"
{
    Properties
    {
        _BaseColor("Background Color", Color) = (0.1, 0.1, 0.1, 1)
        _HealthColor("Health Color", Color) = (0.2, 0.8, 0.2, 1)
        _LowHealthColor("Low Health Color", Color) = (0.8, 0.2, 0.2, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            ZWrite Off
            ZTest Always // 保证血条永远在模型前面（永远在最前面）
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing // 开启实例化支持

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HealthColor;
                float4 _LowHealthColor;
            CBUFFER_END

            // 定义实例化缓冲区
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _FillAmount) // 每个实例独特的血量百分比
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 从实例化缓冲区中获取当前实例的血量
                float fill = UNITY_ACCESS_INSTANCED_PROP(Props, _FillAmount);

                // 简单的 UV 遮罩逻辑
                float mask = step(input.uv.x, fill);

                // 根据血量动态变色（血量低时变红）
                half4 barColor = lerp(_LowHealthColor, _HealthColor, fill);

                // 混合背景色和血色条
                half4 finalColor = lerp(_BaseColor, barColor, mask);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
