import json
import math
from pathlib import Path


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def _read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def start_run(root, run_id, params, tags=None):
    """Создаёт каталог запуска root/run_id с params.json и meta.json (статус RUNNING). Возвращает Path каталога."""
    # TODO: mkdir(parents=True) - если каталог уже есть, пусть вылетит FileExistsError
    # TODO: записать params.json = canonical_json(params)
    # TODO: записать meta.json = canonical_json({"run_id": ..., "status": "RUNNING", "tags": tags or {}})
    return Path(root) / run_id


def log_metric(root, run_id, key, value, step):
    """Дописывает одну строку JSON в metrics.jsonl запуска."""
    # TODO: FileNotFoundError, если каталога запуска нет
    # TODO: ValueError для step (не int, bool или < 0) и для value (не число, bool, NaN, inf)
    # TODO: дописать canonical_json({"key": key, "step": step, "value": value}) + "\n" в режиме "a"
    return None


def end_run(root, run_id, status):
    """Переписывает meta.json с новым статусом (теги сохраняются)."""
    # TODO: ValueError, если status не FINISHED и не FAILED
    # TODO: прочитать meta.json, заменить status, записать обратно
    return None


def load_run(root, run_id):
    """Собирает запуск из файлов: run_id, status, tags, params, metrics (последние значения), history (кривые)."""
    # TODO: прочитать meta.json и params.json
    # TODO: разобрать metrics.jsonl (файла может не быть), сгруппировать по key, отсортировать по step
    # TODO: metrics[key] - значение последней точки history[key]
    return {"run_id": run_id, "status": "", "tags": {}, "params": {}, "metrics": {}, "history": {}}


def best_run(root, metric, mode="max"):
    """run_id лучшего завершённого запуска по метрике или None."""
    # TODO: ValueError для mode не max/min
    # TODO: обойти каталоги запусков в отсортированном порядке, учитывать только FINISHED с этой метрикой
    # TODO: сравнивать строго, чтобы при равенстве победил первый по алфавиту
    return None


def compare_runs(root, run_a, run_b):
    """Различия двух запусков: params (только отличающиеся ключи) и metrics (все ключи), значения [a, b]."""
    # TODO: объединение ключей, отсутствующее значение - None, ключи результата по алфавиту
    return {"params": {}, "metrics": {}}
