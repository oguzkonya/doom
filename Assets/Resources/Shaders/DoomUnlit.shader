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

            // Globals driven by GameManager / PlayerWeapons
            float _DoomExtraLight;
            float _DoomFalloff;
            float _DoomMinLight;

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Scroll.xy * _Time.y;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.sector = v.color.r * _Light;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);
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
