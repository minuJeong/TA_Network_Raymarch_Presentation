
/*
author: minu jeong
*/

#version 430

#define PI 3.141592653589793

#define NEAR 0.05
#define FAR 100.0
#define SURFACE 0.00012
#define STEP 128.0

uniform float u_time;
uniform vec3 u_focus;
uniform vec3 u_campos;
uniform bool u_drawbg;

layout(binding=0) uniform sampler2D u_kjutex;

in vec2 v_uvs;

out vec4 out_color;
out vec2 out_uv;
out float out_time;


float sphere(vec3 p, float radius)
{
    return length(p) - radius;
}

float ellipsoid(vec3 p, vec3 r)
{
    float k0 = length(p / r);
    float k1 = length(p / (r * r));
    return k0 * (k0 - 1.0) / k1;
}

float box(vec3 p, vec3 b)
{
    vec3 d = abs(p) - b;
    vec3 md = max(d, 0.0);
    return length(md) + min(max(d.x, max(d.y, d.z)), 0.0);
}

float capsule(vec3 p, vec3 a, vec3 b, float r)
{
    vec3 pa = p - a;
    vec3 ba = b - a;

    // dot(p->a, a->b)
    float dpa = dot(pa, ba);

    // distance between two points
    float dbb = dot(ba, ba);

    float h = clamp(dpa / dbb, 0.0, 1.0);
    return length(pa - ba * h) - r;
}

float cone(vec3 p, vec2 c)
{
    c = normalize(c);
    float q = length(p.xy);
    return dot(c, vec2(q, p.z));
}

float round_cone(vec3 p, float r1, float r2, float h)
{
    vec2 q = vec2(length(p.xz), p.y);
    
    float b = (r1 - r2) / h;
    float a = sqrt(1.0 - b * b);
    float k = dot(q,vec2(-b,a));
    
    if( k < 0.0 ) return length(q) - r1;
    if( k > a * h ) return length(q - vec2(0.0, h)) - r2;
        
    return dot(q, vec2(a,b)) - r1;
}

float blend(float a, float b, float k)
{
    // return min(a, b);
    float h = clamp(0.5 + 0.5 * (a - b) / k, 0.0, 1.0);
    return mix(a, b, h) - k * h * (1.0 - h);
}

vec3 rotate_x(vec3 p, float r)
{
    float c = cos(r);
    float s = sin(r);
    mat3 rx = mat3(
        1, 0, 0,
        0, c, -s,
        0, s, c
    );

    return rx * p;
}

vec3 rotate_y(vec3 p, float r)
{
    float c = cos(r);
    float s = sin(r);
    mat3 ry = mat3(
        c, 0, s,
        0, 1, 0,
        -s, 0, c
    );

    return ry * p;
}

vec3 rotate_z(vec3 p, float r)
{
    float c = cos(r);
    float s = sin(r);
    mat3 rz = mat3(
        c, -s, 0,
        s, c, 0,
        0, 0, 1
    );

    return rz * p;
}

vec3 rotate(vec3 p, vec3 r)
{
    vec3 c = cos(r);
    vec3 s = sin(r);
    mat3 rx = mat3(
        1, 0, 0,
        0, c.x, -s.x,
        0, s.x, c.s
    );
    mat3 ry = mat3(
        c.y, 0, s.y,
        0, 1, 0,
        -s.y, 0, c.y
    );
    mat3 rz = mat3(
        c.z, -s.z, 0,
        s.z, c.z, 0,
        0, 0, 1
    );
    return rz * ry * rx * p;
}

mat3 rotate_mat(vec3 r)
{
    vec3 c = cos(r);
    vec3 s = sin(r);
    mat3 rx = mat3(
        1, 0, 0,
        0, c.x, -s.x,
        0, s.x, c.s
    );
    mat3 ry = mat3(
        c.y, 0, s.y,
        0, 1, 0,
        -s.y, 0, c.y
    );
    mat3 rz = mat3(
        c.z, -s.z, 0,
        s.z, c.z, 0,
        0, 0, 1
    );
    return rz * ry * rx;
}

float yeon(vec3 p, inout vec3 base_color)
{
    const vec3 deep_grey = vec3(0.05, 0.05, 0.05);
    const vec3 cheek_col = vec3(0.88, 0.45, 0.55);
    const vec3 hair_col = vec3(0.24, 0.66, 0.99);
    const vec3 sushi_col_1 = vec3(0.75, 0.75, 0.75);
    const vec3 sushi_col_2 = vec3(0.88, 0.45, 0.65);
    vec3 face_col = vec3(0.98, 0.77, 0.75);
    vec3 skin_col = vec3(0.35, 0.45, 0.12);

    float anim_spd = 8.0;

    bool is_painted = false;

    // symmetrical
    vec3 _p = p;
    _p.x = abs(p.x);

    // result
    float dist_head;
    float dist_hair;
    float dist;

    vec3 pelvis_p = vec3(0.0, 1.12, 0.0);
    vec3 neck_p = pelvis_p + vec3(0.0, 1.75, 0.0);
    vec3 eye_p = vec3(0.0);
    vec3 cheek_p = vec3(0.0);
    vec3 ear_p = vec3(0.0);
    vec3 mouth_p = vec3(0.0);
    vec3 shoulder_p = pelvis_p + vec3(0.0, 0.9, 0.0);
    vec3 leg_g = pelvis_p + vec3(0.4, -0.02, -0.32);

    float head_r = cos(u_time * anim_spd) * 0.12 - 0.1;
    float body_r = cos(u_time * anim_spd) * 0.12;
    mat3 headm = rotate_mat(vec3(head_r, 0.0, 0.0));
    mat3 bodym = rotate_mat(vec3(body_r, 0.0, 0.0));

    // head
    {
        // main head
        {
            vec3 a = neck_p + vec3(0.0, 0.2, -0.05);
            vec3 b = a + vec3(0.0, 0.35, -0.1);

            eye_p = b + vec3(0.28, -0.26, 0.29);
            cheek_p = b + vec3(0.16, 0.0, 0.0);
            ear_p = b + vec3(0.0, 0.2, 0.0);
            mouth_p = b + vec3(0.0, -0.4, 0.5);

            a = headm * a;
            b = headm * b;
            
            const float r1 = 0.58;
            const float r2 = 0.24;

            float j1 = sphere(_p - a, r1);
            float j2 = sphere(_p - b, r2);

            float j12 = blend(j1, j2, 0.5);
            if (j12 < SURFACE)
            {
                base_color = face_col;
                is_painted = true;
            }

            dist_head = j12;
        }

        // ear
        {
            vec3 a = ear_p + vec3(0.62, -0.55, 0.05);
            vec3 b = a + vec3(0.05, 0.05, -0.05);
            vec3 c = b + vec3(0.05, 0.12, -0.08);

            a = headm * a;
            b = headm * b;
            c = headm * c;

            float r1 = 0.12;
            float r2 = 0.11;

            float j1 = capsule(_p, a, b, r1);
            float j2 = capsule(_p, b, c, r2);

            float j12 = blend(j1, j2, 0.25);
            if (j12 < SURFACE)
            {
                base_color = face_col;
                is_painted = true;
            }
            dist_head = min(dist_head, j12);
        }

        // hair
        {
            vec3 a = neck_p + vec3(0.0, 0.55, -0.15);
            vec3 b = a + vec3(0.0, -0.32, 0.08);
            vec3 d = a + vec3(0.0, -0.5, -0.4);
            vec3 e = d + vec3(0.32, 0.0, 0.0);
            vec3 f = e + vec3(0.4, -0.3, -0.3);

            a = headm * a;
            b = headm * b;
            d = headm * d;
            e = headm * e;
            f = headm * f;

            float r1 = 0.69;
            float r2 = 0.72;
            float r3 = 0.45;
            float r4 = 0.32;

            float j1 = sphere(_p - a, r1);
            float j2 = sphere(_p - b, r2);
            float j3 = capsule(_p, d, e, r3);
            float j4 = sphere(_p - f, r4);

            float j12 = max(j1, -j2);
            float j123 = min(j12, j3);
            float j1234 = min(j123, j4);

            if (j1234 < SURFACE)
            {
                base_color = hair_col;
                is_painted = true;
            }
            dist_hair = j1234;
        }

        // sushi
        {
            vec3 a = neck_p + vec3(0.0, 1.2, 0.1);
            vec3 b = a + vec3(0.08, 0.0, 0.0);
            vec3 c = a + vec3(0.0, 0.08, 0.02);
            vec3 d = b + vec3(0.02, 0.08, 0.02);

            a = headm * a;
            b = headm * b;
            c = headm * c;
            d = headm * d;

            float r1 = 0.2;
            float r2 = 0.19;

            float j1 = capsule(_p, a, b, r1);
            float j2 = capsule(_p, c, d, r2);

            float j12 = min(j1, j2);
            if (j1 < SURFACE)
            {
                base_color = sushi_col_1;
                is_painted = true;
            }

            if (j2 < SURFACE)
            {
                base_color = sushi_col_2;
                is_painted = true;
            }

            dist_hair = min(dist_hair, j12);
        }
    }

    // body
    {
        // base body
        {
            vec3 a = pelvis_p + vec3(0.0, 1.5, 0.0);
            vec3 b = a + vec3(0.0, -0.55, 0.0);
            vec3 c = b + vec3(0.0, -0.65, 0.0);

            a = bodym * a;
            b = bodym * b;
            c = bodym * c;

            const float r1 = 0.21;
            const float r2 = 0.45;
            const float r3 = 0.52;

            float j1;
            float j2;
            float j3;
            float body;

            j1 = sphere(_p - a, r1);
            j2 = sphere(_p - b, r2);
            j3 = sphere(_p - c, r3);

            float j12 = blend(j1, j2, 0.4);
            body = blend(j12, j3, 0.5);
            dist = body;
        }

        // arm
        {
            vec3 a = shoulder_p + vec3(0.48, 0.08, 0.0);
            vec3 b = a + vec3(+0.23, -0.45, 0.12);
            vec3 c = b + vec3(+0.05, -0.23, 0.32);
            vec3 d = c + vec3(0.0, 0.0, 0.0);

            vec3 arm_rot_ax = a;
            float arm_swing = cos(u_time * anim_spd * 4.0) * 0.4;
            if (_p.x > p.x)
            {
                arm_swing = -arm_swing;
            }

            arm_swing = arm_swing + 1.4;
            mat3 arm_m = rotate_mat(vec3(arm_swing, 0.0, 0.0));

            a = bodym * (arm_m * (a - arm_rot_ax) + arm_rot_ax);
            b = bodym * (arm_m * (b - arm_rot_ax) + arm_rot_ax);
            c = bodym * (arm_m * (c - arm_rot_ax) + arm_rot_ax);
            d = bodym * (arm_m * (d - arm_rot_ax) + arm_rot_ax);

            const float r1 = 0.12;
            const float r2 = 0.12;
            const float r3 = 0.14;
            const float r4 = 0.24;

            float j1;
            float j2;
            float j3;
            float j4;

            j1 = capsule(_p, a, b, r1);
            j2 = capsule(_p, b, c, r2);
            j3 = sphere(_p - c, r3);
            j4 = sphere(_p - d, r4);

            if (j4 < SURFACE)
            {
                base_color = face_col;
                is_painted = true;
            }

            dist = min(dist, blend(min(j1, j2), j3, 0.3));
        }
    }

    // feet
    {
        vec3 a = leg_g - vec3(0.0, 0.0, -0.4);
        const vec3 b = a + vec3(0.12, -0.5, 0.24);
        const vec3 c = b + vec3(-0.05, -0.5, -0.32);
        const vec3 d = c + vec3(0.12, 0.0, +0.45);

        a = bodym * a;

        const float r1 = 0.15;
        const float r2 = 0.16;
        const float r3 = 0.16;

        float j1;
        float j2;
        float j3;

        j1 = capsule(_p, a, b, r1);
        j2 = capsule(_p, b, c, r2);
        j3 = capsule(_p, c, d, r3);

        float j12 = min(j1, j2);
        float j123 = min(j12, j3);

        if (j3 < SURFACE)
        {
            base_color = face_col;
            is_painted = true;
        }
        dist = min(dist, j123);
    }

    // combine head;
    dist = min(dist, dist_head);
    dist = min(dist, dist_hair);

    // eyes
    {
        vec3 a = eye_p + vec3(0.1, 0.0, 0.2);
        a = headm * a;
        float r = 0.1;

        float j1 = sphere(_p - a, r);
        if (j1 < SURFACE)
        {
            base_color = deep_grey;
            is_painted = true;
        }
    }

    // cheeks
    {
        vec3 a = cheek_p + vec3(0.3, -0.6, 0.4);
        a = headm * a;
        float r = 0.16;

        float j1 = sphere(_p - a, r);
        if (j1 < SURFACE)
        {
            base_color = cheek_col;
            is_painted = true;
        }
    }

    // mouth
    {
        vec3 a = mouth_p + vec3(0.0, 0.0, 0.12);
        vec3 b = a + vec3(0.08, -0.1, 0.0);
        vec3 c = b + vec3(0.12, -0.05, 0.0);
        a = headm * a;
        b = headm * b;
        c = headm * c;

        float r1 = 0.08;
        float r2 = 0.08;

        float j1 = capsule(_p, a, b, r1);
        float j2 = capsule(_p, b, c, r2);
        float j12 = min(j1, j2);
        if (j12 < SURFACE)
        {
            base_color = deep_grey;
            is_painted = true;
        }
    }

    if (!is_painted && dist < SURFACE)
    {
        base_color = skin_col;
    }

    return dist;
}

float suryeong(vec3 p, inout vec3 base_color)
{
    bool is_painted = false;
    vec3 default_col = base_color;
    vec3 canvas_col = vec3(0.88, 0.85, 0.85);

    float dist = FAR;

    vec3 _cp = vec3(-1.5, 3.0, -1.7);

    // canvas
    {
        vec3 t1 = vec3(0.6, 0.8, 0.05);

        vec3 a = _cp;
        vec3 b = _cp + vec3(-0.62, -0.8, 0.0);
        vec3 c = _cp + vec3(+0.62, -0.8, 0.0);
        vec3 d = _cp + vec3(-0.62, 0.8, 0.0);
        vec3 e = _cp + vec3(+0.62, 0.8, 0.0);

        float r1 = 0.08;
        float r2 = 0.08;

        float j1 = box(p - a, t1);
        float j2 = capsule(p, b, c, r1);
        float j3 = capsule(p, d, e, r2);

        float j23 = min(j2, j3);

        dist = min(j1, j23);
        if (j1 < SURFACE)
        {
            vec2 uv = -p.xy * 0.65 + vec2(-0.4, -0.5);
            vec3 kjutex_col = texture(u_kjutex, uv).xyz;
            base_color = kjutex_col;
            is_painted = true;
        }

        if (j23 < SURFACE)
        {
            base_color = canvas_col;
            is_painted = true;
        }
    }

    if (!is_painted)
    {
        base_color = default_col;
    }

    return dist;
}

float keyboard(vec3 p, inout vec3 base_color)
{
    const vec3 keyboard_col = vec3(0.55);

    vec3 a = vec3(0.6, 1.6, 1.5);
    vec3 b = vec3(0.8, 0.05, 0.4);

    vec3 _p = rotate_y(p, 0.3);
    _p = rotate_x(p, -0.2);
    float j1 = box(_p - a, b);

    if (j1 < SURFACE)
    {
        base_color = keyboard_col;
    }
    return j1;
}

float mouse(vec3 p, inout vec3 base_color)
{
    const vec3 mouse_col = vec3(0.55);

    vec3 a = vec3(-0.59, 1.86, 1.12);
    vec3 b = vec3(-0.59, 1.86, 1.07);
    float r1 = 0.15;

    float j1 = capsule(p, a, b, r1);
    if (j1 < SURFACE)
    {
        base_color = mouse_col;
    }

    return j1;
}

float world(vec3 p, inout vec3 base_color)
{
    float _yeon = yeon(p, base_color);
    float d = _yeon;

    if (u_drawbg)
    {
        float _suryeong = suryeong(p, base_color);
        float _keyboard = keyboard(p, base_color);
        float _mouse = mouse(p, base_color);
        d = min(d, _suryeong);
        d = min(d, _keyboard);
        d = min(d, _mouse);
        float floor = abs(0.0 - p.y);
        if (floor < SURFACE)
        {
            base_color = vec3(0.25, 0.25, 0.25);
        }
        d = min(d, floor);
    }
    return d;
}

float raymarch(vec3 o, vec3 r, inout vec3 base_color)
{
    float travel = NEAR;
    vec3 p = vec3(0.0);

    float d;
    for (float i = 0.0; i < 64.0; i++)
    {
        p = o + r * travel;
        d = world(p, base_color);
        travel += d;
        if (d < SURFACE)
        {
            break;
        }

        if (travel > FAR)
        {
            return FAR;
        }
    }

    return travel;
}

vec3 normal(vec3 p)
{
    vec2 e = vec2(SURFACE, 0.0);
    vec3 _c;
    return normalize(vec3(
        world(p + e.xyy, _c) - world(p - e.xyy, _c),
        world(p + e.yxy, _c) - world(p - e.yxy, _c),
        world(p + e.yyx, _c) - world(p - e.yyx, _c)
    ));
}

float ggx(vec3 vct_dots, float rough, float min_fresnel)
{
    float NdL = vct_dots.x;
    float LdH = vct_dots.y;
    float NdH = vct_dots.z;

    float a = pow(rough, 4.0);
    float denom = 1.0 + NdH * NdH * (a - 1.0);

    float rrh2 = pow(rough * rough * 0.5, 2.0);

    float distribution = a / (PI * denom * denom);
    float fresnel = 0.7 + 0.3 * pow(1.0 - LdH, 5.0);

    float rcp_rrh2 = 1.0 / (1.0 - rrh2) + rrh2;
    float _ggx = distribution * NdL * fresnel * rcp_rrh2;
    return _ggx;
}

float soft_shadow(vec3 o, vec3 r)
{
    float k = 8.4;

    float t = 0.02;
    float res = 1.0;
    float ph = 1e20;

    vec3 _c;
    for (float i = 0.0; i < 32.0; i++)
    {
        float h = world(o + r * t, _c);
        if (h < SURFACE)
        {
            return 0.0;
        }

        float y = h * h / (2.0 * ph);
        float d = sqrt(h * h - y * y);
        res = min(res, k * d / max(0.0, t - y));
        ph = h;
        t += h;
    }
    return res;
}

vec3 aces_film_tonemap(vec3 hdr)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;

    float e = 0.14;

    vec3 x = hdr * (a * hdr + b);
    vec3 y = hdr * (c * hdr + d) + e;

    return clamp(x / y, 0.0, 1.0);
}

mat3 lookat(vec3 o, vec3 t, float roll)
{
    vec3 row_0 = vec3(sin(0.0), cos(0.0), 0.0);
    vec3 row_1 = normalize(t - o);
    vec3 row_2 = normalize(cross(row_1, row_0));
    vec3 row_3 = normalize(cross(row_2, row_1));
    return mat3(row_2, row_3, row_1);
}

void main()
{
    vec3 light = vec3(15.0, 45.0, 15.0);

    vec3 L = normalize(light);

    vec2 uv = v_uvs - 0.5;
    vec3 origin = u_campos;

    // split drawing
    // if (uv.x > 0.0)
    // {
    //     origin = rotate_y(u_campos, PI);
    // }
    // uv.x *= 2.0;
    // uv.x = uv.x - floor(uv.x / 2.0);
    // uv.x -= 0.5;

    vec3 r = vec3(uv, 1.0);
    r = normalize(r);

    mat3 _look = lookat(origin, u_focus, 0.0);
    r = _look * r;

    vec3 base_color = vec3(0.2, 0.3, 0.7);
    float d = raymarch(origin, r, base_color);

    vec3 hdr = base_color;
    float alpha = 0.0;
    if (d < FAR)
    {
        alpha = 1.0;

        vec3 P = origin + r * d;
        vec3 N = normal(P);
        vec3 V = normalize(-origin);
        vec3 H = normalize(L - V);

        float LdH = max(0.0, dot(L, H));
        float NdH = max(0.0, dot(N, H));
        float NdL = max(0.0, dot(N, L));
        float F = max(0.0, dot(N, V));

        float spec = ggx(vec3(LdH, NdH, NdL), 0.2, 0.7);
        hdr = vec3(spec) + base_color;

        vec3 shadow_lp = normalize(light - P);
        float d_shadow = soft_shadow(P, shadow_lp);

        float shadow_intensity = 0.50;
        hdr *= mix(1.0, d_shadow, shadow_intensity);
    }

    // vinette
    float l = length(uv);
    l = 1.0 - pow(l * 1.25, 2.34);
    l = clamp(l, 0.0, 1.0);
    hdr *= l;

    vec3 ldr = aces_film_tonemap(hdr);

    out_color.xyz = ldr;
    out_color.w = alpha;

    out_uv = v_uvs;
    out_time = u_time;
}
