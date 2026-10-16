import json
import os
import shutil
import tempfile
from pathlib import Path

NONCE = os.environ["DEVACADEMY_NONCE"]


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


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import organizer as m

DAY = 86400
JAN = 1704067200  # 2024-01-01T00:00:00Z
MAR = JAN + 60 * DAY  # 2024-03-01T00:00:00Z


def make_tree(files):
    """files: {name: mtime}. Возвращает Path нового каталога внутри рабочего каталога."""
    root = Path(tempfile.mkdtemp(prefix="org_", dir=os.getcwd()))
    for name, mtime in files.items():
        p = root / name
        p.write_text(f"content of {name}", encoding="utf-8")
        os.utime(p, (mtime, mtime))
    return root


def snapshot(root):
    return sorted(str(p.relative_to(root)) for p in Path(root).rglob("*"))


def with_tree(files, body):
    root = make_tree(files)
    try:
        return body(root)
    finally:
        shutil.rmtree(root, ignore_errors=True)


def t_target_dir():
    for args, want in [
        (("Report.PDF", 0, "ext"), "pdf"),
        (("photo.jpeg", 0, "ext"), "jpeg"),
        (("README", 0, "ext"), "other"),
        (("archive.tar.gz", 0, "ext"), "gz"),
        (("a.txt", JAN + DAY, "date"), "2024-01"),
        (("a.txt", MAR + 5, "date"), "2024-03"),
    ]:
        bad = expect(m.target_dir(*args), want, f"target_dir{args}")
        if bad:
            return bad
    try:
        m.target_dir("a.txt", 0, "size")
    except ValueError:
        return None
    return "неизвестный режим должен давать ValueError"


def t_unique_name():
    for args, want in [
        (("a.txt", set()), "a.txt"),
        (("a.txt", {"a.txt"}), "a (1).txt"),
        (("a.txt", {"a.txt", "a (1).txt"}), "a (2).txt"),
        (("data", {"data"}), "data (1)"),
        (("x.tar.gz", {"x.tar.gz"}), "x.tar (1).gz"),
    ]:
        bad = expect(m.unique_name(*args), want, f"unique_name{args}")
        if bad:
            return bad


def t_plan_basic():
    def body(root):
        plan = m.plan_moves(root)
        want = [("a.TXT", "txt/a.TXT"), ("b.csv", "csv/b.csv"), ("noext", "other/noext")]
        bad = expect(plan, want, "plan_moves(root)")
        if bad:
            return bad
        (root / "sub").mkdir()
        (root / "sub" / "deep.txt").write_text("x")
        again = m.plan_moves(root)
        return expect(again, want, "plan_moves с подпапкой (подпапки не трогать)")

    return with_tree({"b.csv": JAN, "a.TXT": JAN, "noext": JAN, ".hidden": JAN}, body)


def t_dry_run():
    def body(root):
        before = snapshot(root)
        plan = m.organize(root, dry_run=True)
        if snapshot(root) != before:
            return "dry_run=True изменил файловую систему"
        return expect(plan, [("a.txt", "txt/a.txt"), ("b.md", "md/b.md")], "organize(root, dry_run=True)")

    return with_tree({"a.txt": JAN, "b.md": JAN}, body)


def t_idempotent():
    def body(root):
        first = m.organize(root, by="date")
        want = [("a.txt", "2024-01/a.txt"), ("b.txt", "2024-03/b.txt")]
        bad = expect(first, want, "organize(root, by='date')")
        if bad:
            return bad
        after_first = snapshot(root)
        second = m.organize(root, by="date")
        if second != []:
            return f"повторный запуск вернул {second!r}, ожидался []"
        if snapshot(root) != after_first:
            return "повторный запуск изменил файлы"
        if (root / "2024-03" / "b.txt").read_text() != "content of b.txt":
            return "содержимое файла изменилось"

    return with_tree({"a.txt": JAN + DAY, "b.txt": MAR}, body)


def t_collision():
    def body(root):
        (root / "txt").mkdir()
        (root / "txt" / "a.txt").write_text("OLD")
        (root / "txt" / "a (1).txt").write_text("OLD1")
        plan = m.organize(root)
        bad = expect(plan, [("a.txt", "txt/a (2).txt"), ("b.txt", "txt/b.txt")], "organize при коллизии")
        if bad:
            return bad
        if (root / "txt" / "a.txt").read_text() != "OLD" or (root / "txt" / "a (1).txt").read_text() != "OLD1":
            return "существующие файлы были затёрты"
        if (root / "txt" / "a (2).txt").read_text() != "content of a.txt":
            return "перемещённый файл потерял содержимое"
        return expect(m.organize(root), [], "повторный organize")

    return with_tree({"a.txt": JAN, "b.txt": JAN}, body)


def t_symlinks():
    outside = Path(tempfile.mkdtemp(prefix="outside_", dir=os.getcwd()))
    try:
        (outside / "secret.txt").write_text("outside")

        def body(root):
            try:
                os.symlink(outside / "secret.txt", root / "link.txt")
                os.symlink(outside, root / "linkdir")
            except (OSError, NotImplementedError):
                return None  # платформа не позволяет создавать симлинки
            plan = m.plan_moves(root)
            bad = expect(plan, [("real.txt", "txt/real.txt")], "plan_moves с симлинками")
            if bad:
                return bad
            m.organize(root)
            if (outside / "secret.txt").read_text() != "outside" or not (root / "link.txt").is_symlink():
                return "симлинк был перемещён или внешний файл изменён"
            if snapshot(outside) != ["secret.txt"]:
                return "в каталог вне root что-то записано"
            # симлинк вместо целевой подпапки: файлы туда не перемещаются
            (root / "new.md").write_text("n")
            os.symlink(outside, root / "md")
            m.organize(root)
            if snapshot(outside) != ["secret.txt"]:
                return "файл перемещён по симлинку наружу"
            if not (root / "new.md").exists():
                return "файл должен остаться на месте, если подпапка - симлинк"

        return with_tree({"real.txt": JAN}, body)
    finally:
        shutil.rmtree(outside, ignore_errors=True)


def t_safe_move():
    def body(root):
        m.safe_move(root / "a.txt", root / "moved.txt")
        if (root / "a.txt").exists() or (root / "moved.txt").read_text() != "content of a.txt":
            return "safe_move должен перенести файл"
        try:
            m.safe_move(root / "b.txt", root / "moved.txt")
        except FileExistsError:
            pass
        else:
            return "safe_move затёр существующий файл (ожидался FileExistsError)"
        if (root / "moved.txt").read_text() != "content of a.txt" or not (root / "b.txt").exists():
            return "при ошибке оба файла должны остаться нетронутыми"

    return with_tree({"a.txt": JAN, "b.txt": JAN}, body)


case("target-dir", t_target_dir)
case("unique-name", t_unique_name)
case("plan-basic", t_plan_basic)
case("dry-run", t_dry_run)
case("idempotent", t_idempotent)
case("collision", t_collision)
case("symlinks", t_symlinks)
case("safe-move", t_safe_move)
