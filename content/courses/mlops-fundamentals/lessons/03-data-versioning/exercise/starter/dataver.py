import hashlib
import shutil
from pathlib import Path


def file_digest(path, chunk_size=65536):
    """SHA-256 (hex) содержимого файла; файл читается блоками по chunk_size байт, а не целиком."""
    # TODO: открыть файл в режиме "rb", читать f.read(chunk_size) до пустого результата, подавая блоки в sha.update
    return ""


def build_manifest(root):
    """Манифест каталога: {относительный путь с "/": хеш файла}, ключи по алфавиту."""
    # TODO: обойти root.rglob("*"), взять только файлы, ключ - path.relative_to(root).as_posix()
    return {}


def dataset_version(manifest):
    """Версия набора данных: первые 12 hex-символов SHA-256 от строк "<хеш>  <путь>\\n" в порядке путей."""
    # TODO: склеить f"{digest}  {path}\n" по sorted(manifest.items()) (два пробела) и хешировать в UTF-8
    return ""


def diff_manifests(old, new):
    """Различия двух манифестов: added, removed, modified, renamed ([старый путь, новый путь])."""
    # TODO: added/removed - разность множеств путей, modified - общий путь с другим хешем
    # TODO: переименование - удалённый и добавленный путь с одним хешем; при нескольких парах i-й удалённый с i-м добавленным (по алфавиту)
    # TODO: пары из renamed исключить из added и removed; все списки отсортировать
    return {"added": [], "removed": [], "modified": [], "renamed": []}


def store_object(cache_dir, path):
    """Копирует файл в кеш по содержимому: cache_dir/<2 символа хеша>/<остаток хеша>. Возвращает хеш."""
    # TODO: digest = file_digest(path); если объекта нет - создать каталоги и скопировать (shutil.copyfile), иначе не трогать
    return ""
