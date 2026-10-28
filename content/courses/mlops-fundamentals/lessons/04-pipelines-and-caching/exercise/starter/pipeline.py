import hashlib
import json


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def sha(text):
    """SHA-256 строки (в UTF-8) в шестнадцатеричном виде."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def topo_order(steps):
    """Имена шагов в топологическом порядке; из готовых берётся первый по алфавиту."""
    # TODO: ValueError для повторяющихся имён и неизвестных зависимостей (в сообщении укажите имя)
    # TODO: алгоритм Кана: на каждом шаге брать первый по алфавиту из готовых (без непройденных зависимостей)
    # TODO: если порядок вышел короче числа шагов - в графе цикл (в том числе зависимость шага от самого себя): ValueError
    return sorted(step["name"] for step in steps)


def step_key(step, dep_keys):
    """Ключ кеша шага: 16 hex-символов от имени, версии, параметров и ключей зависимостей."""
    # TODO: payload = {"name": ..., "version": ..., "params": ..., "deps": {dep: dep_keys[dep] для dep в step["deps"]}}
    # TODO: sha(canonical_json(payload))[:16]
    return ""


def run_pipeline(steps, cache):
    """Выполняет шаги по порядку, пропуская те, чей ключ уже есть в cache (словарь ключ -> результат)."""
    # TODO: идти по topo_order(steps); ключ шага считать из ключей уже обработанных зависимостей
    # TODO: ключ в cache - взять результат (шаг cached), иначе вызвать fn(inputs, params), положить в cache (шаг executed)
    return {"outputs": {}, "keys": {}, "executed": [], "cached": []}
