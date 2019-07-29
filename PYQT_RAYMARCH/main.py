import time

import moderngl as mg
import numpy as np
from PyQt5 import QtWidgets
from PyQt5 import QtCore
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler


class Watcher(QtCore.QThread):
    on_modified_signal = QtCore.pyqtSignal()

    def __init__(self):
        super(Watcher, self).__init__()

    def on_modified(self, e):
        self.on_modified_signal.emit()

    def run(self):
        handler = FileSystemEventHandler()
        handler.on_modified = self.on_modified

        observer = Observer()
        observer.schedule(handler, "./gl/")
        observer.start()
        observer.join()


class Renderer(QtWidgets.QOpenGLWidget):
    def __init__(self):
        super(Renderer, self).__init__()

        self.u_width, self.u_height = 512, 512
        self.setFixedSize(self.u_width, self.u_height)
        self.setWindowFlag(QtCore.Qt.WindowStaysOnTopHint)
        self.setWindowTitle("Raymarch Example")

    def read(self, path):
        context = None

        with open(path, "r") as fp:
            context = fp.read()

        parsed_lines = []
        for line in context.splitlines():
            if line.startswith("#include "):
                parsed_lines.append(self.read(line.split("#include ")[1]))
                continue
            parsed_lines.append(line)

        return "\n".join(parsed_lines)

    def setup_uniforms(self, program, uniform):
        for n, v in uniform.items():
            if n in program:
                program[n].value = v

    def recompile_program(self, gl):
        try:
            self.program = gl.program(
                vertex_shader=self.read("./gl/vs.glsl"),
                fragment_shader=self.read("./gl/fs.glsl"),
            )
            self.vao = gl.vertex_array(self.program, self.vbo, self.ibo)

            if "u_time" in self.program:
                self.u_time = self.program["u_time"]
            self.setup_uniforms(self.program, {"u_width": self.u_width, "u_height": self.u_height})

        except Exception as e:
            print(e)
            return

    def initializeGL(self):
        gl = mg.create_context()
        self.vbo = [
            (
                gl.buffer(
                    np.array([-1.0, -1.0, -1.0, +1.0, +1.0, -1.0, +1.0, +1.0])
                    .astype(np.float32)
                    .tobytes()
                ),
                "2f",
                "in_pos",
            )
        ]
        self.ibo = gl.buffer(
            np.array([0, 1, 2, 2, 1, 3]).astype(np.int32).tobytes()
        )

        self.recompile_program(gl)

        self.watcher = Watcher()
        self.watcher.on_modified_signal.connect(lambda: self.recompile_program(gl))
        self.watcher.start()

    def paintGL(self):
        u_time = time.time() % 1000.0
        self.setup_uniforms(self.program, {"u_time": u_time})
        self.vao.render()
        self.update()


def main():
    app = QtWidgets.QApplication([])
    renderer = Renderer()
    renderer.show()
    app.exec()


if __name__ == "__main__":
    main()
