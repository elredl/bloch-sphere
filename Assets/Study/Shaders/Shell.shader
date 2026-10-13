// Faint translucent sphere shell: nearly invisible face-on, a little brighter
// at the rim, so the sphere reads as a solid volume without hiding the vector.
Shader "Study/Shell"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0.94, 1, 1)
        _BaseAlpha ("Base alpha", Range(0, 1)) = 0.03
        _RimAlpha ("Rim alpha", Range(0, 1)) = 0.12
        _RimPower ("Rim power", Float) = 3
        _Dim ("Dim", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Shell"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _BaseAlpha;
                float _RimAlpha;
                float _RimPower;
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
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.normalWS), normalize(i.viewDirWS)));
                float alpha = (_BaseAlpha + _RimAlpha * pow(1 - facing, _RimPower)) * _Dim;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
