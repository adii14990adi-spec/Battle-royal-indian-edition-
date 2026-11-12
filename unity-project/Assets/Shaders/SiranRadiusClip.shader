Shader "BattleHorizons/Radius Clipped Lit"
{
    // Shared URP surface shader for Siran Island ground, roads and water.
    //
    //  * Terrain/road vertex colours are authored in sRGB (the island palette is
    //    baked from display colours), so they are converted to linear here.
    //    Unity does not colour-correct raw vertex data in a Linear-space project,
    //    which is what made the greybox look washed out / white.
    //  * Smoothness drives the only specular term. Ground materials keep
    //    smoothness 0, which removes the highlight completely (no ice / mirror
    //    glare). Water uses a low value for a soft sheen.
    //  * `_StreamClipMode` splits the two terrain LODs so they never overlap:
    //      0 = draw everywhere (sea, lake, river),
    //      1 = draw only inside the 50 m detail disk (streamed 10 m meshes, roads),
    //      2 = draw only outside it (20 m full-island shell).
    //    Both are cut by the same circle, so there is no z-fighting and no LOD
    //    interpenetration where they meet.
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 1)) = 0
        _UseVertexColor ("Multiply Vertex Color (sRGB)", Float) = 0
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _StreamClipMode ("Streaming Clip (0 off, 1 inside disk, 2 outside disk)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _UseVertexColor;
                half _Metallic;
                half _Smoothness;
                half _StreamClipMode;
            CBUFFER_END

            // Set globally once per frame by SiranChunkStreamer. Keeping the
            // focus outside the material buffer lets all world materials share
            // the same shader and SRP-batcher layout.
            float4 _BHStreamFocusXZ;
            float _BHStreamRadius;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 baseUv : TEXCOORD2;
                float2 bumpUv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // sRGB -> linear for raw vertex colours (Unity leaves them untouched).
            half3 SrgbToLinear(half3 c)
            {
                half3 low = c / 12.92h;
                half3 high = pow(saturate((c + 0.055h) / 1.055h), 2.4h);
                return lerp(low, high, step(0.04045h, c));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color;
                float2 fallbackUv = positionInputs.positionWS.xz * 0.02;
                float2 rawUv = (abs(input.uv.x) + abs(input.uv.y)) > 0.0001 ? input.uv : fallbackUv;
                output.baseUv = TRANSFORM_TEX(rawUv, _BaseMap);
                output.bumpUv = TRANSFORM_TEX(rawUv, _BumpMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (_StreamClipMode > 0.5h)
                {
                    float activeRadius = _BHStreamRadius > 0.001 ? _BHStreamRadius : 50.0;
                    float2 fromFocus = input.positionWS.xz - _BHStreamFocusXZ.xy;
                    float distanceSq = dot(fromFocus, fromFocus);
                    float radiusSq = activeRadius * activeRadius;
                    if (_StreamClipMode < 1.5h)
                    {
                        clip(radiusSq - distanceSq);   // detail disk: inside only
                    }
                    else
                    {
                        clip(distanceSq - radiusSq);   // island shell: outside only
                    }
                }

                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUv);
                half3 albedo = texel.rgb * _BaseColor.rgb;
                if (_UseVertexColor > 0.5h)
                {
                    albedo *= SrgbToLinear(input.color.rgb);
                }

                half3 normalWS = SafeNormalize(input.normalWS);
                if (_BumpScale > 0.001h)
                {
                    half4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.bumpUv);
                    half3 normalTS = packedNormal.xyz * 2.0h - 1.0h;
                    normalTS.xy *= _BumpScale;
                    normalTS.z = sqrt(saturate(1.0h - dot(normalTS.xy, normalTS.xy)));

                    half3 tangentWS = SafeNormalize(cross(normalWS, half3(0.0h, 0.0h, 1.0h)));
                    half3 bitangentWS = SafeNormalize(cross(normalWS, tangentWS));
                    normalWS = SafeNormalize(tangentWS * normalTS.x + bitangentWS * normalTS.y + normalWS * normalTS.z);
                }

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 direct = mainLight.color * ndotl;
                half3 litColor = albedo * (ambient + direct);

                // Matte by default: with _Smoothness = 0 this term is exactly zero,
                // so a sun-lit ground plane cannot blow out to white.
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specPower = exp2(2.0h + _Smoothness * 8.0h);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specPower) * _Smoothness;
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, _Metallic);
                litColor += f0 * mainLight.color * specular;

                return half4(litColor, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
