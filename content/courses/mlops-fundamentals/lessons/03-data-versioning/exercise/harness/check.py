import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

NONCE = os.environ["DEVACADEMY_NONCE"]

import dataver as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def ref_version(manifest):
    text = "".join(f"{d}  {p}\n" for p, d in sorted(manifest.items()))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:12]


def workdir():
    return Path(tempfile.mkdtemp(prefix="dv_", dir=os.getcwd()))


def with_root(body):
    root = workdir()
    try:
        return body(root)
    finally:
        shutil.rmtree(root, ignore_errors=True)


def put(root, rel, data):
    p = Path(root) / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(data)
    return p


BIG = bytes((i * 7) % 251 for i in range(200_000))


def t_file_digest():
    def body(root):
        for name, data in (("empty.bin", b""), ("abc.txt", b"abc"), ("big.bin", BIG), ("ru.txt", "данные\n".encode("utf-8"))):
            p = put(root, name, data)
            got = m.file_digest(p)
            if got != sha(data):
                return f"file_digest({name}) вернула {got!r}, ожидалось {sha(data)!r} (SHA-256 всех байтов файла)"
        p = root / "abc.txt"
        if m.file_digest(str(p)) != sha(b"abc"):
            return "путь можно передать и строкой"
        for size in (1, 2, 7, 4096):
            if m.file_digest(root / "big.bin", chunk_size=size) != sha(BIG):
                return f"результат не должен зависеть от chunk_size (проверено на {size})"
        if m.file_digest(p, 1) != sha(b"abc"):
            return "chunk_size - второй позиционный параметр, при значении 1 результат тот же"
    return with_root(body)


def t_manifest_basic():
    def body(root):
        files = {"b.csv": b"1,2\n", "a.csv": b"x\n", "sub/z.txt": b"zz", "sub/deep/c.txt": b"c"}
        for rel, data in files.items():
            put(root, rel, data)
        got = m.build_manifest(root)
        want = {rel: sha(data) for rel, data in files.items()}
        if got != want:
            return f"build_manifest вернула {got!r}, ожидалось {want!r}"
        if list(got) != sorted(want):
            return f"ключи манифеста должны идти по алфавиту: получили {list(got)!r}, ожидалось {sorted(want)!r}"
        if "\\" in "".join(got):
            return "пути записываются с «/» (as_posix)"
        empty = root / "nothing"
        empty.mkdir()
        if m.build_manifest(empty) != {}:
            return "пустой каталог даёт пустой манифест"
    return with_root(body)


def t_dataset_version():
    d = [sha(bytes([i])) for i in range(3)]
    man = {"b.csv": d[1], "a.csv": d[0], "sub/c.csv": d[2]}
    got = m.dataset_version(man)
    if not isinstance(got, str) or len(got) != 12 or any(c not in "0123456789abcdef" for c in got):
        return f"версия должна быть строкой из 12 строчных шестнадцатеричных символов, получили {got!r}"
    if got != ref_version(man):
        return f"версия {got!r} не совпадает с эталоном {ref_version(man)!r}: хешируйте строки f\"{{digest}}  {{path}}\\n\" (два пробела) в порядке путей"
    reordered = dict(reversed(list(man.items())))
    if m.dataset_version(reordered) != got:
        return "порядок ключей в словаре не должен влиять на версию"
    changed = {**man, "a.csv": d[2]}
    if m.dataset_version(changed) == got:
        return "изменение хеша файла должно менять версию"
    renamed = {"b.csv": d[1], "a2.csv": d[0], "sub/c.csv": d[2]}
    if m.dataset_version(renamed) == got:
        return "переименование файла должно менять версию (путь входит в манифест)"
    empty = hashlib.sha256(b"").hexdigest()[:12]
    if m.dataset_version({}) != empty:
        return f"для пустого манифеста ожидалось {empty!r} (SHA-256 пустой строки, 12 символов), получили {m.dataset_version({})!r}"


def t_diff_basic():
    old = {"a": "h1", "b": "h2", "c": "h3", "same": "h0"}
    new = {"a": "h1", "b": "h9", "d": "h4", "same": "h0"}
    got = m.diff_manifests(old, new)
    want = {"added": ["d"], "removed": ["c"], "modified": ["b"], "renamed": []}
    if got != want:
        return f"diff_manifests вернула {got!r}, ожидалось {want!r}"
    same = m.diff_manifests(old, dict(old))
    if same != {"added": [], "removed": [], "modified": [], "renamed": []}:
        return f"одинаковые манифесты не дают различий, получили {same!r}"


def t_diff_renames():
    # одно переименование, остальное не затронуто
    got = m.diff_manifests({"raw/a.csv": "h1", "k": "h2"}, {"clean/a.csv": "h1", "k": "h2"})
    if got != {"added": [], "removed": [], "modified": [], "renamed": [["raw/a.csv", "clean/a.csv"]]}:
        return f"переименование с тем же хешем: получили {got!r}"
    # переименование с изменением содержимого - это удаление и добавление
    got = m.diff_manifests({"a": "h1"}, {"b": "h2"})
    if got != {"added": ["b"], "removed": ["a"], "modified": [], "renamed": []}:
        return f"путь поменялся вместе с хешем - это removed + added, получили {got!r}"
    # несколько файлов с одним хешем: i-й удалённый с i-м добавленным по алфавиту
    old = {"a1": "h", "a2": "h", "a3": "h", "keep": "k"}
    new = {"b1": "h", "b2": "h", "keep": "k"}
    got = m.diff_manifests(old, new)
    want = {"added": [], "removed": ["a3"], "modified": [], "renamed": [["a1", "b1"], ["a2", "b2"]]}
    if got != want:
        return f"при совпадении хеша у нескольких файлов пары идут по алфавиту, лишние остаются removed/added: получили {got!r}, ожидалось {want!r}"
    # смесь: переименование, изменение, добавление, удаление, дубликат содержимого
    old = {"z.csv": "h1", "m.csv": "h2", "old.csv": "h3", "dup1": "hd"}
    new = {"a.csv": "h1", "m.csv": "h5", "new.csv": "h6", "dup1": "hd", "dup2": "hd"}
    got = m.diff_manifests(old, new)
    want = {"added": ["dup2", "new.csv"], "removed": ["old.csv"], "modified": ["m.csv"], "renamed": [["z.csv", "a.csv"]]}
    if got != want:
        return f"смешанный случай: получили {got!r}, ожидалось {want!r}"
    # renamed отсортирован по старому пути
    old = {"y": "h1", "x": "h2"}
    new = {"b": "h1", "a": "h2"}
    got = m.diff_manifests(old, new)
    if got["renamed"] != [["x", "a"], ["y", "b"]]:
        return f"renamed сортируется по старому пути: получили {got['renamed']!r}"


def t_manifest_unicode_duplicates():
    def body(root):
        put(root, "данные/обучение.csv", "id,имя\n1,Ана\n".encode("utf-8"))
        put(root, "данные/копия/обучение.csv", "id,имя\n1,Ана\n".encode("utf-8"))
        put(root, "пустой.txt", b"")
        put(root, "sub/пустой.txt", b"")
        (root / "пустая_папка").mkdir()
        (root / "данные" / "вложенная_пустая").mkdir()
        got = m.build_manifest(root)
        text = "id,имя\n1,Ана\n".encode("utf-8")
        want = {
            "данные/копия/обучение.csv": sha(text),
            "данные/обучение.csv": sha(text),
            "пустой.txt": sha(b""),
            "sub/пустой.txt": sha(b""),
        }
        if got != want:
            return f"build_manifest вернула {got!r}, ожидалось {want!r}: в манифест входят только файлы (пустые каталоги не входят), одинаковые файлы в разных местах сохраняются оба, пустые файлы допустимы"
        if list(got) != sorted(want):
            return f"ключи по алфавиту: получили {list(got)!r}"
        if m.dataset_version(got) != ref_version(want):
            return "версия манифеста с кириллицей не совпадает с эталоном: кодируйте строки в UTF-8"
    return with_root(body)


def t_store_object_layout():
    def body(root):
        cache = root / "cache"
        a = put(root, "data/a.csv", b"hello cache")
        b = put(root, "data/copy/b.csv", b"hello cache")
        c = put(root, "data/c.csv", "кириллица".encode("utf-8"))
        da = m.store_object(cache, a)
        if da != sha(b"hello cache"):
            return f"store_object вернула {da!r}, ожидался SHA-256 содержимого {sha(b'hello cache')!r}"
        obj = cache / da[:2] / da[2:]
        if not obj.is_file():
            return f"объект должен лежать в {obj.relative_to(root).as_posix()} (первые 2 символа хеша - каталог, остальные - имя файла)"
        if obj.read_bytes() != b"hello cache":
            return "содержимое объекта в кеше должно совпадать с исходным файлом"
        db = m.store_object(str(cache), b)
        if db != da:
            return "одинаковые по содержимому файлы дают один хеш"
        objects = [p for p in cache.rglob("*") if p.is_file()]
        if len(objects) != 1:
            return f"два файла с одним содержимым хранятся один раз, а в кеше {len(objects)} объектов"
        dc = m.store_object(cache, c)
        if (cache / dc[:2] / dc[2:]).read_bytes() != "кириллица".encode("utf-8"):
            return "объект с UTF-8 содержимым скопирован неверно"
        if not (a.is_file() and b.is_file() and c.is_file()):
            return "исходные файлы остаются на месте: store_object копирует, а не переносит"
        empty = put(root, "empty", b"")
        de = m.store_object(cache, empty)
        if de != sha(b"") or not (cache / de[:2] / de[2:]).is_file():
            return "пустой файл тоже хранится как объект"
    return with_root(body)


def t_store_object_idempotent():
    def body(root):
        cache = root / "cache"
        a = put(root, "a.bin", BIG)
        d = m.store_object(cache, a)
        obj = cache / d[:2] / d[2:]
        marker = 1_000_000_000
        os.utime(obj, ns=(marker * 10**9, marker * 10**9))
        before = obj.stat().st_mtime_ns
        again = m.store_object(cache, a)
        if again != d:
            return "повторный вызов возвращает тот же хеш"
        if obj.stat().st_mtime_ns != before:
            return "объект уже есть в кеше: повторный вызов не должен его перезаписывать (время изменения изменилось)"
        files = [p for p in cache.rglob("*") if p.is_file()]
        if len(files) != 1:
            return f"в кеше должен остаться один объект, а их {len(files)}"
        b = put(root, "b.bin", BIG + b"!")
        d2 = m.store_object(cache, b)
        if d2 == d or len([p for p in cache.rglob("*") if p.is_file()]) != 2:
            return "другое содержимое - другой объект"
        if obj.read_bytes() != BIG:
            return "добавление нового объекта не меняет старые"
    return with_root(body)


case("file-digest", t_file_digest)
case("manifest-basic", t_manifest_basic)
case("dataset-version", t_dataset_version)
case("diff-basic", t_diff_basic)
case("diff-renames", t_diff_renames)
case("manifest-unicode-duplicates", t_manifest_unicode_duplicates)
case("store-object-layout", t_store_object_layout)
case("store-object-idempotent", t_store_object_idempotent)
