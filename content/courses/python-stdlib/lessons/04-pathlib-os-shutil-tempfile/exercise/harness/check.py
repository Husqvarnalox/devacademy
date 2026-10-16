import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import tempfile
from contextlib import contextmanager
from pathlib import Path

import fs_task as m


@contextmanager
def workdir():
    with tempfile.TemporaryDirectory(dir=os.getcwd()) as d:
        yield Path(d)


def make_tree(root, files):
    for rel, data in files.items():
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data if isinstance(data, bytes) else data.encode("utf-8"))


def t_atomic_basic():
    with workdir() as d:
        target = d / "out.txt"
        m.atomic_write(target, "привет\n")
        bad = expect(target.read_bytes(), "привет\n".encode("utf-8"), "содержимое после первой записи")
        if bad:
            return bad
        m.atomic_write(str(target), "второй вариант")
        return expect(target.read_text(encoding="utf-8"), "второй вариант", "содержимое после замены")


def t_atomic_failure():
    with workdir() as d:
        target = d / "keep.txt"
        target.write_text("старое", encoding="utf-8")
        try:
            m.atomic_write(target, 12345)
        except Exception:
            pass
        else:
            return "для text, не являющегося строкой, ожидалось исключение"
        bad = expect(target.read_text(encoding="utf-8"), "старое", "исходный файл после сбоя")
        if bad:
            return bad
        return expect(sorted(p.name for p in d.iterdir()), ["keep.txt"], "содержимое каталога после сбоя")


def t_atomic_leftovers():
    with workdir() as d:
        target = d / "new.txt"
        m.atomic_write(target, "a\nb\r\nc")
        bad = expect(target.read_bytes(), b"a\nb\r\nc", "байты файла (переводы строк не меняются)")
        if bad:
            return bad
        return expect(sorted(p.name for p in d.iterdir()), ["new.txt"], "каталог после успешной записи")


def t_ext_basic():
    with workdir() as d:
        make_tree(d, {"a.py": "1", "pkg/b.py": "2", "Makefile": "x", "notes.txt": "n"})
        got = m.list_by_ext(d)
        return expect(
            got,
            {".py": ["a.py", "pkg/b.py"], "": ["Makefile"], ".txt": ["notes.txt"]},
            "list_by_ext(tree)",
        )


def t_ext_edge():
    with workdir() as d:
        make_tree(d, {"README.MD": "x", "docs/a.md": "y", ".gitignore": "z", "z/y/x/deep.Py": "q"})
        (d / "emptydir.d").mkdir()
        got = m.list_by_ext(d)
        bad = expect(
            got,
            {".md": ["README.MD", "docs/a.md"], "": [".gitignore"], ".py": ["z/y/x/deep.Py"]},
            "list_by_ext с регистром и вложенностью",
        )
        if bad:
            return bad
    with workdir() as d:
        return expect(m.list_by_ext(d), {}, "list_by_ext(пустой каталог)")


def t_size_basic():
    with workdir() as d:
        make_tree(d, {"a.txt": b"12345", "sub/b.txt": b"123", "sub/c.bin": b"1234567"})
        bad = expect(m.total_size(d), 15, "total_size(d)")
        if bad:
            return bad
        return expect(m.total_size(d, "*.txt"), 8, "total_size(d, '*.txt')")


def t_join_basic():
    with workdir() as d:
        base = d.resolve()
        got = m.safe_join(d, "static", "a.css")
        bad = expect(got, base / "static" / "a.css", "safe_join(d, 'static', 'a.css')")
        if bad:
            return bad
        bad = expect(m.safe_join(d), base, "safe_join(d)")
        if bad:
            return bad
        return expect(m.safe_join(d, "a", "..", "b"), base / "b", "safe_join(d, 'a', '..', 'b')")


def t_join_escape():
    with workdir() as d:
        for parts in (("..",), ("a", "..", "..", "x"), ("/etc/passwd",), ("../" + d.name + "_evil",)):
            try:
                got = m.safe_join(d, *parts)
            except ValueError:
                continue
            return f"safe_join(d, *{parts!r}) вернул {got}, ожидался ValueError"


def t_copy_clean():
    with workdir() as d:
        src = d / "src"
        make_tree(src, {"a.py": "1", "pkg/b.py": "2", "pkg/__pycache__/b.pyc": "x", "t.tmp": "t", "pkg/u.tmp": "u"})
        dst = d / "dst"
        got = m.copy_clean(src, dst)
        bad = expect(got, ["a.py", "pkg/b.py"], "copy_clean: список файлов")
        if bad:
            return bad
        bad = expect(sorted(p.name for p in (dst / "pkg").iterdir()), ["b.py"], "содержимое dst/pkg")
        if bad:
            return bad
        try:
            m.copy_clean(src, dst)
        except FileExistsError:
            return None
        return "при существующем dst нужен FileExistsError"


case("atomic-basic", t_atomic_basic)
case("ext-basic", t_ext_basic)
case("size-basic", t_size_basic)
case("join-basic", t_join_basic)
case("atomic-failure", t_atomic_failure)
case("atomic-leftovers", t_atomic_leftovers)
case("join-escape", t_join_escape)
case("ext-edge", t_ext_edge)
case("copy-clean", t_copy_clean)
