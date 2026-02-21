Shader "Custom/Effects/DashedLine"
{
    Properties
    {
        _MainTex ("Texture (Unused)", 2D) = "white" {} // 占位符，为了让 Tiling 生效
        _ScrollSpeed ("Scroll Speed", Float) = 2.0
        _Color ("Line Color", Color) = (1,1,1,1)
        _DashRatio ("Dash Ratio", Range(0.1, 0.9)) = 0.5
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
        }
        
        LOD 100
        
        // 混合模式：标准透明混合
        Blend SrcAlpha OneMinusSrcAlpha
        // 关闭深度写入，防止半透明物体相互遮挡出问题
        ZWrite Off
        // 关闭剔除，这样线的正反面都能看到（以防 LineRenderer 扭曲）
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // 接收 LineRenderer 传入的顶点色
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float _DashRatio;
            float4 _MainTex_ST; // 这是一个魔法变量，Unity 会自动把 material.mainTextureScale 传进来
            float _ScrollSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 【关键】应用 Unity 的 Tiling/Offset 设置
                // 我们的 C# 代码里修改 mainTextureScale 就是在修改这个 TRANSFORM_TEX 的结果
                o.uv = TRANSFORM_TEX(v.uv, _MainTex); // _MainTex 虽然没定义但在宏里默认需要
                
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // i.uv.x 代表线段的长度方向（因为 C# 开启了 Tile 模式）
                // frac(i.uv.x) 会返回 0 到 1 的小数部分，形成锯齿波
                
                // step(edge, x) -> 如果 x < edge 返回 0，否则返回 1
                // 比如 _DashRatio 是 0.5：
                // 0.0 ~ 0.5 (实线部分) -> frac > 0.5 为假 -> alpha=1 (显色)
                // 0.5 ~ 1.0 (空隙部分) -> frac > 0.5 为真 -> alpha=0 (透明)
                // 注意：这里为了方便控制，反着写逻辑

                // 【动画黑魔法】 加入 _Time.y 让 UV 随时间滚动
                // 这样虚线看起来就像能量在管线里流动一样喵！
                float scrollingUV = i.uv.x - (_Time.y * _ScrollSpeed);
        
                float isGap = step(_DashRatio, frac(scrollingUV)); 
        
                fixed4 col = i.color;
                col.a *= (1.0 - isGap);

                return col;
            }
            ENDCG
        }
    }
    // 这是一个 fallback，如果没有 MainTex 属性 TRANSFORM_TEX 宏可能会报错，
    // 为了兼容性，我们在 Properties 块虽没写 _MainTex 但 Unity 内部逻辑通常默认存在。
    // 如果报错，可以在 Properties 里加一行: _MainTex ("Texture", 2D) = "white" {}
}