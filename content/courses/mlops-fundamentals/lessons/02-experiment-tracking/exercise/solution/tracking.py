import json
import math
from pathlib import Path


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def _read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _write_meta(run_dir, run_id, status, tags):
    meta = {"run_id": run_id, "status": status, "tags": tags}
    (run_dir / "meta.json").write_text(canonical_json(meta), encoding="utf-8")


def start_run(root, run_id, params, tags=None):
    """Создаёт каталог запуска root/run_id с params.json и meta.json (статус RUNNING). Возвращает Path каталога."""
    run_dir = Path(root) / run_id
    run_dir.mkdir(parents=True)  # FileExistsError, если запуск уже есть
    (run_dir / "params.json").write_text(canonical_json(params), encoding="utf-8")
    _write_meta(run_dir, run_id, "RUNNING", tags or {})
    return run_dir


def log_metric(root, run_id, key, value, step):
    """Дописывает одну строку JSON в metrics.jsonl запуска."""
    run_dir = Path(root) / run_id
    if not run_dir.is_dir():
        raise FileNotFoundError(f"запуск {run_id!r} не найден в {root}")
    if isinstance(step, bool) or not isinstance(step, int) or step < 0:
        raise ValueError(f"step должен быть целым числом >= 0, получено {step!r}")
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"value должен быть конечным числом, получено {value!r}")
    line = canonical_json({"key": key, "step": step, "value": value}) + "\n"
    with open(run_dir / "metrics.jsonl", "a", encoding="utf-8") as f:
        f.write(line)


def end_run(root, run_id, status):
    """Переписывает meta.json с новым статусом (теги сохраняются)."""
    if status not in ("FINISHED", "FAILED"):
        raise ValueError(f"статус должен быть FINISHED или FAILED, получено {status!r}")
    run_dir = Path(root) / run_id
    meta = _read_json(run_dir / "meta.json")
    _write_meta(run_dir, run_id, status, meta["tags"])


def load_run(root, run_id):
    """Собирает запуск из файлов: run_id, status, tags, params, metrics (последние значения), history (кривые)."""
    run_dir = Path(root) / run_id
    meta = _read_json(run_dir / "meta.json")
    history = {}
    metrics_file = run_dir / "metrics.jsonl"
    if metrics_file.exists():
        for line in metrics_file.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            rec = json.loads(line)
            history.setdefault(rec["key"], []).append([rec["step"], rec["value"]])
    for points in history.values():
        points.sort(key=lambda p: p[0])  # сортировка устойчива: при равных шагах порядок строк сохраняется
    metrics = {key: points[-1][1] for key, points in history.items()}
    return {
        "run_id": meta["run_id"],
        "status": meta["status"],
        "tags": meta["tags"],
        "params": _read_json(run_dir / "params.json"),
        "metrics": metrics,
        "history": history,
    }


def best_run(root, metric, mode="max"):
    """run_id лучшего завершённого запуска по метрике или None."""
    if mode not in ("max", "min"):
        raise ValueError(f"mode должен быть max или min, получено {mode!r}")
    root = Path(root)
    best_id, best_value = None, None
    if not root.is_dir():
        return None
    for run_dir in sorted(p for p in root.iterdir() if (p / "meta.json").is_file()):
        run = load_run(root, run_dir.name)
        if run["status"] != "FINISHED" or metric not in run["metrics"]:
            continue
        value = run["metrics"][metric]
        better = best_value is None or (value > best_value if mode == "max" else value < best_value)
        if better:  # строгое сравнение: при равенстве остаётся первый по алфавиту
            best_id, best_value = run_dir.name, value
    return best_id


def compare_runs(root, run_a, run_b):
    """Различия двух запусков: params (только отличающиеся ключи) и metrics (все ключи), значения [a, b]."""
    a, b = load_run(root, run_a), load_run(root, run_b)
    params = {}
    for key in sorted(set(a["params"]) | set(b["params"])):
        va, vb = a["params"].get(key), b["params"].get(key)
        if va != vb:
            params[key] = [va, vb]
    metrics = {}
    for key in sorted(set(a["metrics"]) | set(b["metrics"])):
        metrics[key] = [a["metrics"].get(key), b["metrics"].get(key)]
    return {"params": params, "metrics": metrics}
