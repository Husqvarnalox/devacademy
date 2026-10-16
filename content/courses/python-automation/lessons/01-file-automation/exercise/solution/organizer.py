"""Организатор файлов: классификация по расширению или дате, сухой прогон, идемпотентность."""

import os
from datetime import datetime, timezone
from pathlib import Path


def target_dir(name, mtime, by="ext"):
    """Имя подпапки для файла: расширение в нижнем регистре / 'other' или 'YYYY-MM' (UTC)."""
    if by == "ext":
        ext = os.path.splitext(name)[1].lstrip(".").lower()
        return ext or "other"
    if by == "date":
        return datetime.fromtimestamp(mtime, timezone.utc).strftime("%Y-%m")
    raise ValueError(f"unknown mode: {by!r}")


def unique_name(name, taken):
    """Вернуть name или 'stem (N).ext', которого нет в множестве taken."""
    if name not in taken:
        return name
    stem, ext = os.path.splitext(name)
    n = 1
    while f"{stem} ({n}){ext}" in taken:
        n += 1
    return f"{stem} ({n}){ext}"


def safe_move(src, dst):
    """Переместить файл, не затирая существующий; FileExistsError, если dst занят."""
    if os.path.lexists(dst):
        raise FileExistsError(dst)
    os.replace(src, dst)


def plan_moves(root, by="ext"):
    """План перемещений [(src, dst)] с относительными posix-путями; ничего не меняет на диске."""
    root = Path(root)
    taken = {}
    plan = []
    for entry in sorted(os.scandir(root), key=lambda e: e.name):
        if entry.name.startswith("."):
            continue
        if entry.is_symlink() or not entry.is_file(follow_symlinks=False):
            continue
        folder = target_dir(entry.name, entry.stat(follow_symlinks=False).st_mtime, by)
        dest = root / folder
        if dest.is_symlink() or (dest.exists() and not dest.is_dir()):
            continue
        names = taken.get(folder)
        if names is None:
            names = set(os.listdir(dest)) if dest.is_dir() else set()
            taken[folder] = names
        new_name = unique_name(entry.name, names)
        names.add(new_name)
        plan.append((entry.name, f"{folder}/{new_name}"))
    return plan


def organize(root, by="ext", dry_run=False):
    """Выполнить план (или только вернуть его при dry_run=True). Повторный запуск возвращает []."""
    root = Path(root)
    plan = plan_moves(root, by)
    if dry_run:
        return plan
    for src, dst in plan:
        target = root / dst
        target.parent.mkdir(exist_ok=True)
        safe_move(root / src, target)
    return plan
