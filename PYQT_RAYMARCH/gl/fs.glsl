#version 460

#include ./gl/hg_sdf.glsl

#define FAR 50.0

in vec2 vs_uv;
out vec4 fs_color;

uniform float u_time;

float world(vec3 p)
{
    float d1 = fSphere(p - vec3(cos(u_time * 4.0) * 1.0 + 2.0, 0.0, 0.0), 2.0);
    
    vec3 tx_box = p - vec3(-1.0, -0.5, 0.0);
    float c = cos(u_time * 2.0);
    float s = sin(u_time * 2.0);
    tx_box.xz = tx_box.xz * mat2(c, -s, s, c);
    float b1 = fBoxCheap(tx_box, vec3(1.5, 2.0, 1.0));

    return fOpUnionRound(d1, b1, 0.5);
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
    vec3 r = normalize(vec3(vs_uv, 0.5));

    float t = raymarch(o, r);
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

        vec3 diffuse = lambert * vec3(1.0, 0.0, 0.0);
        vec3 specular = blinn_phong * vec3(0.5);
        
        vec3 reflectance = diffuse + specular;

        RGB = reflectance;
    }

    fs_color = vec4(RGB, 1.0);
}
