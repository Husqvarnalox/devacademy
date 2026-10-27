import hashlib
import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import repro as m


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


def ref_fingerprint(cfg):
    return hashlib.sha256(ref_canonical(cfg).encode("utf-8")).hexdigest()[:12]


def ref_split(ids, pct, salt):
    train, test = [], []
    for i in ids:
        b = int(hashlib.sha256(f"{salt}:{i}".encode("utf-8")).hexdigest(), 16) % 100
        (test if b < pct else train).append(i)
    return train, test


CFG = {"model": "logreg", "lr": 0.1, "epochs": 20, "features": ["age", "income"], "seed": 42}


def t_canonical_order():
    a = m.canonical_json(CFG)
    b = m.canonical_json({"seed": 42, "features": ["age", "income"], "epochs": 20, "lr": 0.1, "model": "logreg"})
    want = '{"epochs":20,"features":["age","income"],"lr":0.1,"model":"logreg","seed":42}'
    if a != want:
        return f"canonical_json вернула {a!r}, ожидалось {want!r} (ключи по алфавиту, без пробелов)"
    if a != b:
        return "порядок ключей в исходном словаре не должен влиять на результат"
    nested = m.canonical_json({"b": {"y": 1, "x": 2}, "a": [3, {"d": 4, "c": 5}]})
    if nested != '{"a":[3,{"c":5,"d":4}],"b":{"x":2,"y":1}}':
        return f"вложенные словари тоже сортируются по ключам: получили {nested!r}"


def t_canonical_unicode_nan():
    got = m.canonical_json({"имя": "Ана", "flag": True, "none": None})
    want = '{"flag":true,"none":null,"имя":"Ана"}'
    if got != want:
        return f"получили {got!r}, ожидалось {want!r}: кириллица записывается как есть (ensure_ascii=False)"
    for bad in (math.nan, math.inf):
        try:
            m.canonical_json({"lr": bad})
        except ValueError:
            continue
        return f"значение {bad} нельзя записать в JSON однозначно: ожидалось ValueError (allow_nan=False)"


def t_fingerprint_format():
    fp = m.config_fingerprint(CFG)
    if not isinstance(fp, str) or len(fp) != 12 or any(c not in "0123456789abcdef" for c in fp):
        return f"отпечаток должен быть строкой из 12 строчных шестнадцатеричных символов, получили {fp!r}"
    if fp != ref_fingerprint(CFG):
        return f"отпечаток {fp!r} не совпадает с эталоном {ref_fingerprint(CFG)!r}: хешируйте canonical_json(cfg) в UTF-8 через SHA-256"
    reordered = dict(reversed(list(CFG.items())))
    if m.config_fingerprint(reordered) != fp:
        return "порядок ключей не должен менять отпечаток"


def t_fingerprint_sensitive():
    base = m.config_fingerprint(CFG)
    for change in ({"lr": 0.01}, {"seed": 43}, {"features": ["income", "age"]}, {"epochs": "20"}):
        cfg = {**CFG, **change}
        got = m.config_fingerprint(cfg)
        if got == base:
            return f"изменение {change} должно менять отпечаток"
        if got != ref_fingerprint(cfg):
            return f"для {change} отпечаток {got!r} не совпадает с эталоном"
    cyr = {"описание": "базовая модель", "lr": 0.1}
    if m.config_fingerprint(cyr) != ref_fingerprint(cyr):
        return "отпечаток конфигурации с кириллицей не совпадает с эталоном: кодируйте строку в UTF-8"


IDS = [f"user-{i}" for i in range(1, 41)]


def t_split_partition():
    train, test = m.hash_split(IDS, 30, "v1")
    want = ref_split(IDS, 30, "v1")
    if (list(train), list(test)) != want:
        return f"разбиение не совпадает с эталоном: в test объекты с bucket(id, salt) < test_percent, порядок как во входе"


def t_split_stable_growth():
    old_train, old_test = m.hash_split(IDS, 25, "exp")
    more = IDS + [f"user-{i}" for i in range(41, 81)]
    new_train, new_test = m.hash_split(more, 25, "exp")
    moved = [i for i in IDS if (i in old_test) != (i in new_test)]
    if moved:
        return f"после добавления новых объектов старые переехали в другую часть: {moved[:3]}"
    if (list(new_train), list(new_test)) != ref_split(more, 25, "exp"):
        return "разбиение расширенного набора не совпадает с эталоном"


def t_split_range():
    train, test = m.hash_split(IDS, 0, "s")
    if list(train) != IDS or list(test) != []:
        return "при test_percent = 0 все объекты уходят в train"
    train, test = m.hash_split(IDS, 100, "s")
    if list(train) != [] or list(test) != IDS:
        return "при test_percent = 100 все объекты уходят в test"
    if tuple(map(list, m.hash_split([], 20, "s"))) != ([], []):
        return "пустой список даёт две пустые части"
    for bad in (-1, 101):
        try:
            m.hash_split(IDS, bad, "s")
        except ValueError:
            continue
        return f"test_percent = {bad} вне диапазона 0..100: ожидалось ValueError"


def t_split_salt():
    a = m.hash_split(IDS, 50, "v1")
    b = m.hash_split(IDS, 50, "v2")
    if tuple(map(list, a)) == tuple(map(list, b)):
        return "разная соль должна давать другое разбиение"
    if tuple(map(list, b)) != ref_split(IDS, 50, "v2"):
        return "разбиение с солью v2 не совпадает с эталоном"
    nums = list(range(1, 21))
    if tuple(map(list, m.hash_split(nums, 40, "n"))) != ref_split(nums, 40, "n"):
        return "числовые id хешируются по строке f\"{salt}:{id}\" так же, как строковые"


case("canonical-order", t_canonical_order)
case("fingerprint-format", t_fingerprint_format)
case("split-partition", t_split_partition)
case("split-stable-growth", t_split_stable_growth)
case("canonical-unicode-nan", t_canonical_unicode_nan)
case("fingerprint-sensitive", t_fingerprint_sensitive)
case("split-range", t_split_range)
case("split-salt", t_split_salt)
