Shader "Battle/NightVignette"
{
    Properties
    {
        // 光心（玩家）在屏幕上的归一化 UV。ShaderLab 没有 Vector2，只有 Vector(float4)，
        // C# 侧用 material.SetVector("_Center", new Vector4(u, v, 0, 0))，仅 .xy 有效。
        // 原点在左下角，与 UGUI 贴图 uv 一致（WorldToScreenPoint 的 y/Screen.height 正好对应；
        // 若 C# 改用左上原点的坐标请自行 1-v 翻转）。
        _Center ("Center UV (.xy = player screen UV 0~1)", Vector) = (0.5, 0.5, 0, 0)

        // 光圈半径。单位 = 屏幕高度的比例（0.5 = 半径占整屏高度的一半）。
        // 推荐由 C# 驱动：_Radius = LightRadiusWorld / (2f * Camera.main.orthographicSize)
        // （正交相机纵向可视世界高 = 2*orthoSize，正好覆盖屏幕高度，故该比值即高度比例）。
        _Radius ("Radius (fraction of screen height)", Float) = 0.5

        // 屏幕宽高比 = Screen.width / (float)Screen.height（竖屏 720x1280 ≈ 0.5625）。
        // 用途：把 x 方向的 UV 距离折算成与 y 相同的“物理长度”，否则按纯 UV 距离算
        // 在竖屏会被拉成横向椭圆。C# 每帧（或分辨率变化时）写入。
        _Aspect ("Screen Aspect (width / height)", Float) = 1.0

        // 压暗色：RGB = 色调（夜=冷蓝 / 炼狱=暗红），A = 圈外的目标压暗强度（夜 ≈ 0.42）。
        // 由 C# 按 DayPhase 插值写入（例如 lerp(DayColor, NightColor, t)）。
        _DarkColor ("Dark Tint (RGB + peak alpha)", Color) = (0.06, 0.10, 0.22, 0.42)

        // 边缘过渡宽度，单位与 _Radius 相同。越大“亮圈→压暗”的过渡越柔。
        _Softness ("Edge Softness (same unit as Radius)", Float) = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // UI 全屏遮罩状态：关背面剔除、关深度写入、标准透明混合。
        // ZTest Always：保证覆盖 3D/unlit 场景，层级顺序靠 Canvas/SortingOrder 控制（在 HUD 之下）。
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
            // 沿用项目唯一在用的自定义 Shader（BattleFootShadow.shader）的 CGPROGRAM + UnityCG.cginc 写法，
            // 避免在本项目 URP 版本里猜 HLSLPROGRAM / Core.hlsl 的 include 路径。
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Center;   // 只取 .xy；与 Properties 的 Vector(float4) 对应
            float  _Radius;
            float  _Aspect;
            fixed4 _DarkColor;
            float  _Softness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex); // UGUI 全屏 Image 的标准顶点变换
                OUT.uv = IN.texcoord;                         // 0~1，原点左下角
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1) 当前像素到光心的 UV 差（原点/方向与屏幕像素一致：左下为 0,0）。
                float2 delta = IN.uv - _Center.xy;

                // 2) 宽高比补偿：x 乘以 aspect(W/H) 后，delta 的模就是“按屏幕高度归一化”的
                //    等物理距离。竖屏 aspect<1 → x 被压缩，屏幕上是正圆而非横向椭圆。
                delta.x *= _Aspect;

                // 3) 归一化距离，单位与 _Radius / _Softness 一致（屏幕高度比例）。
                float dist = length(delta);

                // 4) 暗化系数：圈内 (dist <= Radius-Softness) = 0 全透；
                //    圈外 (dist >= Radius+Softness) = 1 满 _DarkColor；中间平滑过渡。
                //    max 防止 _Softness=0 时 smoothstep 两个 edge 相等而退化。
                float soft = max(_Softness, 0.0001);
                float darken = smoothstep(_Radius - soft, _Radius + soft, dist);

                // 5) 输出：用 alpha 承载压暗强度。因为混合是 Blend SrcAlpha OneMinusSrcAlpha
                //    （非预乘），这里只缩放 alpha、保持 rgb = _DarkColor.rgb：
                //      darken=0 → alpha 0，完全露出底下画面；
                //      darken=1 → alpha=_DarkColor.a（如夜 0.42），底图被冷蓝/暗红压暗。
                //    ⚠ 不要把 rgb 也乘 darken：那会与 SrcAlpha 混合二次相乘导致边缘发黑发脏
                //    （BattleFootShadow.shader 里的 c.rgb *= c.a 是它自己的预乘写法，别照搬）。
                fixed4 col = _DarkColor;
                col.a *= darken;
                return col;
            }
            ENDCG
        }
    }
}
