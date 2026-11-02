// Water tiles: the sprite gives the shape, shore and colours; this adds moving ripples.
// Ripples are worked out from world position, so they flow across tiles without repeating
// per tile, and they animate every frame instead of stepping through a few drawings.
// Only blue (water) pixels get ripples; sand and foam are left alone, apart from the foam
// gently pulsing.
Shader "Age of Stickman/Water Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CrestColor ("Ripple colour", Color) = (0.78, 0.9, 1, 1)
        _CrestStrength ("Ripple strength", Range(0, 1)) = 0.7
        _Speed ("Speed", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _CrestColor;
            float _CrestStrength;
            float _Speed;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Soft-edged band: 1 where x is above 'edge', fading over the pixel footprint.
            float Band(float x, float edge)
            {
                float w = max(fwidth(x), 0.0001) * 1.5;
                return smoothstep(edge - w, edge + w, x);
            }

            float2 Hash2(float2 c)
            {
                return frac(sin(float2(dot(c, float2(127.1, 311.7)), dot(c, float2(269.5, 183.3)))) * 43758.5453);
            }

            // Expanding ring pairs. The world is split into cells; each cell drops a ripple at a
            // random spot every few seconds, then rests. Rings are squashed vertically to match
            // the top-down view. Neighbouring cells are checked so rings can cross cell borders.
            float Ripples(float2 p, float t)
            {
                const float cellSize = 2.2;
                float2 baseCell = floor(p / cellSize);
                // Pixel size from the smooth world position (the per-cell maths jumps at cell borders).
                float pixel = length(fwidth(p)) * 1.3;
                float sum = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = baseCell + float2(x, y);
                        float2 h = Hash2(cell);
                        float period = 3.0 + h.y * 3.0;
                        float clock = t / period + h.x * 7.13;
                        float life = frac(clock) / 0.7;          // grows for 70% of the cycle, then rests
                        if (life > 1) continue;
                        float2 spot = Hash2(cell + floor(clock) * 1.37 + 5.1);
                        float2 center = (cell + 0.15 + 0.7 * spot) * cellSize;
                        float2 d = p - center;
                        d.y *= 1.8;
                        float r = length(d);
                        float radius = 0.08 + life * 0.95;
                        float w = 0.03 + pixel;
                        float outer = 1 - smoothstep(0, w, abs(r - radius));
                        float inner = 1 - smoothstep(0, w, abs(r - radius * 0.62));
                        float fade = pow(1 - life, 1.4) * smoothstep(0, 0.1, life);
                        sum += (outer + inner * 0.55) * fade;
                    }
                }
                return saturate(sum);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float t = _Time.y * _Speed;
                // Effects move in whole art pixels (32 a tile), so the water stays pixel art.
                float2 p = (floor(i.world * 32) + 0.5) / 32;

                // How much this pixel is open water (blue) rather than sand or foam.
                float wet = saturate((c.b - c.r - 0.25) * 5);

                // Slow swell: gentle light and dark patches drifting across.
                float swell = sin(p.x * 0.73 + p.y * 0.41 + t * 0.6) * 0.5 + sin(-p.x * 0.37 + p.y * 0.89 + t * 0.45) * 0.5;
                c.rgb *= 1 + swell * 0.035 * wet;

                // Ripples: rings spreading out from random spots, fading as they grow.
                float ripple = Ripples(p, t);
                c.rgb = lerp(c.rgb, _CrestColor.rgb, ripple * _CrestStrength * wet);

                // Foam lapping: whitish pixels fade in and out along the shore.
                float foam = saturate((min(c.r, min(c.g, c.b)) - 0.72) * 6);
                float lap = 0.5 + 0.5 * sin(t * 1.8 + p.x * 0.9 + p.y * 1.3);
                c.rgb = lerp(c.rgb, float3(0.42, 0.7, 0.88), foam * lap * 0.55);

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
