// Sky cylinder drawn behind everything (no depth write), following the camera position only,
// so it scrolls with yaw like the original sky.
Shader "Doom/Sky"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _DoomInvuln;
            float4 _MainTex_ST;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_DoomInvuln > 0.5)
                {
                    float lum = 1.0 - dot(c.rgb, float3(0.3, 0.59, 0.11));
                    c = fixed4(lum, lum, lum, 1);
                }
                return c;
            }
            ENDCG
        }
    }
}
