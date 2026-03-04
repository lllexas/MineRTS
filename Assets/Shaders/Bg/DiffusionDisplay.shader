// DiffusionDisplay.shader
// 几何密铺渲染器 Shader（世界空间对齐版本）
// 功能：基于世界空间三角形密铺的 SDF 纹理渲染

Shader "MineRTS/DiffusionDisplay"
{
    Properties
    {
        // 纹理输入
        _SDFTex ("SDF 纹理", 2D) = "black" {}
        _EffectTex ("扩散纹理", 2D) = "black" {}
        _DiffuseTex ("扩散纹理备份", 2D) = "black" {}
        _SDFBlendFactor ("SDF 混合系数", Range(0,1)) = 0.5

        // 几何密铺参数
        _GridScale ("网格缩放 (每世界单位三角形数)", Float) = 2.0
        _TriangleSize ("三角形尺寸", Range(0.1, 2.0)) = 0.8
        _TriangleScalePower ("三角形缩放强度", Range(0, 2)) = 1.0

        // 外观参数
        _BgColor ("背景颜色", Color) = (0, 0, 0, 1)
        _FillColor ("填充颜色", Color) = (0.2, 0.6, 1.0, 1.0)
        _EdgeColor ("边缘颜色", Color) = (0.5, 0.5, 0.5, 1.0)
        _EdgeWidth ("边缘宽度", Range(0.001, 0.1)) = 0.02
        _EdgeSmoothness ("边缘平滑度", Range(0.001, 0.1)) = 0.005

        // 颜色调整（向后兼容）
        _TintColor ("颜色 tint", Color) = (1, 1, 1, 1)
        _Brightness ("亮度", Range(0, 3)) = 1.0
        _Contrast ("对比度", Range(0.5, 3)) = 1.0
        _Gamma ("Gamma", Range(0.5, 2.5)) = 1.0

        // 这些属性将由 DiffusionFieldBridge 自动设置
        [HideInInspector] _CameraOrthoSize ("正交尺寸", Float) = 10
        [HideInInspector] _CameraAspect ("宽高比", Float) = 1.77
        [HideInInspector] _CameraWorldPos ("相机世界位置", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "DiffusionDisplay"

            // 混合模式：Alpha 混合
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // 纹理采样器
            sampler2D _EffectTex;
            sampler2D _DiffuseTex;
            sampler2D _SDFTex;

            // SDF 混合参数
            float _SDFBlendFactor;

            // 几何密铺参数
            float _GridScale;
            float _TriangleSize;
            float _TriangleScalePower;

            // 外观参数
            float4 _BgColor;
            float4 _FillColor;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeSmoothness;

            // 颜色调整参数
            float4 _TintColor;
            float _Brightness;
            float _Contrast;
            float _Gamma;

            // 相机参数（用于世界空间对齐）
            float _CameraOrthoSize;
            float _CameraAspect;
            float4 _CameraWorldPos;
            
            // 顶点输入
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            // 顶点输出
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // 顶点着色器
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 颜色调整函数（向后兼容）
            float3 AdjustColor(float3 color, float brightness, float contrast, float gamma)
            {
                // 应用亮度
                color *= brightness;

                // 应用对比度
                float halfContrast = (1.0 - contrast) * 0.5;
                color = (color - halfContrast) / (1.0 - 2.0 * halfContrast);

                // 应用 Gamma 校正
                color = pow(saturate(color), 1.0 / gamma);

                return saturate(color);
            }

            // 正三角形 SDF 函数
            float sdEquilateralTriangle(float2 p, float size)
            {
                const float k = sqrt(3.0);
                p.x = abs(p.x) - size;
                p.y = p.y + size/k;

                if (p.x + k * p.y > 0.0)
                {
                    p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
                }

                p.x -= clamp(p.x, -2.0 * size, 0.0);
                return -length(p) * sign(p.y);
            }

            // 三角形密铺映射函数（轴对齐歪斜坐标版本）
            // 核心思想：使用轴对齐歪斜坐标变换，生成水平对齐的正三角形
            // 输入：世界坐标
            // 输出：三角形内的局部坐标、三角形中心世界坐标、是否朝上
            void TriangleTilingMapping(float2 worldPos, out float2 localPos, out float2 centerWorldPos, out bool isUp)
            {
                // ==================== 第一步：轴对齐歪斜坐标 (Axial Coordinates) ====================
                // 这是图形学中标准的六角/三角网格变换，绝对不会旋转坐标轴！
                float2 p = worldPos * _GridScale;

                float2 gridPos;
                gridPos.y = p.y * 1.154700538;  // p.y * (2.0 / sqrt(3.0))
                gridPos.x = p.x - gridPos.y * 0.5;

                // ==================== 第二步：确定所属格子与切分 ====================
                float2 cell = floor(gridPos);
                float2 fracPos = gridPos - cell;

                // 在这套纯净的坐标系中，对角线 x+y=1 完美切分出两个正三角形
                bool inLowerTriangle = (fracPos.x + fracPos.y) < 1.0;
                isUp = inLowerTriangle; // 左下角这个是尖尖朝上的

                // ==================== 第三步：计算完美的重心 ====================
                float2 centerGrid;
                if (inLowerTriangle)
                {
                    centerGrid = cell + float2(1.0/3.0, 1.0/3.0);
                }
                else
                {
                    centerGrid = cell + float2(2.0/3.0, 2.0/3.0);
                }

                // ==================== 第四步：逆变换回世界空间 ====================
                centerWorldPos.x = centerGrid.x + centerGrid.y * 0.5;
                centerWorldPos.y = centerGrid.y * 0.866025403; // centerGrid.y * (sqrt(3.0) / 2.0)

                centerWorldPos /= _GridScale;

                // ==================== 第五步：完美局部坐标与翻转 ====================
                // 用同在世界坐标系下的差值，确保比例 1:1，绝对不会变形
                localPos = (worldPos - centerWorldPos) * _GridScale;

                if (!isUp)
                {
                    // 右上角的三角形，尖尖必须朝下！
                    localPos.y = -localPos.y;
                }
            }

            // 片元着色器 - 几何密铺渲染器（世界空间版本）
            fixed4 frag (v2f i) : SV_Target
            {
                // ==================== 1. 建立世界空间坐标系 ====================
                float2 viewportSize = float2(_CameraOrthoSize * 2.0 * _CameraAspect, _CameraOrthoSize * 2.0);
                float2 worldPos = _CameraWorldPos.xy + (i.uv - 0.5) * viewportSize;

                // ==================== 2. 三角形密铺映射 ====================
                float2 localPos;
                float2 centerWorldPos;
                bool isUp;
                TriangleTilingMapping(worldPos, localPos, centerWorldPos, isUp);

                // ==================== 3. 中心采样逻辑 ====================
                // 将世界坐标转换为 SDF 纹理的 UV 坐标（0~1）
                float2 sdfUV = (centerWorldPos - _CameraWorldPos.xy) / viewportSize + 0.5;
                sdfUV = clamp(sdfUV, 0, 1);  // 边界保护

                // 采样 SDF 纹理获取 power 值
                float power = tex2D(_SDFTex, sdfUV).r;
                power = saturate(power);

                // ==================== 4. 计算缩放后的三角形尺寸 ====================
                // 修正 1：填充常数从 0.5 改为 0.57735（1/√3）
                // 这样三角形满格时能完全填满格子，顶点对齐
                float scaleMultiplier = lerp(1.0, power, _TriangleScalePower);
                //float scaledSize = 0.57735 * _TriangleSize * scaleMultiplier;
                float scaledSize = 1.0 * _TriangleSize * scaleMultiplier;

                // ==================== 5. 解耦距离场：常驻框线 vs 缩放填充 ====================
                // 修正 2：计算两个距离场
                // staticDist - 固定大小，用于常驻框线（不随 Power 缩放）
                // float staticSize = 0.57735 * _TriangleSize;  // 固定最大值
                float staticSize = 1.0 * _TriangleSize;  // 固定最大值
                float staticDist = sdEquilateralTriangle(localPos, staticSize);
                
                // dynamicDist - 随 Power 缩放，用于填充
                float dynamicDist = sdEquilateralTriangle(localPos, scaledSize);

                // ==================== 6. 计算边框和填充 ====================
                // 填充区域（使用 dynamicDist，随 Power 缩放）
                float fillMask = step(0, -dynamicDist);  // dynamicDist < 0 时为 1

                // 边框区域（使用 staticDist，固定在格子边界）
                float edgeFactor = smoothstep(_EdgeWidth, _EdgeWidth + _EdgeSmoothness, abs(staticDist));
                float edgeMask = 1.0 - edgeFactor;

                // ==================== 7. 混合颜色 ====================
                // 修正：线框必须在最上层，不能被填充盖住！
                
                // 第一步：先画黑色背景
                float3 finalColor = _BgColor.rgb;
                
                // 第二步：先画填充（受 fillMask 控制）
                float3 fillColor = _FillColor.rgb * max(power, 0.2);  // 至少 20% 亮度
                finalColor = lerp(finalColor, fillColor, fillMask);
                
                // 第三步：最后画线框（不受 fillMask 控制，在最上层！）
                finalColor = lerp(finalColor, _EdgeColor.rgb, edgeMask);

                // 强制不透明
                float finalAlpha = 1.0;

                // ==================== 8. 向后兼容：颜色调整 ====================
                finalColor = AdjustColor(finalColor, _Brightness, _Contrast, _Gamma);
                finalColor *= _TintColor.rgb;

                // ==================== 9. 可选：与原始扩散纹理混合 ====================
                #ifdef ENABLE_DIFFUSION_BLEND
                fixed4 diffuseCol = tex2D(_EffectTex, i.uv);
                finalColor = lerp(finalColor, diffuseCol.rgb, _SDFBlendFactor);
                finalAlpha = lerp(finalAlpha, diffuseCol.a, _SDFBlendFactor);
                #endif

                return fixed4(finalColor, finalAlpha);
            }

            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
