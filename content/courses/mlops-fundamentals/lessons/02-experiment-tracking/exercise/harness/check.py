import json
import os
import shutil
import tempfile
from pathlib import Path

NONCE = os.environ["DEVACADEMY_NONCE"]

import tracking as m


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


def canon(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def workdir():
    return Path(tempfile.mkdtemp(prefix="trk_", dir=os.getcwd()))


def with_root(body):
    root = workdir()
    try:
        return body(root)
    finally:
        shutil.rmtree(root, ignore_errors=True)


def make_run(root, run_id, status="FINISHED", points=(), params=None, tags=None):
    """Пишет файлы запуска напрямую, не используя функции ученика."""
    d = Path(root) / run_id
    d.mkdir(parents=True)
    (d / "params.json").write_text(canon(params or {}), encoding="utf-8")
    (d / "meta.json").write_text(canon({"run_id": run_id, "status": status, "tags": tags or {}}), encoding="utf-8")
    if points:
        lines = [canon({"key": k, "step": s, "value": v}) + "\n" for k, s, v in points]
        (d / "metrics.jsonl").write_text("".join(lines), encoding="utf-8")


def raises(exc_type, fn, *args):
    try:
        fn(*args)
    except exc_type:
        return True
    except Exception:
        return False
    return False


def t_start_run_files():
    def body(root):
        d = m.start_run(root, "run-1", {"lr": 0.1, "epochs": 5}, {"owner": "ana"})
        if Path(d) != root / "run-1":
            return f"start_run вернула {d!r}, ожидался путь {root / 'run-1'}"
        p = root / "run-1" / "params.json"
        meta = root / "run-1" / "meta.json"
        if not p.is_file() or not meta.is_file():
            return "в каталоге запуска должны появиться params.json и meta.json"
        want_p = '{"epochs":5,"lr":0.1}'
        if p.read_text(encoding="utf-8") != want_p:
            return f"params.json содержит {p.read_text(encoding='utf-8')!r}, ожидалось {want_p!r} (canonical_json)"
        want_m = '{"run_id":"run-1","status":"RUNNING","tags":{"owner":"ana"}}'
        if meta.read_text(encoding="utf-8") != want_m:
            return f"meta.json содержит {meta.read_text(encoding='utf-8')!r}, ожидалось {want_m!r}"
        m.start_run(root, "run-2", {})
        if (root / "run-2" / "meta.json").read_text(encoding="utf-8") != '{"run_id":"run-2","status":"RUNNING","tags":{}}':
            return "без tags в meta.json должен быть пустой объект tags"
        m.start_run(root, "deep/run-3", {"a": 1})
        if not (root / "deep" / "run-3" / "params.json").is_file():
            return "родительские каталоги создаются автоматически (parents=True)"
    return with_root(body)


def t_log_metric_lines():
    def body(root):
        m.start_run(root, "r", {})
        m.log_metric(root, "r", "loss", 0.5, 0)
        m.log_metric(root, "r", "acc", 1, 0)
        m.log_metric(root, "r", "loss", 0.25, 1)
        text = (root / "r" / "metrics.jsonl").read_text(encoding="utf-8")
        want = (
            '{"key":"loss","step":0,"value":0.5}\n'
            '{"key":"acc","step":0,"value":1}\n'
            '{"key":"loss","step":1,"value":0.25}\n'
        )
        if text != want:
            return f"metrics.jsonl содержит {text!r}, ожидалось {want!r}: по одной строке canonical_json на вызов, дописывая в конец"
    return with_root(body)


def t_load_run_history():
    def body(root):
        make_run(root, "a", "FINISHED",
                 [("loss", 0, 0.9), ("loss", 1, 0.5), ("acc", 1, 0.7), ("loss", 2, 0.3), ("acc", 2, 0.8)],
                 {"lr": 0.1}, {"team": "ml"})
        run = m.load_run(root, "a")
        want = {
            "run_id": "a", "status": "FINISHED", "tags": {"team": "ml"}, "params": {"lr": 0.1},
            "metrics": {"loss": 0.3, "acc": 0.8},
            "history": {"loss": [[0, 0.9], [1, 0.5], [2, 0.3]], "acc": [[1, 0.7], [2, 0.8]]},
        }
        if run != want:
            return f"load_run вернула {run!r}, ожидалось {want!r}"
        make_run(root, "empty", "RUNNING")
        e = m.load_run(root, "empty")
        if e["metrics"] != {} or e["history"] != {}:
            return "без metrics.jsonl metrics и history - пустые словари"
    return with_root(body)


def t_best_run_basic():
    def body(root):
        make_run(root, "r1", "FINISHED", [("acc", 0, 0.70)])
        make_run(root, "r2", "FINISHED", [("acc", 0, 0.60), ("acc", 1, 0.90)])
        make_run(root, "r3", "FINISHED", [("acc", 0, 0.80)])
        got = m.best_run(root, "acc")
        if got != "r2":
            return f"best_run вернула {got!r}, ожидалось 'r2': сравниваются последние значения метрики (0.9 > 0.8 > 0.7)"
        if m.best_run(root, "f1") is not None:
            return "ни у одного запуска нет метрики f1: ожидалось None"
        empty = root / "nothing"
        empty.mkdir()
        if m.best_run(empty, "acc") is not None:
            return "в пустом каталоге запусков ожидалось None"
    return with_root(body)


def t_run_errors():
    def body(root):
        m.start_run(root, "r", {"a": 1})
        before = (root / "r" / "params.json").read_text(encoding="utf-8")
        if not raises(FileExistsError, m.start_run, root, "r", {"a": 2}):
            return "повторный start_run с тем же run_id должен бросать FileExistsError"
        if (root / "r" / "params.json").read_text(encoding="utf-8") != before:
            return "повторный start_run не должен менять файлы существующего запуска"
        if not raises(FileNotFoundError, m.log_metric, root, "ghost", "loss", 1.0, 0):
            return "log_metric в несуществующий запуск должен бросать FileNotFoundError"
        if (root / "ghost").exists():
            return "log_metric не должен создавать каталог запуска"
        bad_values = [float("nan"), float("inf"), float("-inf"), True, "0.5", None]
        for bad in bad_values:
            if not raises(ValueError, m.log_metric, root, "r", "loss", bad, 0):
                return f"value={bad!r} должен приводить к ValueError"
        for bad in (-1, True, 1.5, "1", None):
            if not raises(ValueError, m.log_metric, root, "r", "loss", 0.5, bad):
                return f"step={bad!r} должен приводить к ValueError (нужен int >= 0, не bool)"
        if (root / "r" / "metrics.jsonl").exists():
            return "после отклонённых вызовов в metrics.jsonl не должно быть записей"
        m.log_metric(root, "r", "loss", 3, 0)
        m.log_metric(root, "r", "loss", -0.5, 0)
        if len((root / "r" / "metrics.jsonl").read_text(encoding="utf-8").splitlines()) != 2:
            return "целые и отрицательные конечные значения допустимы"
        for bad in ("DONE", "finished", "RUNNING", None):
            if not raises(ValueError, m.end_run, root, "r", bad):
                return f"статус {bad!r} недопустим: разрешены только FINISHED и FAILED"
        if '"status":"RUNNING"' not in (root / "r" / "meta.json").read_text(encoding="utf-8"):
            return "после отклонённого end_run статус остаётся RUNNING"
    return with_root(body)


def t_status_equal_steps_unicode():
    def body(root):
        m.start_run(root, "r", {"модель": "логрегрессия", "lr": 0.1}, {"автор": "Ана"})
        for key, value, step in [("loss", 0.5, 2), ("loss", 0.9, 0), ("loss", 0.7, 1), ("loss", 0.4, 2), ("loss", 0.45, 2)]:
            m.log_metric(root, "r", key, value, step)
        m.end_run(root, "r", "FAILED")
        run = m.load_run(root, "r")
        if run["status"] != "FAILED":
            return f"после end_run(..., 'FAILED') статус {run['status']!r}"
        if run["tags"] != {"автор": "Ана"} or run["params"] != {"lr": 0.1, "модель": "логрегрессия"}:
            return f"кириллица в тегах и параметрах должна читаться без искажений, получили {run['tags']!r} и {run['params']!r}"
        want_hist = [[0, 0.9], [1, 0.7], [2, 0.5], [2, 0.4], [2, 0.45]]
        if run["history"]["loss"] != want_hist:
            return f"history['loss'] = {run['history']['loss']!r}, ожидалось {want_hist!r}: сортировка по step, при равных шагах - порядок строк файла"
        if run["metrics"]["loss"] != 0.45:
            return f"metrics['loss'] = {run['metrics']['loss']!r}, ожидалось 0.45: при равных шагах побеждает более поздняя строка"
        if "модель" not in (root / "r" / "params.json").read_text(encoding="utf-8"):
            return "в params.json кириллица пишется как есть (ensure_ascii=False)"
        m.end_run(root, "r", "FINISHED")
        again = m.load_run(root, "r")
        if again["status"] != "FINISHED" or again["tags"] != run["tags"]:
            return "end_run сохраняет теги и может вызываться повторно"
    return with_root(body)


def t_best_run_rules():
    def body(root):
        make_run(root, "b", "FINISHED", [("acc", 0, 0.8), ("loss", 0, 0.30)])
        make_run(root, "a", "FINISHED", [("acc", 0, 0.8), ("loss", 0, 0.30)])
        make_run(root, "c", "FINISHED", [("acc", 0, 0.7), ("loss", 0, 0.10)])
        make_run(root, "z-failed", "FAILED", [("acc", 0, 0.99), ("loss", 0, 0.01)])
        make_run(root, "y-running", "RUNNING", [("acc", 0, 0.98), ("loss", 0, 0.02)])
        make_run(root, "d-no-loss", "FINISHED", [("acc", 0, 0.5)])
        got = m.best_run(root, "acc")
        if got != "a":
            return f"best_run(acc) вернула {got!r}, ожидалось 'a': FAILED и RUNNING игнорируются, при равенстве побеждает первый по алфавиту"
        got = m.best_run(root, "loss", mode="min")
        if got != "c":
            return f"best_run(loss, min) вернула {got!r}, ожидалось 'c' (0.10; запуск без loss пропускается)"
        got = m.best_run(root, "loss", "max")
        if got != "a":
            return f"best_run(loss, max) вернула {got!r}, ожидалось 'a' (0.30 у a и b, a раньше по алфавиту)"
        if not raises(ValueError, m.best_run, root, "acc", "avg"):
            return "mode, отличный от max и min, должен приводить к ValueError"
        # последнее значение, а не лучшее за всё время
        make_run(root, "e", "FINISHED", [("acc", 0, 0.99), ("acc", 1, 0.10)])
        if m.best_run(root, "acc") != "a":
            return "для запуска учитывается значение на последнем шаге (0.10), а не максимум по всей кривой"
    return with_root(body)


def t_compare_runs():
    def body(root):
        make_run(root, "a", "FINISHED", [("acc", 0, 0.7), ("loss", 0, 0.5)], {"lr": 0.1, "epochs": 5, "opt": "sgd"})
        make_run(root, "b", "FINISHED", [("acc", 0, 0.8), ("f1", 0, 0.6)], {"lr": 0.01, "epochs": 5, "wd": 0.1})
        got = m.compare_runs(root, "a", "b")
        want = {
            "params": {"lr": [0.1, 0.01], "opt": ["sgd", None], "wd": [None, 0.1]},
            "metrics": {"acc": [0.7, 0.8], "f1": [None, 0.6], "loss": [0.5, None]},
        }
        if got != want:
            return f"compare_runs вернула {got!r}, ожидалось {want!r}"
        for part in ("params", "metrics"):
            if list(got[part]) != sorted(got[part]):
                return f"ключи {part} должны идти в алфавитном порядке, получили {list(got[part])!r}"
        same = m.compare_runs(root, "a", "a")
        if same["params"] != {}:
            return "у одинаковых запусков словарь params пустой"
        if same["metrics"] != {"acc": [0.7, 0.7], "loss": [0.5, 0.5]}:
            return "metrics содержит все ключи объединения, даже если значения равны"
    return with_root(body)


case("start-run-files", t_start_run_files)
case("log-metric-lines", t_log_metric_lines)
case("load-run-history", t_load_run_history)
case("best-run-basic", t_best_run_basic)
case("run-errors", t_run_errors)
case("status-equal-steps-unicode", t_status_equal_steps_unicode)
case("best-run-rules", t_best_run_rules)
case("compare-runs", t_compare_runs)
