Shader "Custom/SpriteFlatOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        // 标准的透明混合
        Blend SrcAlpha OneMinusSrcAlpha

        // =======================================================
        // 【核心黑魔法】 模版测试 (Stencil)
        // =======================================================
        Stencil
        {
            Ref 1           // 设定参考值为 1
            Comp NotEqual   // 比较规则：只有当缓冲区的值 "不等于" 1 时才绘制
            Pass Replace    // 通过规则：绘制后，把缓冲区的值 "替换" 为 1
        }
        // =======================================================

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                // 继承 SpriteRenderer 传入的颜色
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _AlphaSplitEnabled;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // 【核心修复】 像素剔除
                // 如果当前像素的 Alpha 值低于 0.1（也就是透明部分），
                // 立即丢弃该像素，不执行后面的绘制，也不修改 Stencil 缓冲区！
                clip(c.a - 0.1);

                c.rgb *= c.a; // 预乘 Alpha
                return c;
            }
        ENDCG
        }
    }
}