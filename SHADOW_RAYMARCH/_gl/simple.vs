
#version 430

in vec3 in_verts;
in vec2 in_uvs;

out vec2 v_uvs;

void main()
{
    v_uvs = in_uvs;
    gl_Position = vec4(in_verts, 1.0);
}
