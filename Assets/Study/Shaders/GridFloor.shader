// Dim Tron grid at the real floor. Lines are anti-aliased with fwidth and
// thin out before they get smaller than a pixel (no shimmer), and the whole
// grid fades with distance from the point under the sphere.
Shader "Study/GridFloor"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0.94, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.18
        _CellSize ("Cell size (m)", Float) = 0.5
        _LineWidth ("Line width (m)", Float) = 0.008
        _FadeCenter ("Fade center (world)", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start (m)", Float) = 1.5
        _FadeEnd ("Fade end (m)", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GridFloor"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _CellSize;
                float _LineWidth;
                float4 _FadeCenter;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 coord = i.positionWS.xz / _CellSize;
                float2 distToLine = abs(frac(coord - 0.5) - 0.5) * _CellSize;    // metres to nearest line
                float2 pixel = max(fwidth(coord) * _CellSize, 1e-5);            // metres per pixel
                float2 halfWidth = max(_LineWidth * 0.5, pixel * 0.5);
                float2 lines = 1 - smoothstep(halfWidth, halfWidth + pixel, distToLine);
                // Keep total brightness per pixel constant when lines are thinner than a pixel.
                lines *= saturate(_LineWidth / max(pixel, 1e-5));
                float lineMask = max(lines.x, lines.y);

                float dist = distance(i.positionWS.xz, _FadeCenter.xz);
                float fade = 1 - smoothstep(_FadeStart, _FadeEnd, dist);
                return half4(_Color.rgb, lineMask * fade * _Intensity);
            }
            ENDHLSL
        }
    }
}
