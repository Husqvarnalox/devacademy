import hashlib
import shutil
from pathlib import Path


def file_digest(path, chunk_size=65536):
    """SHA-256 (hex) содержимого файла; файл читается блоками по chunk_size байт, а не целиком."""
    sha = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            chunk = f.read(chunk_size)
            if not chunk:
                break
            sha.update(chunk)
    return sha.hexdigest()


def build_manifest(root):
    """Манифест каталога: {относительный путь с "/": хеш файла}, ключи по алфавиту."""
    root = Path(root)
    entries = {}
    for path in root.rglob("*"):
        if path.is_file():
            entries[path.relative_to(root).as_posix()] = file_digest(path)
    return dict(sorted(entries.items()))


def dataset_version(manifest):
    """Версия набора данных: первые 12 hex-символов SHA-256 от строк "<хеш>  <путь>\\n" в порядке путей."""
    text = "".join(f"{digest}  {path}\n" for path, digest in sorted(manifest.items()))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:12]


def diff_manifests(old, new):
    """Различия двух манифестов: added, removed, modified, renamed ([старый путь, новый путь])."""
    added = sorted(set(new) - set(old))
    removed = sorted(set(old) - set(new))
    modified = sorted(path for path in set(old) & set(new) if old[path] != new[path])

    removed_by_digest, added_by_digest = {}, {}
    for path in removed:
        removed_by_digest.setdefault(old[path], []).append(path)
    for path in added:
        added_by_digest.setdefault(new[path], []).append(path)

    renamed = []
    for digest, old_paths in removed_by_digest.items():
        for old_path, new_path in zip(old_paths, added_by_digest.get(digest, [])):
            renamed.append([old_path, new_path])
    renamed.sort()

    renamed_old = {pair[0] for pair in renamed}
    renamed_new = {pair[1] for pair in renamed}
    return {
        "added": [path for path in added if path not in renamed_new],
        "removed": [path for path in removed if path not in renamed_old],
        "modified": modified,
        "renamed": renamed,
    }


def store_object(cache_dir, path):
    """Копирует файл в кеш по содержимому: cache_dir/<2 символа хеша>/<остаток хеша>. Возвращает хеш."""
    digest = file_digest(path)
    target = Path(cache_dir) / digest[:2] / digest[2:]
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
    return digest
