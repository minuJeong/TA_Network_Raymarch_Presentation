
import math

import numpy as np
from PIL import Image


def _read(path, args={}):
    with open(path, 'r') as fp:
        context = fp.read()

    for k, v in args.items():
        context = context.replace(f"%{k}", str(v))

    lines = []
    for line in context.splitlines():
        if line.startswith("%include "):
            include_path = line.split("%include ")[1]
            line = _read(include_path)
        lines.append(line)

    return '\n'.join(lines)


def _flatten_array(data):
    data = data * 255.99
    data = data.astype(np.uint8)
    data = data[::-1]
    return data


def _screen_quad(program, context, aspect=1.0):
    vbo = np.array([
        -1.0, -1.0, 0.0,  0.0 * aspect, 0.0,
        +1.0, -1.0, 0.0,  1.0 * aspect, 0.0,
        -1.0, +1.0, 0.0,  0.0 * aspect, 1.0,
        +1.0, +1.0, 0.0,  1.0 * aspect, 1.0,
    ]).astype('f4')
    vbo = [(
        context.buffer(vbo.tobytes()),
        '3f 2f',
        'in_verts', 'in_uvs'
    )]

    ibo = np.array([
        0, 1, 2,
        1, 2, 3
    ]).astype('i4')
    ibo = context.buffer(ibo.tobytes())
    return context.vertex_array(program, vbo, ibo)


def _load_tex(context, path, force_size=None):
    img = Image.open(path)
    if force_size and force_size is tuple and len(force_size) == 2:
        img = img.resize(force_size)
    texture = _image_to_texture(context, img)
    return texture


def _image_to_texture(context, img):
    img = img.convert("RGBA")
    img = img.transpose(Image.FLIP_TOP_BOTTOM)
    data = img.tobytes()
    return context.texture(img.size, 4, data)


def spherified_cube_vertices(context):

    class Vector(np.array):
        def __init__(self, x, y, z, u=0.0, v=0.0):
            super(Vector, self).__init__((x, y, z, u, v))

    verticies = np.array()
    faces = np.array()
    res = 12
    idx = 0
    for i in range(6):
        origin = Vector(0, 0, 0)
        right = Vector(0, 0, 0)
        up = Vector(0, 0, 0)

        for y in range(res):
            for x in range(res):
                p1 = origin + 2.0 * (right * x + up * y) / res
                p2 = p1 * p1

                rx = math.sqrt(1.0 - 0.5 * (p2.y + p2.z) + p2.y * p2.z / 3.0)
                ry = math.sqrt(1.0 - 0.5 * (p2.z + p2.x) + p2.z * p2.x / 3.0)
                rz = math.sqrt(1.0 - 0.5 * (p2.x + p2.y) + p2.x * p2.y / 3.0)
                ru = x / res
                rv = y / res
                v = Vector(rx, ry, rz, ru, rv)

                idx = x + (y * res) + (i * res * res)
                f = np.array([
                    idx + 0, idx + 1, idx + 2,
                    idx + 1, idx + 2, idx + 3
                ])

                np.append(verticies, v)
                np.append(faces, f)

    vbo = verticies.astype('f4')
    vbo = [(
        context.buffer(vbo.tobytes()),
        '3f 2f',
        'in_verts', 'in_uvs'
    )]
