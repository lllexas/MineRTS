Shader "MineRTS/InfiniteWorldGrid"
{
    Properties
    {
        // 网格参数
        _GridDensity ("Grid Density", Float) = 1.0
        _MajorGridColor ("Major Grid Color", Color) = (0.3, 0.3, 0.3, 0.5)
        _MinorGridColor ("Minor Grid Color", Color) = (0.2, 0.2, 0.2, 0.2)
        _GridThickness ("Grid Thickness", Float) = 0.02

        // 相机参数（由脚本动态设置）
        _CameraOrthoSize ("Camera Ortho Size", Float) = 5.0
        _CameraAspect ("Camera Aspect Ratio", Float) = 1.777
        _CameraWorldPos ("Camera World Position", Vector) = (0, 0, 0, 0)

        // 节点参数（未来用于绘制地图节点）
        _NodeRadius ("Node Radius", Float) = 0.2
        _NodeColor ("Node Color", Color) = (0.1, 0.5, 0.9, 0.8)

        // 调试选项
        [Toggle] _ShowDebugGrid ("Show Debug Grid", Float) = 0
        _DebugGridColor ("Debug Grid Color", Color) = (1, 0, 0, 0.5)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            // 属性变量
            float _GridDensity;
            float4 _MajorGridColor;
            float4 _MinorGridColor;
            float _GridThickness;

            float _CameraOrthoSize;
            float _CameraAspect;
            float3 _CameraWorldPos;

            float _NodeRadius;
            float4 _NodeColor;

            float _ShowDebugGrid;
            float4 _DebugGridColor;

            // GPU缓冲区数据（通过ComputeBuffer传递）
            struct NodeData
            {
                float4 positionAndRadius; // x,y:位置, z:半径, w:保留
                float4 colorAndFlags;     // x,y,z,w:颜色(RGBA)
                float4 attributes;        // x:标志位, yzw:保留
            };

            struct EdgeData
            {
                float4 fromPosAndThickness; // x,y:起点位置, z:厚度, w:保留
                float4 toPosAndFlags;       // x,y:终点位置, z:保留, w:标志位
                float4 colorAndAttributes;  // x,y,z,w:颜色(RGBA)
            };

            StructuredBuffer<NodeData> _NodeBuffer;
            int _NodeCount;

            StructuredBuffer<EdgeData> _EdgeBuffer;
            int _EdgeCount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // 计算世界位置
                // 由于Quad是相机子物体，其世界位置随相机移动
                // 我们需要计算每个顶点的绝对世界坐标
                float3 localPos = v.vertex.xyz;
                float3 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1.0)).xyz;
                o.worldPos = worldPos;

                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            // 绘制网格线
            float4 DrawGrid(float3 worldPos, float4 baseColor)
            {
                // 将世界坐标转换为网格空间
                float2 gridPos = worldPos.xy * _GridDensity;

                // 计算到最近网格线的距离
                float2 gridFraction = frac(gridPos);
                float2 distanceToLine = min(gridFraction, 1.0 - gridFraction);

                // 主网格线（每1单位）
                float2 majorGridFraction = frac(gridPos);
                float2 majorDistance = min(majorGridFraction, 1.0 - majorGridFraction);

                // 次网格线（每0.1单位，如果网格密度较高）
                float2 minorGridFraction = frac(gridPos * 10.0);
                float2 minorDistance = min(minorGridFraction, 1.0 - minorGridFraction);

                // 计算主网格线强度
                float majorLineX = 1.0 - smoothstep(0.0, _GridThickness, majorDistance.x);
                float majorLineY = 1.0 - smoothstep(0.0, _GridThickness, majorDistance.y);
                float majorLine = max(majorLineX, majorLineY);

                // 计算次网格线强度
                float minorLineX = 1.0 - smoothstep(0.0, _GridThickness * 0.5, minorDistance.x);
                float minorLineY = 1.0 - smoothstep(0.0, _GridThickness * 0.5, minorDistance.y);
                float minorLine = max(minorLineX, minorLineY) * 0.5;

                // 混合网格颜色
                float4 gridColor = float4(0, 0, 0, 0);

                if (majorLine > 0.01)
                {
                    gridColor = lerp(gridColor, _MajorGridColor, majorLine);
                }

                if (minorLine > 0.01 && majorLine < 0.1)
                {
                    gridColor = lerp(gridColor, _MinorGridColor, minorLine);
                }

                // 与基础颜色混合（网格线应该覆盖在基础颜色上）
                return float4(lerp(baseColor.rgb, gridColor.rgb, gridColor.a), max(baseColor.a, gridColor.a));
            }

            // 绘制节点（从ComputeBuffer读取真实数据）
            float4 DrawNodes(float3 worldPos, float4 baseColor)
            {
                float4 nodeColor = baseColor;

                // 如果Buffer无效或没有节点，返回基础颜色
                if (_NodeCount <= 0)
                {
                    return nodeColor;
                }

                // 遍历所有节点
                for (int i = 0; i < _NodeCount; i++)
                {
                    NodeData node = _NodeBuffer[i];

                    // 提取节点数据
                    float2 nodePos = node.positionAndRadius.xy;
                    float nodeRadius = node.positionAndRadius.z;
                    float4 nodeColorValue = node.colorAndFlags; // 直接使用RGBA颜色

                    // 计算距离
                    float2 delta = worldPos.xy - nodePos;
                    float distance = length(delta);

                    // 节点半径内绘制
                    if (distance < nodeRadius)
                    {
                        // 平滑衰减
                        float falloff = 1.0 - smoothstep(nodeRadius * 0.3, nodeRadius, distance);
                        nodeColor = lerp(nodeColor, nodeColorValue, falloff * nodeColorValue.a);
                    }
                }

                return nodeColor;
            }

            // 绘制边（从ComputeBuffer读取真实数据）
            float4 DrawEdges(float3 worldPos, float4 baseColor)
            {
                float4 edgeColor = baseColor;

                // 如果Buffer无效或没有边，返回基础颜色
                if (_EdgeCount <= 0)
                {
                    return edgeColor;
                }

                // 遍历所有边
                for (int i = 0; i < _EdgeCount; i++)
                {
                    EdgeData edge = _EdgeBuffer[i];

                    // 提取边数据
                    float2 fromPos = edge.fromPosAndThickness.xy;
                    float2 toPos = edge.toPosAndFlags.xy;
                    float thickness = edge.fromPosAndThickness.z;
                    float4 lineColor = edge.colorAndAttributes; // 直接使用RGBA颜色

                    // 计算点到线段的距离
                    float2 lineDir = toPos - fromPos;
                    float lineLength = length(lineDir);

                    if (lineLength < 0.001) continue; // 跳过无效边

                    float2 lineUnit = lineDir / lineLength;
                    float2 toPoint = worldPos.xy - fromPos;

                    // 投影点到线段
                    float projection = dot(toPoint, lineUnit);

                    if (projection < 0.0)
                    {
                        // 点在线段起点之前，计算到起点的距离
                        float distance = length(toPoint);
                        if (distance < thickness)
                        {
                            // 平滑衰减
                            float falloff = 1.0 - smoothstep(thickness * 0.3, thickness, distance);
                            edgeColor = lerp(edgeColor, lineColor, falloff * lineColor.a);
                        }
                    }
                    else if (projection > lineLength)
                    {
                        // 点在线段终点之后，计算到终点的距离
                        float2 toEnd = worldPos.xy - toPos;
                        float distance = length(toEnd);
                        if (distance < thickness)
                        {
                            // 平滑衰减
                            float falloff = 1.0 - smoothstep(thickness * 0.3, thickness, distance);
                            edgeColor = lerp(edgeColor, lineColor, falloff * lineColor.a);
                        }
                    }
                    else
                    {
                        // 点在线段上，计算垂直距离
                        float2 perpendicular = toPoint - lineUnit * projection;
                        float distance = length(perpendicular);

                        if (distance < thickness)
                        {
                            // 平滑衰减（沿线段方向也有衰减）
                            float lineFalloff = 1.0 - smoothstep(thickness * 0.3, thickness, distance);
                            float lengthFalloff = 1.0 - abs(projection / lineLength - 0.5) * 2.0; // 中间更亮
                            float falloff = lineFalloff * lengthFalloff;

                            edgeColor = lerp(edgeColor, lineColor, falloff * lineColor.a);
                        }
                    }
                }

                return edgeColor;
            }

            // 绘制调试网格（显示相机边界等）
            float4 DrawDebugGrid(float3 worldPos, float4 baseColor)
            {
                if (_ShowDebugGrid < 0.5) return baseColor;

                float4 debugColor = baseColor;

                // 计算相机边界
                float cameraWidth = _CameraOrthoSize * 2.0 * _CameraAspect;
                float cameraHeight = _CameraOrthoSize * 2.0;

                float2 cameraMin = _CameraWorldPos.xy - float2(cameraWidth * 0.5, cameraHeight * 0.5);
                float2 cameraMax = _CameraWorldPos.xy + float2(cameraWidth * 0.5, cameraHeight * 0.5);

                // 检查是否在相机边界附近
                float borderDistance = 0.1;
                float2 borderMin = cameraMin + borderDistance;
                float2 borderMax = cameraMax - borderDistance;

                // 如果在边界附近，绘制红色边框
                if (worldPos.x < borderMin.x || worldPos.x > borderMax.x ||
                    worldPos.y < borderMin.y || worldPos.y > borderMax.y)
                {
                    float borderAlpha = 0.3;
                    debugColor = lerp(debugColor, _DebugGridColor, borderAlpha);
                }

                return debugColor;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 基础颜色（透明黑色）
                float4 color = float4(0, 0, 0, 0);

                // 绘制无限世界网格
                color = DrawGrid(i.worldPos, color);

                // 绘制边（从GPU缓冲区）
                color = DrawEdges(i.worldPos, color);

                // 绘制节点（从GPU缓冲区）
                color = DrawNodes(i.worldPos, color);

                // 绘制调试信息
                color = DrawDebugGrid(i.worldPos, color);

                // 应用雾效
                UNITY_APPLY_FOG(i.fogCoord, color);

                return color;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
    CustomEditor "InfiniteWorldGridShaderEditor"
}