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


import jobs as m


def workdir(body):
    root = Path(tempfile.mkdtemp(prefix="job_", dir=os.getcwd()))
    try:
        return body(root)
    finally:
        shutil.rmtree(root, ignore_errors=True)


def t_lock_basic():
    def body(root):
        path = root / "job.lock"
        clock = [1000.0]
        a = m.LockFile(path, now=lambda: clock[0], pid=lambda: 111, is_alive=lambda pid: True)
        b = m.LockFile(path, now=lambda: clock[0], pid=lambda: 222, is_alive=lambda pid: True)
        a.acquire()
        if path.read_text().split() != ["111", "1000.0"]:
            return f"содержимое файла блокировки {path.read_text()!r}, ожидалось '111 1000.0'"
        try:
            b.acquire()
        except m.LockHeldError:
            pass
        else:
            return "второй acquire должен поднять LockHeldError"
        b.release()
        if not path.exists():
            return "release объекта, не владеющего блокировкой, не должен удалять чужой файл"
        a.release()
        a.release()
        if path.exists():
            return "release должен удалить файл блокировки"
        b.acquire()
        b.release()

    return workdir(body)


def t_lock_context():
    def body(root):
        path = root / "ctx.lock"
        lock = m.LockFile(path, is_alive=lambda pid: True)
        try:
            with lock:
                if not path.exists():
                    return "внутри with файл блокировки должен существовать"
                raise RuntimeError("boom")
        except RuntimeError:
            pass
        if path.exists():
            return "блокировка должна освобождаться и при исключении"
        with lock:
            pass

    return workdir(body)


def t_next_run():
    for args, want in [
        ((100, 10, 50), 100), ((100, 10, 100), 110), ((100, 10, 104), 110),
        ((100, 10, 110), 120), ((100, 10, 137), 140), ((0, 60, 3601), 3660),
    ]:
        bad = expect(m.next_run_time(*args), want, f"next_run_time{args}")
        if bad:
            return bad


def t_checkpoint_io():
    def body(root):
        path = root / "state.json"
        bad = expect(m.load_checkpoint(path, {"done": 0}), {"done": 0}, "load_checkpoint без файла")
        if bad:
            return bad
        m.save_checkpoint(path, {"done": 3, "name": "я"})
        bad = expect(m.load_checkpoint(path), {"done": 3, "name": "я"}, "load_checkpoint после save")
        if bad:
            return bad
        m.save_checkpoint(path, {"done": 4})
        left = sorted(p.name for p in root.iterdir())
        return expect(left, ["state.json"], "файлы после сохранений (временных быть не должно)")

    return workdir(body)


def t_lock_stale():
    def body(root):
        path = root / "s.lock"
        clock = [10_000.0]

        def lock(pid, alive):
            return m.LockFile(path, stale_after=600, now=lambda: clock[0], pid=lambda: pid, is_alive=alive)

        path.write_text("5 9000.0\n")  # старше stale_after, процесс жив
        new = lock(7, lambda p: True)
        new.acquire()
        if path.read_text().split()[0] != "7":
            return "устаревшая по времени блокировка должна быть перехвачена"
        new.release()
        path.write_text("5 9990.0\n")  # свежая, но процесс мёртв
        dead = lock(8, lambda p: p != 5)
        dead.acquire()
        if path.read_text().split()[0] != "8":
            return "блокировка мёртвого процесса должна быть перехвачена"
        dead.release()
        path.write_text("5 9990.0\n")  # свежая и процесс жив
        try:
            lock(9, lambda p: True).acquire()
        except m.LockHeldError:
            pass
        else:
            return "свежая блокировка живого процесса не должна перехватываться"
        path.write_text("мусор")
        try:
            lock(9, lambda p: False).acquire()
        except m.LockHeldError:
            pass
        else:
            return "нечитаемое содержимое должно давать LockHeldError, а не перехватываться"
        if path.read_text() != "мусор":
            return "файл с непонятным содержимым нельзя удалять"

    return workdir(body)


def t_resume():
    def body(root):
        cp = root / "cp.json"
        items = list("abcdef")
        seen = []

        def handler(item):
            if item == "d" and "boom" not in seen:
                seen.append("boom")
                raise OSError("временный сбой")
            seen.append(item)

        try:
            m.process_with_checkpoint(items, handler, cp)
        except OSError:
            pass
        else:
            return "исключение handler должно пробрасываться"
        bad = expect(m.load_checkpoint(cp), {"done": 3}, "контрольная точка после сбоя на 'd'")
        if bad:
            return bad
        count = m.process_with_checkpoint(items, handler, cp)
        bad = expect((count, [x for x in seen if x != "boom"]), (3, list("abcdef")), "повторный запуск -> (обработано, порядок)")
        if bad:
            return bad
        bad = expect(m.process_with_checkpoint(items, handler, cp), 0, "запуск после завершения")
        if bad:
            return bad
        return expect(m.process_with_checkpoint([], handler, root / "empty.json"), 0, "пустой список")

    return workdir(body)


def t_checkpoint_invalid():
    def body(root):
        cp = root / "bad.json"
        cp.write_text("{не json")
        for content in ["{не json", "[1, 2]", '{"done": "x"}', '{"done": 99}', '{"done": -1}']:
            cp.write_text(content)
            try:
                m.process_with_checkpoint([1, 2, 3], lambda i: None, cp)
            except ValueError:
                continue
            return f"контрольная точка {content!r} должна давать ValueError"
        if cp.read_text() != '{"done": -1}':
            return "повреждённую точку нельзя перезаписывать молча"

    return workdir(body)


case("lock-basic", t_lock_basic)
case("lock-context", t_lock_context)
case("next-run", t_next_run)
case("checkpoint-io", t_checkpoint_io)
case("lock-stale", t_lock_stale)
case("resume", t_resume)
case("checkpoint-invalid", t_checkpoint_invalid)
