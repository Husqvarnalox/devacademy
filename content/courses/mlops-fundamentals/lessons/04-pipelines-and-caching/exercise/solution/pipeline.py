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
    names = [step["name"] for step in steps]
    if len(set(names)) != len(names):
        duplicates = sorted({n for n in names if names.count(n) > 1})
        raise ValueError(f"повторяющиеся имена шагов: {duplicates}")
    by_name = {step["name"]: step for step in steps}
    pending = {}
    children = {name: [] for name in by_name}
    for name, step in by_name.items():
        deps = set(step["deps"])
        for dep in sorted(deps):
            if dep not in by_name:
                raise ValueError(f"шаг {name!r} зависит от неизвестного шага {dep!r}")
            children[dep].append(name)
        pending[name] = len(deps)
    ready = sorted(name for name, count in pending.items() if count == 0)
    order = []
    while ready:
        name = ready.pop(0)
        order.append(name)
        for child in children[name]:
            pending[child] -= 1
            if pending[child] == 0:
                ready.append(child)
        ready.sort()
    if len(order) != len(by_name):
        stuck = sorted(set(by_name) - set(order))
        raise ValueError(f"в графе шагов есть цикл, затронуты: {stuck}")
    return order


def step_key(step, dep_keys):
    """Ключ кеша шага: 16 hex-символов от имени, версии, параметров и ключей зависимостей."""
    payload = {
        "name": step["name"],
        "version": step["version"],
        "params": step["params"],
        "deps": {dep: dep_keys[dep] for dep in step["deps"]},
    }
    return sha(canonical_json(payload))[:16]


def run_pipeline(steps, cache):
    """Выполняет шаги по порядку, пропуская те, чей ключ уже есть в cache (словарь ключ -> результат)."""
    by_name = {step["name"]: step for step in steps}
    outputs, keys, executed, cached = {}, {}, [], []
    for name in topo_order(steps):
        step = by_name[name]
        key = step_key(step, keys)
        if key in cache:
            output = cache[key]
            cached.append(name)
        else:
            inputs = {dep: outputs[dep] for dep in step["deps"]}
            output = step["fn"](inputs, step["params"])
            cache[key] = output
            executed.append(name)
        outputs[name] = output
        keys[name] = key
    return {"outputs": outputs, "keys": keys, "executed": executed, "cached": cached}
