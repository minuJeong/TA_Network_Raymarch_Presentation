
import os
import time
import math
from functools import partial

import numpy as np
import moderngl as mg
import imageio as ii

from PIL import Image
from PIL.ImageQt import ImageQt

from PyQt5 import QtWidgets
from PyQt5.QtGui import QPixmap
from PyQt5.QtCore import Qt
from PyQt5.QtCore import QThread
from PyQt5.QtCore import pyqtSignal

from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

from _common import _read
from _common import _screen_quad
from _common import _flatten_array


def _rotate_around(n_row=9, distance=10):
    half = 0.5 / n_row
    pi = math.pi
    for i in range(n_row * n_row):
        u = i % n_row
        v = i // n_row

        ur = u / n_row + half
        vr = v / n_row + half

        yr = abs(0.5 - vr) * 2.0
        xzr = math.cos(math.atan2(yr, 1.0))
        ra = 2.0 * -pi * ur

        x = math.cos(ra) * distance * xzr
        y = yr * distance
        z = math.sin(ra) * distance * xzr
        yield (x, y, z), (u, v)


def _imposter_gen(res, vs, fs, n_row=9, dist=10):
    context = mg.create_standalone_context()
    vs = _read(vs)
    fs = _read(fs)
    program = context.program(vertex_shader=vs, fragment_shader=fs)
    vao = _screen_quad(program, context)

    u_campos = program['u_campos']
    if "u_drawbg" in program:
        program["u_drawbg"].value = False

    atlas = Image.new("RGBA", (res, res))
    winw, winh = int(res / n_row), int(res / n_row)
    window_tex = context.texture((winw, winh), 4, dtype="f4")
    frame = context.framebuffer([window_tex])
    frame.use()

    for pos, uv in _rotate_around(n_row, dist):
        u_campos.value = pos
        vao.render()

        u = uv[0] * winw
        v = uv[1] * winh

        data = np.frombuffer(window_tex.read(), dtype="f4")
        data = data.reshape((winw, winh, 4))
        data = _flatten_array(data)
        img = Image.fromarray(data)

        atlas.paste(img, (u, v))

    return atlas


def _imposter_gen_buffers(res, vs, fs, n_row=9, dist=10):
    context = mg.create_standalone_context()
    vs = _read(vs)
    fs = _read(fs)
    program = context.program(vertex_shader=vs, fragment_shader=fs)
    vao = _screen_quad(program, context)

    u_campos = program['u_campos']
    if "u_drawbg" in program:
        program["u_drawbg"].value = True
        # program["u_drawbg"].value = False

    atlas_albedo = Image.new("RGBA", (res, res))
    atlas_normal = Image.new("RGBA", (res, res))
    winw, winh = int(res / n_row), int(res / n_row)

    albedo_tex = context.texture((winw, winh), 4, dtype="f4")
    normal_tex = context.texture((winw, winh), 4, dtype="f4")

    frame = context.framebuffer([albedo_tex, normal_tex])
    frame.use()

    for pos, uv in _rotate_around(n_row, dist):
        u_campos.value = pos
        vao.render()

        u = uv[0] * winw
        v = uv[1] * winh

        data = np.frombuffer(albedo_tex.read(), dtype="f4")
        data = data.reshape((winw, winh, 4))
        data = _flatten_array(data)
        img_albedo = Image.fromarray(data)

        data = np.frombuffer(normal_tex.read(), dtype="f4")
        data = data.reshape((winw, winh, 4))
        data = _flatten_array(data)
        img_normal = Image.fromarray(data)

        atlas_albedo.paste(img_albedo, (u, v))
        atlas_normal.paste(img_normal, (u, v))

    return atlas_albedo, atlas_normal


def _screenspace_generation(
            width, height,
            vspath, fspath,
            start_time=0.0, end_time=1.0, frames=1,
            **uniforms
        ):
    vs = _read(vspath)
    fs = _read(fspath)

    context = mg.create_standalone_context()
    program = context.program(vertex_shader=vs, fragment_shader=fs)
    vao = _screen_quad(program, context)

    for k, v in uniforms.items():
        if k not in program:
            continue

        program[k].value = v

    test_texture = context.texture((width, height), 4)
    test_texture.use(0)

    frame_tex = context.texture((width, height), 4, dtype='f4')
    frame = context.framebuffer([frame_tex])
    frame.use()

    u_time = {"value": 0.0}
    if "u_time" in program:
        u_time = program["u_time"]

    span = end_time - start_time
    step = span / max(float(frames), 0.0)
    for t in range(int(frames)):
        u_time.value = start_time + step * t

        vao.render()
        result_bytes = frame_tex.read()

        data = np.frombuffer(result_bytes, dtype='f4')
        data = data.reshape((height, width, 4))
        yield _flatten_array(data)


def _compute_driven_generation(width, height, cs_path):
    x, y, z = 1024, 1, 1
    cs_args = {
        'X': x,
        'Y': y,
        'Z': z,
        'WIDTH': width,
        'HEIGHT': height,
    }

    cs = _read(cs_path, cs_args)

    context = mg.create_standalone_context()
    compute_shader = context.compute_shader(cs)

    in_data = np.random.uniform(0.0, 1.0, (width, height, 4))
    out_data = np.zeros((width, height, 4))

    in_buffer = context.buffer(in_data.astype('f4'))
    in_buffer.bind_to_storage_buffer(0)

    out_buffer = context.buffer(out_data.astype('f4'))
    out_buffer.bind_to_storage_buffer(1)

    compute_shader.run(x, y, z)

    data = np.frombuffer(out_buffer.read(), dtype='f4')
    data = data.reshape((height, width, 4))
    return _flatten_array(data)


class QtObserver(QThread):
    ''' glues qt thread with watchdog observer thread '''

    signal_glue = pyqtSignal()

    def __init__(self, watch_path):
        super(QtObserver, self).__init__()

        self.watch_path = watch_path

    def on_watch(self):
        self.signal_glue.emit()

    def run(self):
        observer = Observer()

        event_handler = OnChangeHandler(self.on_watch)
        observer.schedule(event_handler, self.watch_path, recursive=True)
        observer.start()

        observer.join()


class OnChangeHandler(FileSystemEventHandler):
    def __init__(self, on_mod_callback):
        self.on_mod_callback = on_mod_callback
        self.on_mod_callback()

    def on_modified(self, event):
        self.on_mod_callback()


class ComputeShaderViewer(QtWidgets.QLabel):
    def __init__(self, size):
        super(ComputeShaderViewer, self).__init__()

        self.size = size
        self.shader_path = "./gl/tex_gen/step_texture.glsl"
        self.watch_path = "./gl/tex_gen/"

        self.observer = QtObserver(self.watch_path)
        self.observer.signal_glue.connect(self.recompile_compute_shader)
        self.observer.start()

    def recompile_compute_shader(self):
        try:
            data = _compute_driven_generation(self.size[0], self.size[1], self.shader_path)
            img = Image.fromarray(data)
            pixmap = QPixmap.fromImage(ImageQt(img))
            self.setPixmap(pixmap)
            self.setAlignment(Qt.AlignCenter)
        except Exception as e:
            print(e)

    def keyPressEvent(self, e=None):
        if e.key() == Qt.Key_Space:
            data = _compute_driven_generation(self.size[0], self.size[1], self.shader_path)
            img = Image.fromarray(data)
            img.save("GPU_Generated.png")


class FragmentWatcher(QtWidgets.QOpenGLWidget):
    def __init__(self, size, fspath):
        super(FragmentWatcher, self).__init__()

        self.size = size
        self.setMinimumSize(size[0], size[1])
        self.setMaximumSize(size[0], size[1])
        self.watch_path = "./_gl/"

        self.fspath = fspath

        self.vao = None

    def recompile_shaders(self, path):
        print("recompiling shaders..", path)

        try:
            vs = _read("./_gl/simple.vs")
            fs = _read(path)

            program = self.context.program(vertex_shader=vs, fragment_shader=fs)
            self.u_time = program["u_time"]
            self.u_campos = program["u_campos"]
            self.u_campos.value = (0.0, 5.0, -10.0)

            self.u_focus = program["u_focus"]
            self.u_focus.value = (0.0, 2.0, 0.0)

            if "u_drawbg" in program:
                program["u_drawbg"].value = True

            self.vao = _screen_quad(program, self.context)

            kju_data = ii.imread("./_tex/kju_sq.jpg")
            tex_w = kju_data.shape[0]
            tex_h = kju_data.shape[1]
            tex_c = kju_data.shape[2]

            _tex = self.context.texture((tex_w, tex_h), tex_c, kju_data.tobytes())
            _tex.use(0)

        except Exception as e:
            print("failed to compile shaders, {}".format(e))
            return

        print("recompiled shaders!")

    def start_recording(self):
        if not os.path.isdir("./yeon"):
            os.makedirs("./yeon")
        self.mp4_writer = ii.get_writer("./yeon/yeon.mp4", fps=24)
        self.is_recording = True

    def initializeGL(self):
        self.context = mg.create_context()
        self.start_time = time.time()
        self.recompile_shaders(self.fspath)

        self.observer = QtObserver(self.watch_path)
        self.observer.signal_glue.connect(partial(self.recompile_shaders, self.fspath))
        self.observer.start()

        self.tex = self.context.texture(self.size, 4, dtype='f4')
        self.frame_buffer = self.context.framebuffer([self.tex])

        self.is_recording = False
        if False:
            self.start_recording()

    def paintGL(self):
        if self.vao:
            t = time.time() - self.start_time
            self.u_time.value = t
            x = math.cos(t) * +7.0
            z = math.sin(t) * -7.0
            self.u_campos.value = (x, 4.0, z)
            self.vao.render()
            self.update()

            if self.is_recording:
                self.frame_buffer.use()
                self.vao.render()
                data = self.tex.read()
                data = np.frombuffer(data, dtype='f4')
                data = data.reshape(self.size[1], self.size[0], 4)
                data = _flatten_array(data)
                self.mp4_writer.append_data(data)


class Tool(QtWidgets.QWidget):

    def __init__(self, width, height):
        super(Tool, self).__init__()

        root_layout = QtWidgets.QVBoxLayout()
        self.setLayout(root_layout)
        root_layout.setContentsMargins(0, 0, 0, 0)

        shaders_layout = QtWidgets.QVBoxLayout()
        root_layout.addLayout(shaders_layout)
        shaders_layout.setContentsMargins(0, 0, 0, 0)

        self.path_le = QtWidgets.QLineEdit()
        shaders_layout.addWidget(self.path_le)
        fspath = "./_gl/scenes/pikachu.fs"
        self.path_le.setText(fspath)

        self.renderer = FragmentWatcher((width, height), fspath)
        root_layout.addWidget(self.renderer)

        self.path_le.returnPressed.connect(self.recompile)

    def recompile(self):
        path = self.path_le.text()
        self.renderer.recompile_shaders(path)


def main():
    # serialize result
    if False:
        if not os.path.isdir("pika"):
            os.makedirs("pika")

        if not os.path.isdir("./zupang"):
            os.makedirs("./zupang")

        if not os.path.isdir("./yeon"):
            os.makedirs("./yeon")

        vs = "./_gl/simple.vs"
        fs = "./_gl/scenes/zupang.fs"

        distance = 10.0
        u_campos = (0.0, 10.0, -10.0)
        u_focus = (0.0, 2.0, 0.0)

        # gif/mp4
        if False:
            gif_writer = ii.get_writer("./zupang/zupang.gif", fps=24)
            mp4_writer = ii.get_writer("./zupang/zupang.mp4", fps=24)

            for i in range(64):
                x, y, z = math.cos(i * 0.15) * distance, 2.5, math.sin(i * 0.15) * distance
                u_campos = (x, y, z)
                for data in _screenspace_generation(
                        304, 304,
                        vs, fs,
                        start_time=0.0, end_time=0.0, frames=1,
                        u_campos=u_campos, u_focus=u_focus):
                    gif_writer.append_data(data)
                    mp4_writer.append_data(data)

        if True:
            res = 1024
            n_row = 8

            vs = "./_gl/simple.vs"
            fs = "./_gl/scenes/pikachu_buffers.fs"

            # _imposter_gen(res, vs, fs, n_row=n_row).save("./pika/T_PikachuAtlas.png")

            albedo, normal = _imposter_gen_buffers(
                res, vs, fs,
                n_row=n_row, dist=8.0)
            albedo.save("./pika/T_PikachuAtlas_albedo.png")
            normal.save("./pika/T_PikachuAtlas_normal.png")

        # screenshot
        if False:
            i = 0
            for data in _screenspace_generation(
                    512, 512,
                    vs, fs,
                    start_time=2.4, end_time=2.2,
                    u_campos=u_campos, u_focus=u_focus):
                ii.imwrite("./zupang/zupang_{}.png".format(i), data)
                i += 1

        return

    app = QtWidgets.QApplication([])
    mainwin = QtWidgets.QMainWindow()
    mainwin.setWindowFlags(Qt.WindowStaysOnTopHint)
    mainwin.setWindowTitle("Imposter Renderer")

    w, h = 600, 600
    tool = Tool(w, h)
    mainwin.setCentralWidget(tool)
    mainwin.show()

    app.exec()

if __name__ == "__main__":
    main()
