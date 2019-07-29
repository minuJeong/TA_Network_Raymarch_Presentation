#version 460

#include ./gl/hg_sdf.glsl

#define FAR 50.0

in vec2 vs_uv;
out vec4 fs_color;

uniform float u_time;

float world(vec3 p)
{
    float d1 = fSphere(p - vec3(cos(u_time * 4.0) * 1.0 + 2.0, -0.5, 0.0), 2.0);
    float d2 = fSphere(p - vec3(cos(u_time * 2.0) * 2.0 + 1.0, +0.5, 0.0), 2.0);
    float sphere_dist = fOpUnionRound(d1, d2, 0.5);
    
    vec3 tx_box_1 = p - vec3(-1.0, -1.5, 0.0);
    float c = cos(u_time * 2.0);
    float s = sin(u_time * 2.0);
    tx_box_1.xz = tx_box_1.xz * mat2(c, -s, s, c);
    vec3 box_extent_1 = vec3(1.5, 1.0, 1.0);
    float b1 = fBox(tx_box_1, box_extent_1);

    vec3 tx_box_2 = p - vec3(-1.2, 1.0, -0.5);
    c = cos(u_time * 4.0);
    s = sin(u_time * 4.0);
    tx_box_2.xy = tx_box_2.xy * mat2(-c, s, -s, -c);
    vec3 box_extent_2 = vec3(2.5, 1.2, 0.75);
    float b2 = fBox(tx_box_2, box_extent_2);

    float box_dist = fOpUnionRound(b1, b2, 0.85);

    return fOpUnionRound(sphere_dist, box_dist, 0.85);
}

float raymarch(vec3 o, vec3 r)
{
    float t;
    float d;
    vec3 p;
    for (int i = 0; i < 64; i++)
    {
        p = o + r * t;
        d = world(p);
        if (d < 0.002)
        {
            break;
        }
        t += d;
    }
    return t;
}

vec3 normal_at(vec3 p)
{
    vec2 e = vec2(0.002, 0.0);
    return normalize(vec3(
        world(p + e.xyy) - world(p - e.xyy),
        world(p + e.yxy) - world(p - e.yxy),
        world(p + e.yyx) - world(p - e.yyx)
    ));
}

void main()
{
    vec3 RGB;

    vec3 o = vec3(0.0, 0.0, -5.0);
    vec3 r = normalize(vec3(vs_uv, 1.0));

    float t = raymarch(o, r);

    float extiction = 0.0;
    if (t < FAR)
    {
        vec3 P = o + r * t;
        vec3 N = normal_at(P);
        vec3 L = vec3(-2.0, 5.0, -10.0) - P;
        L = normalize(L);
        vec3 H = normalize(-r + L);
        float lambert = clamp(dot(N, L), 0.0, 1.0);
        float blinn_phong = clamp(dot(N, H), 0.0, 1.0);
        blinn_phong = pow(blinn_phong, 12.0);

        vec3 diffuse = lambert * vec3(0.7, 0.2, 0.2);
        vec3 specular = blinn_phong * vec3(0.5);
        
        vec3 reflectance = diffuse + specular;

        for (int i = 0; i < 48; i++)
        {
            vec3 P2 = P + (r * i * 0.05);
            if (world(P2) < 0.0)
            {
                extiction += 0.02;
            }
        }


        RGB = reflectance * extiction;

    }

    fs_color = vec4(RGB, 1.0);
}
