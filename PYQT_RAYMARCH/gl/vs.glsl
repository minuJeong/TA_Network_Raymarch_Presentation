#version 460

in vec2 in_pos;
out vec2 vs_uv;

uniform float u_width;
uniform float u_height;

void main()
{
    float aspect = u_width / u_height;
    gl_Position = vec4(in_pos, 0.0, 1.0);
    vs_uv = in_pos;
    vs_uv.x *= aspect;
}
