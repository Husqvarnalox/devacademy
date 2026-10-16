"""Generates sample_nbformat45.ipynb the way Jupyter writes notebooks (json.dumps indent=1, sorted keys, ensure_ascii=False).
Run: python3 make_fixture.py > sample_nbformat45.ipynb   (the output is committed; this file documents where it came from)."""
import base64, json, struct, zlib, sys

def png(w=2, h=2):
    raw = b''.join(b'\x00' + bytes([200, 30, 30]) * w for _ in range(h))
    def chunk(t, d):
        c = struct.pack('>I', len(d)) + t + d
        return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')

def lines(s):
    return s.splitlines(True)

nb = {
    "cells": [
        {"cell_type": "markdown", "id": "intro0001", "metadata": {"tags": ["intro"]},
         "source": lines("# Заметки\n\nФормула: $$E = mc^2$$\n\n| a | b |\n|---|---|\n| 1 | 2 |\n"),
         "attachments": {"pic.png": {"image/png": base64.b64encode(png()).decode()}}},
        {"cell_type": "code", "execution_count": 1, "id": "code00001", "metadata": {"collapsed": False, "scrolled": True},
         "outputs": [
            {"name": "stdout", "output_type": "stream", "text": lines("hello\nмир\n")},
            {"name": "stderr", "output_type": "stream", "text": lines("warning: \x1b[33mcareful\x1b[0m\n")},
         ],
         "source": lines("import sys\nprint('hello')\nprint('мир')\nprint('warning', file=sys.stderr)")},
        {"cell_type": "code", "execution_count": 2, "id": "code00002", "metadata": {},
         "outputs": [
            {"data": {"text/html": lines("<table><tr><th>a</th></tr><tr><td>1</td></tr></table>"),
                      "text/plain": lines("   a\n0  1")},
             "execution_count": 2, "metadata": {}, "output_type": "execute_result"},
         ],
         "source": ["df"]},
        {"cell_type": "code", "execution_count": 3, "id": "code00003", "metadata": {},
         "outputs": [
            {"data": {"image/png": base64.b64encode(png()).decode() + "\n", "text/plain": ["<Figure size 640x480 with 1 Axes>"]},
             "metadata": {"needs_background": "light"}, "output_type": "display_data"},
            {"data": {"application/json": {"a": [1, 2, {"b": None}], "pi": 3.14}, "text/plain": ["{'a': [1, 2, {'b': None}], 'pi': 3.14}"]},
             "metadata": {"application/json": {"expanded": False}}, "output_type": "display_data"},
         ],
         "source": ["plt.plot([1, 2, 3])"]},
        {"cell_type": "code", "execution_count": 4, "id": "code00004", "metadata": {},
         "outputs": [
            {"ename": "ZeroDivisionError", "evalue": "division by zero", "output_type": "error",
             "traceback": ["\x1b[0;31m---------------------------------------------------------------------------\x1b[0m",
                           "\x1b[0;31mZeroDivisionError\x1b[0m                         Traceback (most recent call last)",
                           "Cell \x1b[0;32mIn[4], line 1\x1b[0m\n\x1b[0;32m----> 1\x1b[0m \x1b[38;5;241;43m1\x1b[39;49m\x1b[38;5;241m/\x1b[39m\x1b[38;5;241m0\x1b[39m\n",
                           "\x1b[0;31mZeroDivisionError\x1b[0m: division by zero"]},
         ],
         "source": ["1/0"]},
        {"cell_type": "code", "execution_count": None, "id": "code00005", "metadata": {}, "outputs": [], "source": []},
        {"cell_type": "raw", "id": "raw000001", "metadata": {"format": "text/restructuredtext"}, "source": ["Raw *text*"]},
    ],
    "metadata": {
        "kernelspec": {"display_name": "Python 3 (ipykernel)", "language": "python", "name": "python3"},
        "language_info": {"codemirror_mode": {"name": "ipython", "version": 3}, "file_extension": ".py", "mimetype": "text/x-python",
                          "name": "python", "nbconvert_exporter": "python", "pygments_lexer": "ipython3", "version": "3.14.6"},
        "custom_extension": {"anything": [1, 2, 3], "nested": {"k": "v"}},
    },
    "nbformat": 4,
    "nbformat_minor": 5,
}
sys.stdout.write(json.dumps(nb, indent=1, sort_keys=True, ensure_ascii=False) + "\n")
