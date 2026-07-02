Shader "ActionRPG/UI/SolidColorCharge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Edge Glow Color (HDR)", Color) = (1, 0, 0, 1)
        
        _RadialFill ("Radial Fill Amount", Range(0, 1)) = 1.0
        _CenterFill ("Center Fill Amount", Range(0, 1)) = 1.0
        _AngleOffset ("Angle Offset (Degrees)", Float) = 0.0
        _EdgeGlow ("Edge Glow Intensity", Float) = 3.0
        
        // UI Mask Support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _GlowColor;
            float _RadialFill;
            float _CenterFill;
            float _AngleOffset;
            float _EdgeGlow;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1. 마스크 생성을 위한 극좌표계(Polar Coordinates) 계산
                float2 centeredUV = IN.texcoord - 0.5;
                float dist = length(centeredUV) * 2.0; 
                float angle = atan2(centeredUV.x, centeredUV.y); 
                
                angle += _AngleOffset * 3.14159265359 / 180.0;
                
                float normAngle = 1.0 - frac((angle / 6.28318530718) + 0.5);

                // 원형 자르기 (네모난 이미지를 깔끔한 원형으로)
                float circleMask = step(dist, 1.0) * smoothstep(1.0, 0.98, dist);

                // 2. 두 개의 독립된 파라미터로 채우기 적용
                float radialMask = step(normAngle, _RadialFill);
                float centerMask = step(dist, _CenterFill);
                float fillMask = radialMask * centerMask;

                // 3. 각각의 파라미터에 대한 경계선 엣지 발광 효과
                float radialEdge = smoothstep(_RadialFill, _RadialFill - 0.05, normAngle) * smoothstep(_RadialFill - 0.1, _RadialFill, normAngle);
                float centerEdge = smoothstep(_CenterFill, _CenterFill - 0.05, dist) * smoothstep(_CenterFill - 0.1, _CenterFill, dist);
                
                // 마스크 조합에 맞춰 엣지 출력
                float edgeMask = (radialEdge * centerMask) + (centerEdge * radialMask);
                float edgeGlow = edgeMask * _EdgeGlow;

                // 4. 점박이 패턴 없이, Image의 기본 색상에 야광만 합성
                float3 finalColor = IN.color.rgb + (_GlowColor.rgb * edgeGlow);
                
                // 알파값(투명도) 적용
                float finalAlpha = fillMask * circleMask * IN.color.a;

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
