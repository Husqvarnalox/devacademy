import hashlib
import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import pipeline as m


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


def ref_canonical(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def ref_key(step, dep_keys):
    payload = {
        "name": step["name"],
        "version": step["version"],
        "params": step["params"],
        "deps": {d: dep_keys[d] for d in step["deps"]},
    }
    return hashlib.sha256(ref_canonical(payload).encode("utf-8")).hexdigest()[:16]


def raises(exc_type, fn, *args):
    """Возвращает сообщение исключения, если оно нужного типа, иначе None."""
    try:
        fn(*args)
    except exc_type as exc:
        return str(exc)
    except Exception:
        return None
    return None


# ---- маленький ML-конвейер на чистом Python ----

XS = [1, 2, 3, 4, 5, 6, 7, 8]
YS = [2.1, 3.9, 6.2, 7.8, 10.1, 12.2, 13.9, 16.1]


def make_flow(calls, train_fraction=0.75, train_version="1", scale=1.0):
    """load -> split -> train -> evaluate, плюс независимая ветка stats (зависит только от load)."""

    def load(inputs, params):
        calls.setdefault("load", []).append(sorted(inputs))
        return {"x": XS, "y": [round(v * params["scale"], 6) for v in YS]}

    def split(inputs, params):
        calls.setdefault("split", []).append(sorted(inputs))
        data = inputs["load"]
        n = int(len(data["x"]) * params["train_fraction"])
        return {"train": [data["x"][:n], data["y"][:n]], "test": [data["x"][n:], data["y"][n:]]}

    def train(inputs, params):
        calls.setdefault("train", []).append(sorted(inputs))
        xs, ys = inputs["split"]["train"]
        return {"w": round(sum(x * y for x, y in zip(xs, ys)) / sum(x * x for x in xs), 6)}

    def evaluate(inputs, params):
        calls.setdefault("evaluate", []).append(sorted(inputs))
        xs, ys = inputs["split"]["test"]
        w = inputs["train"]["w"]
        return {"mse": round(sum((w * x - y) ** 2 for x, y in zip(xs, ys)) / len(xs), 6)}

    def stats(inputs, params):
        calls.setdefault("stats", []).append(sorted(inputs))
        ys = inputs["load"]["y"]
        return {"mean_y": round(sum(ys) / len(ys), 6)}

    return [
        # намеренно в перемешанном порядке: порядок в списке не должен влиять на результат
        {"name": "evaluate", "deps": ["train", "split"], "params": {}, "version": "1", "fn": evaluate},
        {"name": "stats", "deps": ["load"], "params": {}, "version": "1", "fn": stats},
        {"name": "train", "deps": ["split"], "params": {}, "version": train_version, "fn": train},
        {"name": "split", "deps": ["load"], "params": {"train_fraction": train_fraction}, "version": "1", "fn": split},
        {"name": "load", "deps": [], "params": {"scale": scale}, "version": "1", "fn": load},
    ]


def expected_outputs(train_fraction=0.75, scale=1.0):
    calls = {}
    steps = {s["name"]: s for s in make_flow(calls, train_fraction, scale=scale)}
    out = {}
    for name in ("load", "split", "train", "evaluate", "stats"):
        s = steps[name]
        out[name] = s["fn"]({d: out[d] for d in s["deps"]}, s["params"])
    return out


def calls_count(calls):
    return {name: len(v) for name, v in calls.items()}


ORDER = ["load", "split", "stats", "train", "evaluate"]


def t_topo_basic():
    steps = make_flow({})
    got = m.topo_order(steps)
    if got != ORDER:
        return f"topo_order вернула {got!r}, ожидалось {ORDER!r}: зависимости раньше зависимых шагов, из готовых - первый по алфавиту"
    got2 = m.topo_order(list(reversed(steps)))
    if got2 != ORDER:
        return "порядок шагов во входном списке не должен влиять на результат"
    abc = [{"name": n, "deps": [], "params": {}, "version": "1", "fn": None} for n in ("c", "a", "b")]
    if m.topo_order(abc) != ["a", "b", "c"]:
        return "независимые шаги идут по алфавиту"
    if m.topo_order([]) != []:
        return "пустой список шагов даёт пустой порядок"


def t_step_key():
    s = {"name": "train", "deps": ["split", "load"], "params": {"lr": 0.1, "epochs": 5}, "version": "2", "fn": None}
    dk = {"split": "aaaa", "load": "bbbb"}
    got = m.step_key(s, dk)
    if not isinstance(got, str) or len(got) != 16 or any(c not in "0123456789abcdef" for c in got):
        return f"ключ должен быть строкой из 16 строчных шестнадцатеричных символов, получили {got!r}"
    if got != ref_key(s, dk):
        return f"ключ {got!r} не совпадает с эталоном {ref_key(s, dk)!r}: хешируйте canonical_json от имени, версии, параметров и словаря ключей зависимостей"
    if m.step_key(s, {**dk, "unrelated": "zzzz"}) != got:
        return "в ключ входят только ключи зависимостей шага, лишние записи dep_keys игнорируются"
    s_swapped = {**s, "deps": ["load", "split"], "params": {"epochs": 5, "lr": 0.1}}
    if m.step_key(s_swapped, dk) != got:
        return "порядок зависимостей и порядок ключей параметров не должны влиять на ключ"
    variants = [
        ({**s, "name": "fit"}, dk),
        ({**s, "version": "3"}, dk),
        ({**s, "params": {"lr": 0.2, "epochs": 5}}, dk),
        (s, {**dk, "split": "cccc"}),
    ]
    for variant, keys in variants:
        if m.step_key(variant, keys) == got:
            return "ключ должен меняться при изменении имени, версии, параметров или ключа зависимости"
    leaf = {"name": "load", "deps": [], "params": {}, "version": "1", "fn": None}
    if m.step_key(leaf, {}) != ref_key(leaf, {}):
        return "шаг без зависимостей: ключ считается от пустого словаря deps"


def t_run_first():
    calls, cache = {}, {}
    steps = make_flow(calls)
    res = m.run_pipeline(steps, cache)
    if res["executed"] != ORDER or res["cached"] != []:
        return f"при пустом кеше выполняются все шаги в топологическом порядке: executed={res['executed']!r}, cached={res['cached']!r}"
    want = expected_outputs()
    if res["outputs"] != want:
        return f"outputs {res['outputs']!r} не совпадают с ожидаемыми {want!r}"
    keys = {}
    by_name = {s["name"]: s for s in steps}
    for name in ORDER:
        keys[name] = ref_key(by_name[name], keys)
    if res["keys"] != keys:
        return f"keys не совпадают с эталоном: ключ шага строится из ключей его зависимостей (step_key)"
    if calls_count(calls) != {n: 1 for n in ORDER}:
        return f"каждая fn должна быть вызвана один раз, вызовов: {calls_count(calls)!r}"
    if len(cache) != 5 or any(cache.get(keys[n]) != want[n] for n in ORDER):
        return "результаты каждого шага кладутся в переданный словарь cache по его ключу"
    wanted_inputs = {"load": [], "split": ["load"], "stats": ["load"], "train": ["split"], "evaluate": ["split", "train"]}
    got_inputs = {n: calls[n][0] for n in ORDER}
    if got_inputs != wanted_inputs:
        return f"fn получает в inputs только свои зависимости: получили ключи {got_inputs!r}, ожидалось {wanted_inputs!r}"


def t_run_second_cached():
    calls, cache = {}, {}
    steps = make_flow(calls)
    first = m.run_pipeline(steps, cache)
    before = calls_count(calls)
    second = m.run_pipeline(make_flow(calls), cache)
    if second["executed"] != [] or second["cached"] != ORDER:
        return f"повторный запуск без изменений берёт всё из кеша: executed={second['executed']!r}, cached={second['cached']!r}"
    if calls_count(calls) != before:
        return "при попадании в кеш функции шагов не вызываются"
    if second["outputs"] != first["outputs"] or second["keys"] != first["keys"]:
        return "результаты и ключи повторного запуска совпадают с первым"
    if len(cache) != 5:
        return f"кеш не должен расти при повторном запуске: записей {len(cache)}"


def t_topo_errors():
    def mk(name, deps):
        return {"name": name, "deps": deps, "params": {}, "version": "1", "fn": None}

    msg = raises(ValueError, m.topo_order, [mk("a", []), mk("b", ["ghost"])])
    if msg is None:
        return "неизвестная зависимость должна приводить к ValueError"
    if "ghost" not in msg:
        return f"в сообщении об ошибке должно быть имя неизвестной зависимости 'ghost', получили {msg!r}"
    cycles = {
        "двух шагов": [mk("a", ["b"]), mk("b", ["a"])],
        "трёх шагов": [mk("a", ["c"]), mk("b", ["a"]), mk("c", ["b"]), mk("ok", [])],
        "самозависимость": [mk("x", ["x"])],
    }
    for title, steps in cycles.items():
        if raises(ValueError, m.topo_order, steps) is None:
            return f"цикл ({title}) должен приводить к ValueError"
    if raises(ValueError, m.topo_order, [mk("a", []), mk("a", [])]) is None:
        return "повторяющееся имя шага должно приводить к ValueError"
    calls = {}
    broken = make_flow(calls)
    broken.append({"name": "bad", "deps": ["missing"], "params": {}, "version": "1", "fn": lambda i, p: 1})
    if raises(ValueError, m.run_pipeline, broken, {}) is None:
        return "run_pipeline сообщает об ошибке графа"
    if calls:
        return "ошибка в графе должна обнаруживаться до запуска первого шага: ни одна fn не должна вызываться"


def t_invalidate_params():
    calls, cache = {}, {}
    m.run_pipeline(make_flow(calls), cache)
    base = calls_count(calls)
    res = m.run_pipeline(make_flow(calls, train_fraction=0.5), cache)
    if res["executed"] != ["split", "train", "evaluate"]:
        return f"изменение параметров split пересчитывает split и всё, что зависит от него: executed={res['executed']!r}"
    if res["cached"] != ["load", "stats"]:
        return f"load и независимая ветка stats берутся из кеша: cached={res['cached']!r}"
    after = calls_count(calls)
    if after["load"] != base["load"] or after["stats"] != base["stats"]:
        return "функции load и stats не должны вызываться повторно"
    if after["split"] != 2 or after["train"] != 2 or after["evaluate"] != 2:
        return f"split, train и evaluate должны быть вызваны ещё по разу, вызовов: {after!r}"
    want = expected_outputs(train_fraction=0.5)
    if res["outputs"] != want:
        return f"outputs после смены параметров неверны: {res['outputs']!r}"
    back = m.run_pipeline(make_flow(calls), cache)
    if back["executed"] != [] or back["cached"] != ORDER:
        return f"возврат к прежним параметрам берёт старые результаты из кеша: executed={back['executed']!r}"
    root = m.run_pipeline(make_flow(calls, scale=2.0), cache)
    if root["executed"] != ORDER or root["cached"] != []:
        return f"изменение параметров корневого шага пересчитывает весь граф: executed={root['executed']!r}"


def t_invalidate_version():
    calls, cache = {}, {}
    m.run_pipeline(make_flow(calls), cache)
    res = m.run_pipeline(make_flow(calls, train_version="2"), cache)
    if res["executed"] != ["train", "evaluate"] or res["cached"] != ["load", "split", "stats"]:
        return f"новая version у train пересчитывает train и evaluate: executed={res['executed']!r}, cached={res['cached']!r}"
    if res["outputs"] != expected_outputs():
        return "результаты не должны измениться: версия поменялась, а функция та же"
    again = m.run_pipeline(make_flow(calls, train_version="2"), cache)
    if again["executed"] != []:
        return "после пересчёта новая версия закеширована"
    old = m.run_pipeline(make_flow(calls, train_version="1"), cache)
    if old["executed"] != []:
        return "старая версия остаётся в кеше: возврат к version '1' ничего не пересчитывает"
    other_cache = {}
    fresh = m.run_pipeline(make_flow(calls), other_cache)
    if fresh["executed"] != ORDER:
        return "другой пустой кеш - всё выполняется заново"


def t_diamond_and_shared_cache():
    log = []

    def step(name, deps, version="1", params=None):
        def fn(inputs, p):
            log.append(name)
            return {"name": name, "sum": sum(v["sum"] for v in inputs.values()) + 1}
        return {"name": name, "deps": deps, "params": params or {}, "version": version, "fn": fn}

    steps = [
        step("sink", ["left", "right", "bottom"]),
        step("left", ["root"]),
        step("right", ["root"]),
        step("bottom", ["left"]),
        step("root", []),
        step("alpha", []),
    ]
    order = m.topo_order(steps)
    want_order = ["alpha", "root", "left", "bottom", "right", "sink"]
    if order != want_order:
        return f"topo_order для ромбовидного графа вернула {order!r}, ожидалось {want_order!r} (после каждого шага снова выбирается первый по алфавиту из готовых)"
    cache = {}
    res = m.run_pipeline(steps, cache)
    if res["executed"] != want_order or log != want_order:
        return f"шаги выполняются в топологическом порядке: executed={res['executed']!r}, вызовы={log!r}"
    if res["outputs"]["sink"] != {"name": "sink", "sum": 8}:
        return f"sink получает результаты всех трёх зависимостей: {res['outputs']['sink']!r}"
    log.clear()
    # меняем версию 'left': пересчитываются left, bottom, sink; root, right, alpha - из кеша
    steps2 = [dict(s) for s in steps]
    for s in steps2:
        if s["name"] == "left":
            s["version"] = "2"
    res2 = m.run_pipeline(steps2, cache)
    if res2["executed"] != ["left", "bottom", "sink"] or res2["cached"] != ["alpha", "root", "right"]:
        return f"после смены версии left: executed={res2['executed']!r}, cached={res2['cached']!r}, ожидалось executed=['left', 'bottom', 'sink'], cached=['alpha', 'root', 'right']"
    if log != ["left", "bottom", "sink"]:
        return f"вызваны функции {log!r}, ожидалось ['left', 'bottom', 'sink']"
    # кеш общий для разных графов: тот же шаг с теми же входами берётся из кеша
    other = [step("root", []), step("extra", ["root"])]
    log.clear()
    res3 = m.run_pipeline(other, cache)
    if res3["cached"] != ["root"] or res3["executed"] != ["extra"]:
        return f"шаг root с теми же именем, версией, параметрами и зависимостями уже в кеше: cached={res3['cached']!r}, executed={res3['executed']!r}"


case("topo-basic", t_topo_basic)
case("step-key", t_step_key)
case("run-first", t_run_first)
case("run-second-cached", t_run_second_cached)
case("topo-errors", t_topo_errors)
case("invalidate-params", t_invalidate_params)
case("invalidate-version", t_invalidate_version)
case("diamond-and-shared-cache", t_diamond_and_shared_cache)
