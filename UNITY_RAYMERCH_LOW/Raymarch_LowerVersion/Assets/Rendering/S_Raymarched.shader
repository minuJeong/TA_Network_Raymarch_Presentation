Shader "Unlit/S_Raymarched"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

			#define FAR 50.0

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }


			float vmax(float2 v)
			{
				return max(v.x, v.y);
			}

			float vmax(float3 v)
			{
				return max(max(v.x, v.y), v.z);
			}

			float vmax(float4 v)
			{
				return max(max(v.x, v.y), max(v.z, v.w));
			}

			float vmin(float2 v) {
				return min(v.x, v.y);
			}

			float vmin(float3 v) {
				return min(min(v.x, v.y), v.z);
			}

			float vmin(float4 v) {
				return min(min(v.x, v.y), min(v.z, v.w));
			}

			float sphere(float3 p, float r)
			{
				return length(p) - r;
			}

			float plane(float3 p, float3 n, float distanceFromOrigin)
			{
				return dot(p, n) + distanceFromOrigin;
			}
			
			float box_cheap(float3 p, float3 b)
			{
				return vmax(abs(p) - b);
			}

			float box(float3 p, float3 b)
			{
				float3 d = abs(p) - b;
				return length(max(d, float3(0.0, 0.0, 0.0))) + vmax(min(d, float3(0.0, 0.0, 0.0)));
			}

			float box2_cheap(float2 p, float2 b)
			{
				return vmax(abs(p) - b);
			}

			float box2(float2 p, float2 b)
			{
				float2 d = abs(p) - b;
				return length(max(d, float2(0.0, 0.0))) + vmax(min(d, float2(0.0, 0.0)));
			}

			// Capsule: A Cylinder with round caps on both sides
			float capsule(float3 p, float r, float c) {
				return lerp(length(p.xz) - r, length(float3(p.x, abs(p.y) - c, p.z)) - r, step(c, abs(p.y)));
			}

			// Distance to line segment between <a> and <b>, used for fCapsule() version 2below
			float lines_segment(float3 p, float3 a, float3 b) {
				float3 ab = b - a;
				float t = saturate(dot(p - a, ab) / dot(ab, ab));
				return length((ab * t + a) - p);
			}

			float capsule(float3 p, float3 a, float3 b, float r) {
				return lines_segment(p, a, b) - r;
			}

			float torus(float3 p, float smallRadius, float largeRadius) {
				return length(float2(length(p.xz) - largeRadius, p.y)) - smallRadius;
			}

			float intersection_chamfer(float a, float b, float r)
			{
				return max(max(a, b), (a + r + b) * sqrt(0.5));
			}

			float difference_chamfer(float a, float b, float r)
			{
				return intersection_chamfer(a, -b, r);
			}

			float union_round(float a, float b, float r)
			{
				float2 u = max(float2(r - a, r - b), float2(0.0, 0.0));
				return max(r, min(a, b)) - length(u);
			}

			float union_chamfer(float a, float b, float r)
			{
				return min(min(a, b), (a - r + b) * sqrt(0.5));
			}

			float intersection_round(float a, float b, float r)
			{
				float2 u = max(float2(r + a, r + b), float2(0.0, 0.0));
				return min(-r, max(a, b)) + length(u);
			}

			float difference_round(float a, float b, float r)
			{
				return intersection_round(a, -b, r);
			}

			float world(float3 pos)
			{
				float3 tx_sphere = float3(cos(_Time.z) * 2.0, 0.5, 2.0);
				float radius_sphere = 2.0;

				float3 tx_capsule_1 = float3(-1.0, -1.0, 0.0);
				float3 tx_capsule_2 = float3(-1.0, +2.0, 0.5);
				float radius_capsule = 1.2;

				float dist_sphere = sphere(pos - tx_sphere, radius_sphere);

				float3 pos_capsule = pos;
				float c = cos(_Time.z * 4.0);
				float s = sin(_Time.z * 4.0);
				pos_capsule.xy = mul(pos_capsule.xy, float2x2(c, -s, s, c));
				float dist_capsule = capsule(pos_capsule, tx_capsule_1, tx_capsule_2, radius_capsule);

				float dist = union_round(dist_sphere, dist_capsule, 0.5);

				return dist;
			}

			float raymarch(float3 origin, float3 ray)
			{
				float travel;
				float distance;
				float3 pos;

				for (int i = 0; i < 128; i++)
				{
					pos = origin + ray * travel;
					distance = world(pos);
					if (distance < 0.001 || travel > FAR)
					{
						break;
					}
					travel += distance;
				}
				return travel;
			}

			float3 normal_at(float3 p)
			{
				float2 e = float2(0.002, 0.0);
				return normalize(float3(
					world(p + e.xyy) - world(p - e.xyy),
					world(p + e.yxy) - world(p - e.yxy),
					world(p + e.yyx) - world(p - e.yyx)
				));
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 origin = float3(0.0, 0.0, -5.0);
				float3 ray = normalize(float3(i.uv * 2.0 - 1.0, 1.0));

				fixed3 RGB = fixed3(i.uv, 0.5);
				float alpha = 0.0;
				float travel = raymarch(origin, ray);
				if (travel < FAR)
				{
					alpha = 1.0;

					float3 p = origin + ray * travel;
					float3 normal = normal_at(p);
					float3 light = float3(-2.0, 5.0, -10.0) - p;
					light = normalize(light);

					float lambert = dot(normal, light);

					RGB.xyz = lambert.xxx;
				}

                return fixed4(RGB, 1.0);
            }
            ENDCG
        }
    }
}
