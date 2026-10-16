import os
import shutil
import tempfile
from pathlib import Path


def atomic_write(path, text):
    """Атомарно записать text в файл path (UTF-8)."""
    # TODO: временный файл в том же каталоге + os.replace
    return None


def list_by_ext(root):
    """{'.py': ['a.py', 'pkg/b.py'], '': ['Makefile']}"""
    # TODO
    return None


def total_size(root, pattern="*"):
    """Сумма размеров файлов дерева, подходящих под pattern."""
    # TODO
    return None


def safe_join(base, *parts):
    """Path внутри base или ValueError."""
    # TODO
    return None


def copy_clean(src, dst):
    """Копировать дерево без __pycache__ и *.tmp; вернуть список файлов."""
    # TODO
    return None
