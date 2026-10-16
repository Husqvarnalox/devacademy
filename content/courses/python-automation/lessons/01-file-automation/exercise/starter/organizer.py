"""Организатор файлов: классификация по расширению или дате, сухой прогон, идемпотентность."""

import os
from datetime import datetime, timezone
from pathlib import Path


def target_dir(name, mtime, by="ext"):
    """Имя подпапки: при by='ext' - расширение в нижнем регистре без точки или 'other';
    при by='date' - 'YYYY-MM' по mtime в UTC; иначе ValueError."""
    raise NotImplementedError


def unique_name(name, taken):
    """Вернуть name, если его нет в множестве taken, иначе 'stem (1).ext', 'stem (2).ext', ..."""
    raise NotImplementedError


def safe_move(src, dst):
    """Переместить файл, не затирая существующий: если dst занят - FileExistsError."""
    raise NotImplementedError


def plan_moves(root, by="ext"):
    """Вернуть [(src, dst)] с относительными posix-путями ('a.txt', 'txt/a.txt').
    Только обычные файлы прямо в root, отсортированные по имени; пропускать скрытые (.name),
    симлинки и подпапки. Коллизии с уже существующими файлами разрешать через unique_name.
    Диск не менять."""
    raise NotImplementedError


def organize(root, by="ext", dry_run=False):
    """Выполнить план (создав подпапки) и вернуть его; при dry_run=True только вернуть."""
    raise NotImplementedError
