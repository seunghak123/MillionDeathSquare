Shader "Seunghak/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        // 메인 오브젝트 뒤에, 불투명 오브젝트보다 나중에 렌더
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        Pass
        {
            Name "OUTLINE"
            // 앞면 컬링: 뒷면만 그려서 외곽선처럼 보이게
            Cull Front
            ZWrite On
            ZTest LEqual
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float  _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // View 공간에서 노멀 방향으로 버텍스를 밀어낸 뒤 클립 공간으로 변환
                float3 norm   = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 offset = TransformViewToProjection(norm.xy);

                o.pos    = UnityObjectToClipPos(v.vertex);
                // 클립 공간 z는 플랫폼마다 다르다: DirectX(에디터)는 reversed-Z라 z ≈ near(상수),
                // OpenGL ES/WebGL은 z ≈ 거리(w)라 같은 식이 WebGL에서 수십 배 두꺼워진다.
                // 에디터에서 맞춰 둔 두께를 모든 플랫폼에서 동일하게 내기 위해 near plane 값을 쓴다.
                o.pos.xy += offset * _ProjectionParams.y * _OutlineWidth;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }

    // Built-in 렌더 파이프라인 전용 (WebGL 호환)
    FallBack Off
}
