Shader "Defend/PhaseFive/IndustrialEffect"
{
    Properties
    {
        [HDR] _BaseColor("Color", Color) = (0.1,0.9,1,1)
        _Softness("Billboard radial falloff (0 for lines and meshes)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Softness;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color; return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half4 color = _BaseColor * input.color;
                float radius = length(input.uv * 2 - 1);
                color.a *= lerp(1, 1 - smoothstep(.15, 1, radius), _Softness);
                return color;
            }
            ENDHLSL
        }
    }
}
