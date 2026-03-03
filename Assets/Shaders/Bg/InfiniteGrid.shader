Shader "MineRTS/InfiniteWorldGrid"
{
    Properties
    {
        _GridColor ("网格颜色", Color) = (0.5, 0.5, 0.5, 1)
        _BgColor ("背景颜色", Color) = (0.1, 0.1, 0.1, 1)
        _GridSpacing ("网格间距", Float) = 1.0
        _LineWeight ("线条粗细", Range(0.01, 0.1)) = 0.02
        
        // 这些属性将由 CameraPropertiesShaderBridge 自动填充
        [HideInInspector] _CameraWorldPos ("相机位置", Vector) = (0,0,0,0)
        [HideInInspector] _CameraOrthoSize ("相机尺寸", Float) = 10
        [HideInInspector] _CameraAspect ("相机宽高比", Float) = 1.77
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Background" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 worldUV : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _GridColor;
            float4 _BgColor;
            float _GridSpacing;
            float _LineWeight;
            
            float4 _CameraWorldPos;
            float _CameraOrthoSize;
            float _CameraAspect;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 【核心逻辑】
                // v.uv 是 (0,1) 范围，我们需要把它映射到当前相机看到的“世界空间”
                float2 centeredUV = v.uv - 0.5; // 转为 (-0.5, 0.5)
                
                float2 viewSize;
                viewSize.y = _CameraOrthoSize * 2.0;
                viewSize.x = viewSize.y * _CameraAspect;
                
                // 计算当前像素点对应的世界坐标偏移
                o.worldUV = _CameraWorldPos.xy + (centeredUV * viewSize);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 使用世界坐标计算网格
                float2 grid = abs(frac(i.worldUV / _GridSpacing - 0.5) - 0.5) / (_LineWeight * 0.5);
                float lineFactor = min(grid.x, grid.y);
                float alpha = 1.0 - smoothstep(0.0, 1.0, lineFactor);

                fixed4 col = lerp(_BgColor, _GridColor, alpha);
                return col;
            }
            ENDCG
        }
    }
}