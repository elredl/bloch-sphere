// Glow for tube meshes without full-screen bloom (too expensive on Quest).
// Additive, unlit. "Falloff" fades the tube toward its silhouette, so a wide
// tube drawn around a thin one reads as a soft halo. UV.x carries distance
// along the line in metres, used for dashes; vertex alpha fades trails.
Shader "Study/NeonLine"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _CoreWhite ("Core whiteness (center of tube toward white)", Range(0, 1)) = 0
        _Falloff ("Edge falloff (0 = flat)", Float) = 0
        _DashLength ("Dash length (m, 0 = solid)", Float) = 0
        _GapLength ("Gap length (m)", Float) = 0
        _Dim ("Dim", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "NeonLine"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _CoreWhite;
                float _Falloff;
                float _DashLength;
                float _GapLength;
                float _Dim;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(positionWS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float facing = saturate(abs(dot(normalize(i.normalWS), normalize(i.viewDirWS))));
                float profile = _Falloff > 0 ? pow(facing, _Falloff) : 1;

                float dash = 1;
                if (_DashLength > 0)
                {
                    float period = _DashLength + _GapLength;
                    float p = i.uv.x - period * floor(i.uv.x / period);
                    float aa = max(fwidth(i.uv.x), 1e-5);
                    dash = smoothstep(0, aa, p) * (1 - smoothstep(_DashLength - aa, _DashLength, p));
                }

                float3 color = lerp(_Color.rgb, float3(1, 1, 1), _CoreWhite * facing);
                return half4(color * (_Intensity * profile * dash * _Dim * i.color.a), 0);
            }
            ENDHLSL
        }
    }
}
