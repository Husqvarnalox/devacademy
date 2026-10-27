import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import serving as m


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


# ---------- эталонная реализация ----------

def ref_validate(row, schema):
    if not isinstance(row, dict):
        return ["row: expected object"]
    out = []
    for f in sorted(schema):
        if f not in row:
            out.append(f + ": missing")
            continue
        v = row[f]
        if type(v) not in (int, float):
            out.append(f + ": expected number")
            continue
        if math.isnan(v) or math.isinf(v):
            out.append(f + ": expected finite number")
            continue
        lo, hi = schema[f].get("min"), schema[f].get("max")
        if (lo is not None and v < lo) or (hi is not None and v > hi):
            out.append(f + ": out of range")
    for k in sorted(set(row) - set(schema)):
        out.append(k + ": unknown field")
    return out


def ref_p(z):
    if z >= 0:
        return 1 / (1 + math.exp(-z))
    e = math.exp(z)
    return e / (1 + e)


def ref_predict(model, row):
    z = model["bias"] + sum(w * row[f] for f, w in zip(model["features"], model["weights"]))
    p = ref_p(z)
    return {"score": round(p, 6), "label": 1 if p >= model["threshold"] else 0}


def ref_batch(model, schema, rows, size):
    results = []
    for i, row in enumerate(rows):
        errs = ref_validate(row, schema)
        if errs:
            results.append({"index": i, "ok": False, "errors": errs})
        else:
            results.append({"index": i, "ok": True, **ref_predict(model, row)})
    return {"model": f"{model['name']}:{model['version']}", "batches": -(-len(rows) // size), "results": results}


MODEL = {"name": "churn", "version": 3, "features": ["age", "income_k"], "weights": [0.04, 0.02], "bias": -3.0, "threshold": 0.5}
SCHEMA = {"age": {"min": 18, "max": 100}, "income_k": {"min": 0, "max": None}}
GOOD = {"age": 40, "income_k": 50}


def rows(n):
    return [{"age": 20 + (i * 7) % 60, "income_k": (i * 13) % 90 + 0.5} for i in range(n)]


def close(a, b):
    return abs(a - b) <= 1e-9


# ---------- публичные ----------

def t_validate_basic():
    if m.validate_row(GOOD, SCHEMA) != []:
        return f"корректная строка: ожидался пустой список, получили {m.validate_row(GOOD, SCHEMA)!r}"
    got = m.validate_row({"age": 40}, SCHEMA)
    if got != ["income_k: missing"]:
        return f"нет признака income_k: ожидалось ['income_k: missing'], получили {got!r}"
    got = m.validate_row({"age": "40", "income_k": 50}, SCHEMA)
    if got != ["age: expected number"]:
        return f"строка вместо числа: ожидалось ['age: expected number'], получили {got!r}"
    got = m.validate_row({"age": 10, "income_k": -1}, SCHEMA)
    if got != ["age: out of range", "income_k: out of range"]:
        return f"значения вне границ: получили {got!r}; ошибки идут в алфавитном порядке признаков"
    got = m.validate_row({"age": 40, "income_k": 50, "zeta": 1, "alpha": 2}, SCHEMA)
    if got != ["alpha: unknown field", "zeta: unknown field"]:
        return f"лишние поля: получили {got!r}; они идут после остальных ошибок в алфавитном порядке"
    for bad in ([], "text", None, 5):
        if m.validate_row(bad, SCHEMA) != ["row: expected object"]:
            return f"строка {bad!r} не объект: ожидалось ['row: expected object'], получили {m.validate_row(bad, SCHEMA)!r}"


def t_predict_one():
    for row in ({"age": 40, "income_k": 50}, {"age": 60, "income_k": 80}, {"age": 18, "income_k": 0}, {"age": 25, "income_k": 12.5}):
        got = m.predict_one(MODEL, row)
        want = ref_predict(MODEL, row)
        if set(got) != {"score", "label"}:
            return f"в ответе должны быть ключи score и label, получили {sorted(got)!r}"
        if not close(got["score"], want["score"]) or got["label"] != want["label"]:
            return f"для {row} ожидалось {want}, получили {got}"
    model = {**MODEL, "features": ["a", "b", "c"], "weights": [1.0, -2.0, 0.5], "bias": 0.25}
    row = {"a": 0.3, "b": 0.1, "c": 1.2}
    if m.predict_one(model, row) != ref_predict(model, row):
        return f"три признака: ожидалось {ref_predict(model, row)}, получили {m.predict_one(model, row)}"


def t_batch_basic():
    data = [GOOD, {"age": 40}, {"age": 60, "income_k": 80}]
    got = m.predict_batch(MODEL, SCHEMA, data, 2)
    want = ref_batch(MODEL, SCHEMA, data, 2)
    if got["model"] != "churn:3":
        return f"поле model должно быть 'churn:3', получили {got.get('model')!r}"
    if got["batches"] != 2:
        return f"3 строки по 2 в пачке - это 2 пачки, получили {got['batches']!r}"
    if len(got["results"]) != 3:
        return f"результатов должно быть 3 (по одному на строку), получили {len(got['results'])}"
    r = got["results"]
    if r[1] != {"index": 1, "ok": False, "errors": ["income_k: missing"]}:
        return f"плохая строка должна дать {{'index': 1, 'ok': False, 'errors': [...]}}, получили {r[1]!r}"
    for i in (0, 2):
        if not r[i].get("ok") or r[i].get("index") != i or not close(r[i]["score"], want["results"][i]["score"]) or r[i]["label"] != want["results"][i]["label"]:
            return f"строка {i}: ожидалось {want['results'][i]}, получили {r[i]}"


def t_request_statuses():
    ok_body = json.dumps({"instances": [GOOD, {"age": 5, "income_k": 1}]})
    status, payload = m.handle_request(MODEL, SCHEMA, ok_body)
    if status != 200 or payload.get("batches") != 1 or len(payload.get("results", [])) != 2:
        return f"корректный запрос: ожидался статус 200 и одна пачка, получили {status}, {payload!r}"
    if m.handle_request(MODEL, SCHEMA, "{not json") != (400, {"error": "invalid json"}):
        return f"неверный JSON: ожидалось (400, {{'error': 'invalid json'}}), получили {m.handle_request(MODEL, SCHEMA, '{not json')!r}"
    shape = (400, {"error": 'expected {"instances": [...]}'})
    for body in ('{"rows": []}', "[1, 2]", '{"instances": 5}'):
        if m.handle_request(MODEL, SCHEMA, body) != shape:
            return f"тело {body}: ожидалось {shape!r}, получили {m.handle_request(MODEL, SCHEMA, body)!r}"
    big = json.dumps({"instances": rows(5)})
    got = m.handle_request(MODEL, SCHEMA, big, max_instances=4)
    if got != (413, {"error": "too many instances", "limit": 4}):
        return f"5 строк при лимите 4: ожидалось (413, {{'error': 'too many instances', 'limit': 4}}), получили {got!r}"


# ---------- скрытые ----------

def t_validate_edges():
    nan, inf = float("nan"), float("inf")
    got = m.validate_row({"age": nan, "income_k": inf}, SCHEMA)
    if got != ["age: expected finite number", "income_k: expected finite number"]:
        return f"NaN и бесконечность: ожидалось 'expected finite number' для обоих, получили {got!r}"
    got = m.validate_row({"age": True, "income_k": False}, SCHEMA)
    if got != ["age: expected number", "income_k: expected number"]:
        return f"bool не считается числом: получили {got!r}"
    got = m.validate_row({"age": None, "income_k": [1]}, SCHEMA)
    if got != ["age: expected number", "income_k: expected number"]:
        return f"None и список - не числа: получили {got!r}"
    if m.validate_row({"age": 18, "income_k": 0}, SCHEMA) != [] or m.validate_row({"age": 100, "income_k": 10 ** 9}, SCHEMA) != []:
        return "границы включительно: 18 и 100 допустимы, а отсутствие max (None) означает «без верхней границы»"
    if m.validate_row({"age": 17.999, "income_k": 0}, SCHEMA) != ["age: out of range"] or m.validate_row({"age": 100.001, "income_k": 0}, SCHEMA) != ["age: out of range"]:
        return "17.999 и 100.001 лежат вне диапазона 18..100"
    free = {"x": {"min": None, "max": None}}
    if m.validate_row({"x": -1e9}, free) != [] or m.validate_row({"x": 1e9}, free) != []:
        return "при min=None и max=None допустимо любое конечное число"
    got = m.validate_row({"z": 1, "b": "s", "q": 9}, {"b": {"min": None, "max": None}, "a": {"min": None, "max": None}})
    if got != ["a: missing", "b: expected number", "q: unknown field", "z: unknown field"]:
        return f"порядок ошибок: признаки схемы по алфавиту, затем лишние ключи по алфавиту; получили {got!r}"
    if m.validate_row({}, {}) != [] or m.validate_row({"a": 1}, {}) != ["a: unknown field"]:
        return "пустая схема: пустая строка корректна, любой ключ - лишний"
    row = {"age": 3, "income_k": "x"}
    snapshot = dict(row)
    m.validate_row(row, SCHEMA)
    if row != snapshot:
        return "validate_row не должна изменять строку"


def t_predict_extremes():
    big = {"name": "x", "version": 1, "features": ["a"], "weights": [1.0], "bias": 0.0, "threshold": 0.5}
    hi = m.predict_one(big, {"a": 1000.0})
    lo = m.predict_one(big, {"a": -1000.0})
    if hi != {"score": 1.0, "label": 1}:
        return f"z = 1000: ожидалось {{'score': 1.0, 'label': 1}} без OverflowError, получили {hi!r}"
    if lo != {"score": 0.0, "label": 0}:
        return f"z = -1000: ожидалось {{'score': 0.0, 'label': 0}} без OverflowError, получили {lo!r}"
    if m.predict_one(big, {"a": 1e6})["score"] != 1.0 or m.predict_one(big, {"a": -1e6})["score"] != 0.0:
        return "z = ±1e6 тоже не должно падать"
    zero = m.predict_one(big, {"a": 0.0})
    if zero != {"score": 0.5, "label": 1}:
        return f"p = 0.5 при пороге 0.5: label = 1 (p >= threshold), получили {zero!r}"
    strict = {**big, "threshold": 1.0}
    got = m.predict_one(strict, {"a": 15.0})
    if got != {"score": 1.0, "label": 0}:
        return f"z = 15, порог 1.0: score округляется до 1.0, но label считается по нерундированному p (< 1.0), ожидалось label 0, получили {got!r}"
    for t in (0.0, 0.3, 0.9):
        model = {**big, "threshold": t}
        for a in (-2.0, 0.0, 2.0):
            if m.predict_one(model, {"a": a}) != ref_predict(model, {"a": a}):
                return f"порог {t}, a = {a}: ожидалось {ref_predict(model, {'a': a})}, получили {m.predict_one(model, {'a': a})}"
    model = {**big, "bias": 1.5, "weights": [-0.75]}
    if m.predict_one(model, {"a": 2}) != ref_predict(model, {"a": 2}):
        return "отрицательные веса и целое значение признака обрабатываются так же"


def t_batch_boundaries():
    for n, size, want in ((64, 32, 2), (65, 32, 3), (63, 32, 2), (1, 32, 1), (5, 10, 1), (7, 1, 7), (32, 32, 1), (33, 32, 2)):
        data = rows(n)
        got = m.predict_batch(MODEL, SCHEMA, data, size)
        if got["batches"] != want:
            return f"{n} строк по {size} в пачке: ожидалось пачек {want}, получили {got['batches']}"
        if len(got["results"]) != n or [r["index"] for r in got["results"]] != list(range(n)):
            return f"{n} строк: results должен содержать по одному элементу на строку с индексами 0..{n - 1} по порядку"
    empty = m.predict_batch(MODEL, SCHEMA, [], 32)
    if empty != {"model": "churn:3", "batches": 0, "results": []}:
        return f"пустой список строк: ожидалось {{'model': 'churn:3', 'batches': 0, 'results': []}}, получили {empty!r}"
    for bad in (0, -5):
        try:
            m.predict_batch(MODEL, SCHEMA, rows(3), bad)
        except ValueError:
            continue
        return f"max_batch_size = {bad}: ожидалось ValueError"
    mixed = rows(40)
    mixed[7] = {"age": "x"}
    mixed[33] = 17
    got = m.predict_batch(MODEL, SCHEMA, mixed, 32)
    if got != ref_batch(MODEL, SCHEMA, mixed, 32):
        bad = [r["index"] for r in got["results"] if not r["ok"]]
        return f"смесь корректных и плохих строк: плохие индексы {bad}, результат не совпал с эталоном; индекс сквозной по всему списку, а не по пачке"
    other = {**MODEL, "name": "fraud", "version": 12}
    if m.predict_batch(other, SCHEMA, rows(2), 5)["model"] != "fraud:12":
        return "поле model строится как имя:версия"


def t_request_edges():
    status, payload = m.handle_request(MODEL, SCHEMA, '{"instances": []}')
    if (status, payload) != (200, {"model": "churn:3", "batches": 0, "results": []}):
        return f"пустой список instances - допустимый запрос: ожидалось (200, ... batches 0), получили {(status, payload)!r}"
    exact = json.dumps({"instances": rows(10)})
    status, payload = m.handle_request(MODEL, SCHEMA, exact, max_instances=10, max_batch_size=4)
    if status != 200 or payload["batches"] != 3:
        return f"ровно max_instances строк - ещё 200; 10 строк по 4 - 3 пачки; получили {status}, batches={payload.get('batches')!r}"
    over = json.dumps({"instances": rows(11)})
    got = m.handle_request(MODEL, SCHEMA, over, max_instances=10)
    if got != (413, {"error": "too many instances", "limit": 10}):
        return f"max_instances + 1 строк: ожидалось 413 с полем limit, получили {got!r}"
    status, payload = m.handle_request(MODEL, SCHEMA, json.dumps({"instances": rows(101)}))
    if status != 413 or payload.get("limit") != 100:
        return f"значение по умолчанию max_instances = 100: 101 строка даёт 413, получили {status}"
    status, payload = m.handle_request(MODEL, SCHEMA, json.dumps({"instances": rows(65)}))
    if status != 200 or payload["batches"] != 3:
        return f"значение по умолчанию max_batch_size = 32: 65 строк - 3 пачки, получили {status}, batches={payload.get('batches')!r}"
    for body in ("", "null", '"text"', '{"instances": null}', '{"instances": {"a": 1}}', '{"instances": "abc"}'):
        got = m.handle_request(MODEL, SCHEMA, body)
        if got[0] != 400:
            return f"тело {body!r}: ожидался статус 400, получили {got!r}"
    if m.handle_request(MODEL, SCHEMA, "")[1] != {"error": "invalid json"}:
        return "пустая строка - это неверный JSON, а не неверная форма"
    nan_body = '{"instances": [{"age": NaN, "income_k": 5}, {"age": 30, "income_k": 5}]}'
    status, payload = m.handle_request(MODEL, SCHEMA, nan_body)
    first = payload["results"][0] if status == 200 else None
    if status != 200 or first != {"index": 0, "ok": False, "errors": ["age: expected finite number"]}:
        return f"json.loads принимает NaN: ожидалась ошибка 'age: expected finite number' в results[0] при статусе 200, получили {status}, {first!r}"
    mixed = '{"instances": [1, {"age": 30, "income_k": 5}], "note": "extra"}'
    status, payload = m.handle_request(MODEL, SCHEMA, mixed)
    if status != 200 or payload["results"][0] != {"index": 0, "ok": False, "errors": ["row: expected object"]} or not payload["results"][1]["ok"]:
        return f"лишний ключ верхнего уровня допустим; элемент-не-объект - ошибка строки, а не всего запроса; получили {status}, {payload!r}"
    expect = ref_batch(MODEL, SCHEMA, rows(20), 8)
    status, payload = m.handle_request(MODEL, SCHEMA, json.dumps({"instances": rows(20)}), max_batch_size=8)
    if status != 200 or payload["batches"] != 3 or [r.get("label") for r in payload["results"]] != [r.get("label") for r in expect["results"]]:
        return "max_batch_size должен передаваться в predict_batch: 20 строк по 8 - 3 пачки"


case("validate-basic", t_validate_basic)
case("predict-one", t_predict_one)
case("batch-basic", t_batch_basic)
case("request-statuses", t_request_statuses)
case("validate-edges", t_validate_edges)
case("predict-extremes", t_predict_extremes)
case("batch-boundaries", t_batch_boundaries)
case("request-edges", t_request_edges)
