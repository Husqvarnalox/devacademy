import os
import shutil
import tempfile
from pathlib import Path


def atomic_write(path, text):
    path = Path(path)
    fd, tmp = tempfile.mkstemp(dir=path.parent, prefix=path.name + ".", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(text)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp, path)
    except BaseException:
        Path(tmp).unlink(missing_ok=True)
        raise


def list_by_ext(root):
    root = Path(root)
    result = {}
    for p in root.rglob("*"):
        if p.is_file():
            result.setdefault(p.suffix.lower(), []).append(p.relative_to(root).as_posix())
    return {ext: sorted(files) for ext, files in result.items()}


def total_size(root, pattern="*"):
    return sum(p.stat().st_size for p in Path(root).rglob(pattern) if p.is_file())


def safe_join(base, *parts):
    base = Path(base).resolve()
    result = base.joinpath(*parts).resolve()
    if not result.is_relative_to(base):
        raise ValueError(f"путь выходит за пределы {base}")
    return result


def copy_clean(src, dst):
    shutil.copytree(src, dst, ignore=shutil.ignore_patterns("__pycache__", "*.tmp"))
    dst = Path(dst)
    return sorted(p.relative_to(dst).as_posix() for p in dst.rglob("*") if p.is_file())
