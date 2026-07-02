Shader "ActionRPG/UI/SwirlVoronoiCharge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color (HDR)", Color) = (0, 0.5, 1, 1)
        
        _RadialFill ("Radial Fill Amount", Range(0, 1)) = 1.0
        _CenterFill ("Center Fill Amount", Range(0, 1)) = 1.0
        _AngleOffset ("Angle Offset (Degrees)", Float) = 0.0
        
        _SwirlSpeed ("Swirl Speed", Float) = 2.0
        _SuckSpeed ("Suck Speed", Float) = -1.0
        _PatternScale ("Pattern Scale", Float) = 6.0
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
            float _PatternScale;
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

            // --- 일정한 거리의 점박이 격자 (Polka Dot) 패턴 함수 ---
            float UniformDots(float2 uv)
            {
                // uv를 0~1 사이로 반복되는 바둑판 격자로 만듭니다.
                float2 grid = frac(uv);
                // 각 격자의 정중앙(0.5, 0.5)으로부터의 거리를 잽니다.
                float distFromCenter = length(grid - 0.5);
                
                // 중앙에 일정 크기의 완벽한 동그라미를 그립니다. (부드러운 테두리 안티앨리어싱 적용)
                return 1.0 - smoothstep(0.15, 0.35, distFromCenter);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1. 왜곡 없는 완벽한 평면 UV (일정하게 박힌 점을 위해)
                float2 baseUV = IN.texcoord;
                
                // 점박이 패턴 적용 (간격이 일정한 완벽한 동그라미 격자)
                float noise = UniformDots(baseUV * _PatternScale);

                // 2. 마스크 생성을 위한 극좌표계(Polar Coordinates) 계산
                float2 centeredUV = IN.texcoord - 0.5;
                float dist = length(centeredUV) * 2.0; 
                float angle = atan2(centeredUV.x, centeredUV.y); 
                
                // 시작 각도 오프셋을 적용합니다.
                // 각도(Degrees)를 라디안으로 변환하여 더해줍니다.
                angle += _AngleOffset * 3.14159265359 / 180.0;
                
                float normAngle = 1.0 - frac((angle / 6.28318530718) + 0.5);

                // 원형 자르기
                float circleMask = step(dist, 1.0) * smoothstep(1.0, 0.98, dist);

                // 3. 두 개의 독립된 파라미터 (Radial 과 Center)
                float radialMask = step(normAngle, _RadialFill);
                float centerMask = step(dist, _CenterFill);
                float fillMask = radialMask * centerMask;

                // 4. 각각의 파라미터에 대한 경계선 엣지 발광
                float radialEdge = smoothstep(_RadialFill, _RadialFill - 0.05, normAngle) * smoothstep(_RadialFill - 0.1, _RadialFill, normAngle);
                float centerEdge = smoothstep(_CenterFill, _CenterFill - 0.05, dist) * smoothstep(_CenterFill - 0.1, _CenterFill, dist);
                
                // 마스크 조합에 맞춰 엣지 출력
                float edgeMask = (radialEdge * centerMask) + (centerEdge * radialMask);
                float edgeGlow = edgeMask * _EdgeGlow;

                // 5. 최종 합성
                float3 finalColor = _GlowColor.rgb * noise + (_GlowColor.rgb * edgeGlow);
                float finalAlpha = noise * fillMask * circleMask * IN.color.a * _GlowColor.a;

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
