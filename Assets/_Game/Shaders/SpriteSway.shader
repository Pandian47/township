// Sprite that sways in the wind: the top of the sprite moves sideways, the bottom (where plants meet the ground) stays.
// Used for crops. Each field gets its own phase from its world position, so a farm doesn't move in lockstep.
Shader "TownBuilder/SpriteSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Amplitude ("Sway (world units at the top)", Float) = 0.035
        _Speed ("Speed", Float) = 1.7
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Amplitude;
            float _Speed;

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Sprites may be batched (vertices already in world space); TransformObjectToWorld is correct either way.
                float3 world = TransformObjectToWorld(v.positionOS);
                // Each sprite is its own texture (no atlas), so uv.y runs 0 at the bottom to 1 at the top.
                float height = pow(saturate(v.uv.y), 1.5);
                world.x += height * _Amplitude * sin(_Time.y * _Speed + world.x * 1.3 + world.y * 2.1);
                o.positionCS = TransformWorldToHClip(world);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            }
            ENDHLSL
        }
    }
}
