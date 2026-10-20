// Unlit shader with Doom-style "diminishing light": brightness comes from the sector light level
// (vertex color red channel for level geometry, _Light for sprites) minus a distance falloff,
// quantized into bands. Lives in Resources so it is always included in builds.
Shader "Doom/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Light ("Light Level", Float) = 1
        _Fullbright ("Fullbright", Float) = 0
        _Cutoff ("Alpha Cutoff", Float) = 0.5
        _Tint ("Tint", Color) = (1,1,1,1)
        _Scroll ("UV Scroll", Vector) = (0,0,0,0)
        _Fuzz ("Fuzz (spectre / invisibility)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Light;
            float _Fullbright;
            float _Cutoff;
            float4 _Tint;
            float4 _Scroll;
            float _Fuzz;

            // Globals driven by GameManager / PlayerWeapons
            float _DoomExtraLight;
            float _DoomFalloff;
            float _DoomMinLight;
            float _DoomInvuln;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float sector : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Scroll.xy * _Time.y;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.sector = v.color.r * _Light;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);

                // Spectre-style fuzz: a flickering dark speckle instead of the sprite's colors.
                if (_Fuzz > 0.5)
                {
                    float2 sp = floor(i.screenPos.xy / i.screenPos.w * _ScreenParams.xy * 0.5);
                    float n = frac(sin(dot(sp, float2(12.9898, 78.233)) + floor(_Time.y * 15.0)) * 43758.5453);
                    clip(n - 0.45);
                    return fixed4(0.03, 0.03, 0.03, 1);
                }

                // Invulnerability: inverted greyscale, full bright.
                if (_DoomInvuln > 0.5)
                {
                    float lum = 1.0 - dot(c.rgb, float3(0.3, 0.59, 0.11));
                    return fixed4(lum, lum, lum, 1);
                }
                float d = distance(i.worldPos, _WorldSpaceCameraPos);
                float l = i.sector - d * _DoomFalloff + _DoomExtraLight;
                l = floor(saturate(l) * 24.0) / 24.0;
                l = max(l, _DoomMinLight);
                l = max(l, _Fullbright);
                c.rgb *= l * _Tint.rgb;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
