import hashlib
import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import registry as m


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


def sha(data):
    return hashlib.sha256(data).hexdigest()


def ref_canonical(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


MODEL = {
    "name": "churn",
    "features": ["age", "income"],
    "weights": [0.5, -1.25],
    "bias": 0.5,
    "threshold": 0.5,
    "metadata": {"автор": "Ана", "data_version": "76917f82d3e0"},
}
BLOB_TEXT = (
    '{"bias":0.5,"features":["age","income"],"format":"linear-v1",'
    '"metadata":{"data_version":"76917f82d3e0","автор":"Ана"},"name":"churn","threshold":0.5,"weights":[0.5,-1.25]}'
)


def blob_of(**changes):
    """Готовый blob модели без обращения к функциям ученика."""
    body = {"format": "linear-v1", **MODEL, **changes}
    return ref_canonical(body).encode("utf-8")


def raises(exc_type, fn, *args, **kwargs):
    """Возвращает сообщение исключения нужного типа, иначе None."""
    try:
        fn(*args, **kwargs)
    except exc_type as exc:
        return str(exc) or exc_type.__name__
    except Exception:
        return None
    return None


def filled(reg, name="m", metrics_list=({"acc": 0.8}, {"acc": 0.9}, {"acc": 0.7})):
    for i, metrics in enumerate(metrics_list):
        reg.register(name, blob_of(bias=float(i)), metrics, f"run-{i + 1}")
    return reg


def t_serialize_roundtrip():
    blob = m.serialize_model(MODEL)
    if not isinstance(blob, bytes):
        return f"serialize_model должна вернуть bytes, получили {type(blob).__name__}"
    if blob.decode("utf-8") != BLOB_TEXT:
        return f"serialize_model вернула {blob.decode('utf-8')!r}, ожидалось {BLOB_TEXT!r} (canonical_json от {{\"format\": \"linear-v1\", **model}})"
    shuffled = dict(reversed(list(MODEL.items())))
    if m.serialize_model(shuffled) != blob:
        return "порядок ключей модели не должен влиять на байты"
    back = m.deserialize_model(blob)
    if back != MODEL:
        return f"deserialize_model вернула {back!r}, ожидалось {MODEL!r}"
    if "format" in back:
        return "в результате deserialize_model нет поля format"
    if MODEL["metadata"]["автор"] != "Ана" or "автор" not in blob.decode("utf-8"):
        return "кириллица в метаданных записывается как есть (UTF-8)"
    if m.deserialize_model(blob, expected_sha256=sha(blob)) != MODEL:
        return "с верной контрольной суммой модель читается как обычно"


def t_register_versions():
    reg = m.ModelRegistry()
    blob = blob_of()
    metrics = {"acc": 0.8, "loss": 0.4}
    v1 = reg.register("churn", blob, metrics, "run-a")
    v2 = reg.register("churn", blob_of(bias=1.0), {"acc": 0.9}, "run-b")
    w1 = reg.register("fraud", blob, {"acc": 0.5}, "run-c")
    v3 = reg.register("churn", blob_of(bias=2.0), {"acc": 0.7}, "run-d")
    if (v1, v2, v3, w1) != (1, 2, 3, 1):
        return f"версии нумеруются с 1 отдельно для каждого имени: churn -> {(v1, v2, v3)}, fraud -> {w1}"
    got = reg.get("churn", 1)
    want = {"name": "churn", "version": 1, "stage": "None", "sha256": sha(blob), "metrics": metrics, "run_id": "run-a"}
    if got != want:
        return f"get вернула {got!r}, ожидалось {want!r}"
    if reg.get("churn", 3)["run_id"] != "run-d" or reg.get("fraud", 1)["run_id"] != "run-c":
        return "каждая версия хранит свой run_id"
    for name, version in (("churn", 4), ("churn", 0), ("ghost", 1)):
        if raises(KeyError, reg.get, name, version) is None:
            return f"get({name!r}, {version}) для неизвестной версии должна бросать KeyError"


def t_transition_production():
    reg = filled(m.ModelRegistry())
    reg.transition("m", 1, "Staging")
    if reg.get("m", 1)["stage"] != "Staging":
        return "после transition(..., 'Staging') стадия должна быть Staging"
    reg.transition("m", 1, "Production")
    if reg.get("m", 1)["stage"] != "Production":
        return "стадия Production не установилась"
    reg.transition("m", 2, "Production")
    stages = [reg.get("m", v)["stage"] for v in (1, 2, 3)]
    if stages != ["Archived", "Production", "None"]:
        return f"новая Production-версия отправляет прежнюю в Archived: стадии {stages!r}, ожидалось ['Archived', 'Production', 'None']"
    if raises(ValueError, reg.transition, "m", 3, "Prod") is None:
        return "неизвестная стадия 'Prod' должна приводить к ValueError"
    if reg.get("m", 3)["stage"] != "None":
        return "после ошибки стадия не меняется"
    if raises(KeyError, reg.transition, "m", 9, "Staging") is None or raises(KeyError, reg.transition, "ghost", 1, "Staging") is None:
        return "неизвестная версия или имя должны приводить к KeyError"
    latest = reg.latest("m")
    if latest != reg.get("m", 2):
        return f"latest('m') вернула {latest!r}, ожидалось описание версии 2"


def t_promote_basic():
    reg = filled(m.ModelRegistry())
    if reg.promote_if_better("m", 1, "acc") is not True:
        return "если Production нет, кандидат повышается: ожидалось True"
    if reg.get("m", 1)["stage"] != "Production":
        return "после повышения версия 1 в стадии Production"
    if reg.promote_if_better("m", 3, "acc") is not False:
        return "версия 3 (acc 0.7) хуже Production (acc 0.8): ожидалось False"
    stages = [reg.get("m", v)["stage"] for v in (1, 2, 3)]
    if stages != ["Production", "None", "None"]:
        return f"при отказе стадии не меняются: {stages!r}"
    if reg.promote_if_better("m", 2, "acc") is not True:
        return "версия 2 (acc 0.9) лучше Production (acc 0.8): ожидалось True"
    stages = [reg.get("m", v)["stage"] for v in (1, 2, 3)]
    if stages != ["Archived", "Production", "None"]:
        return f"после повышения версии 2 прежняя Production уходит в Archived: {stages!r}"


def t_deserialize_errors():
    blob = blob_of()
    good = sha(blob)
    tampered = blob.replace(b'"bias":0.5', b'"bias":0.9')
    if tampered == blob:
        return "внутренняя ошибка теста"
    msg = raises(ValueError, m.deserialize_model, tampered, good)
    if msg is None:
        return "изменённый blob при заданной контрольной сумме должен приводить к ValueError"
    if "checksum" not in msg:
        return f"сообщение об ошибке контрольной суммы должно содержать 'checksum', получили {msg!r}"
    if m.deserialize_model(tampered)["bias"] != 0.9:
        return "без expected_sha256 контрольная сумма не проверяется"
    if raises(ValueError, m.deserialize_model, blob, "0" * 64) is None:
        return "неверная контрольная сумма - ValueError"
    if raises(ValueError, m.deserialize_model, blob_of().replace(b"linear-v1", b"pickle-v0")) is None:
        return "неизвестный format должен приводить к ValueError"
    if raises(ValueError, m.deserialize_model, ref_canonical({k: v for k, v in MODEL.items()}).encode("utf-8")) is None:
        return "blob без поля format должен приводить к ValueError"
    if raises(ValueError, m.deserialize_model, blob_of(weights=[0.5])) is None:
        return "число весов не равно числу признаков: ValueError"
    if raises(ValueError, m.deserialize_model, blob_of(weights=[0.5, 1.0, 2.0])) is None:
        return "лишние веса тоже приводят к ValueError"
    if raises(ValueError, m.deserialize_model, b"{not json") is None:
        return "некорректный JSON приводит к ValueError"
    ok = blob_of(weights=[], features=[])
    if m.deserialize_model(ok)["weights"] != []:
        return "модель без признаков допустима, если размеры совпадают"


def t_rollback_latest_copies():
    reg = filled(m.ModelRegistry(), metrics_list=({"acc": 0.8}, {"acc": 0.9}, {"acc": 0.7}, {"acc": 0.6}))
    if reg.latest("m") is not None or reg.latest("ghost") is not None:
        return "latest без версий в стадии, а также для неизвестного имени, возвращает None"
    reg.transition("m", 1, "Staging")
    reg.transition("m", 3, "Staging")
    reg.transition("m", 2, "Production")
    if reg.latest("m", "Staging")["version"] != 3:
        return "latest(stage) возвращает версию с наибольшим номером из стадии"
    if reg.latest("m", "None")["version"] != 4:
        return "стадию 'None' можно запрашивать как любую другую"
    reg.transition("m", 4, "Production")
    reg.transition("m", 4, "Archived")
    reg.transition("m", 2, "Archived")
    if reg.latest("m") is not None:
        return "если все Production-версии архивированы, latest() возвращает None"
    reg.transition("m", 4, "Production")
    reg.transition("m", 2, "Production")
    stages = [reg.get("m", v)["stage"] for v in (1, 2, 3, 4)]
    if stages != ["Staging", "Production", "Staging", "Archived"]:
        return f"откат Archived -> Production архивирует текущую Production: стадии {stages!r}, ожидалось ['Staging', 'Production', 'Staging', 'Archived']"
    reg.transition("m", 2, "Production")
    if reg.get("m", 2)["stage"] != "Production":
        return "повторный перевод в ту же стадию ничего не ломает"
    info = reg.get("m", 2)
    info["metrics"]["acc"] = -1
    info["stage"] = "Archived"
    if reg.get("m", 2)["metrics"]["acc"] != 0.9 or reg.get("m", 2)["stage"] != "Production":
        return "get возвращает копию: изменение результата не должно менять реестр"
    src = {"acc": 0.5}
    v = reg.register("other", blob_of(), src, "r")
    src["acc"] = 0.0
    if reg.get("other", v)["metrics"] != {"acc": 0.5}:
        return "register сохраняет копию metrics: последующие изменения исходного словаря не влияют"


def t_promote_modes():
    reg = filled(m.ModelRegistry(), metrics_list=({"loss": 0.5}, {"loss": 0.25}, {"loss": 0.625}, {"loss": 0.375}, {"acc": 0.9}))
    reg.promote_if_better("m", 1, "loss", mode="min")
    if reg.promote_if_better("m", 3, "loss", mode="min") is not False:
        return "loss 0.625 хуже Production (0.5) в режиме min: ожидалось False"
    if reg.promote_if_better("m", 2, "loss", mode="min", min_delta=0.25) is not False:
        return "улучшение 0.5 - 0.25 = 0.25 не больше min_delta = 0.25: граница включается в отказ, ожидалось False"
    if reg.get("m", 2)["stage"] != "None" or reg.get("m", 1)["stage"] != "Production":
        return "при отказе стадии остаются прежними"
    if reg.promote_if_better("m", 2, "loss", mode="min", min_delta=0.125) is not True:
        return "улучшение 0.25 больше min_delta = 0.125: ожидалось True"
    stages = [reg.get("m", v)["stage"] for v in (1, 2, 3, 4)]
    if stages != ["Archived", "Production", "None", "None"]:
        return f"после повышения стадии {stages!r}"
    if reg.promote_if_better("m", 4, "loss", mode="min") is not False:
        return "loss 0.375 хуже Production (0.25): ожидалось False"
    if reg.promote_if_better("m", 2, "loss", mode="min") is not False:
        return "повышение той же версии, что уже в Production, - улучшения нет: False"
    if raises(ValueError, reg.promote_if_better, "m", 5, "loss", "min") is None:
        return "у кандидата нет метрики loss: ожидалось ValueError"
    if raises(ValueError, reg.promote_if_better, "m", 5, "acc") is None:
        return "у текущей Production-версии нет метрики acc: ожидалось ValueError"
    if raises(ValueError, reg.promote_if_better, "m", 4, "loss", "avg") is None:
        return "неизвестный mode - ValueError"
    if raises(KeyError, reg.promote_if_better, "m", 99, "loss") is None:
        return "неизвестная версия - KeyError"
    if reg.get("m", 2)["stage"] != "Production" or reg.get("m", 5)["stage"] != "None":
        return "ошибки не меняют стадий"
    reg2 = filled(m.ModelRegistry(), metrics_list=({"acc": 0.5}, {"acc": 0.75}))
    reg2.transition("m", 1, "Production")
    if reg2.promote_if_better("m", 2, "acc", min_delta=0.25) is not False:
        return "mode max: улучшение 0.25 при min_delta = 0.25 - отказ"
    if reg2.promote_if_better("m", 2, "acc", min_delta=0.125) is not True:
        return "mode max: улучшение 0.25 при min_delta = 0.125 - повышение"


def t_load():
    reg = m.ModelRegistry()
    blob1 = m.serialize_model(MODEL)
    model2 = {**MODEL, "bias": 2.0, "weights": [1.0, 1.0]}
    blob2 = m.serialize_model(model2)
    reg.register("churn", blob1, {"acc": 0.8}, "r1")
    reg.register("churn", blob2, {"acc": 0.9}, "r2")
    if raises(LookupError, reg.load, "churn") is None:
        return "пока в Production ничего нет, load бросает LookupError"
    if raises(LookupError, reg.load, "ghost") is None:
        return "неизвестное имя - LookupError"
    reg.transition("churn", 1, "Production")
    reg.transition("churn", 2, "Staging")
    if reg.load("churn") != MODEL:
        return f"load('churn') вернула {reg.load('churn')!r}, ожидалась Production-модель версии 1"
    if reg.load("churn", "Staging") != model2:
        return "load('churn', 'Staging') возвращает модель из стадии Staging"
    if reg.get("churn", 1)["sha256"] != sha(blob1):
        return "sha256 версии - SHA-256 сохранённых байтов"
    bad_format = blob1.replace(b"linear-v1", b"linear-v9")
    reg.register("odd", bad_format, {}, "r3")
    reg.transition("odd", 1, "Production")
    if raises(ValueError, reg.load, "odd") is None:
        return "load проверяет формат: blob с чужим format приводит к ValueError"
    bad_dims = m.serialize_model({**MODEL, "weights": [1.0]})
    reg.register("odd2", bad_dims, {}, "r4")
    reg.transition("odd2", 1, "Production")
    if raises(ValueError, reg.load, "odd2") is None:
        return "load проверяет согласованность размеров: ValueError"


case("serialize-roundtrip", t_serialize_roundtrip)
case("register-versions", t_register_versions)
case("transition-production", t_transition_production)
case("promote-basic", t_promote_basic)
case("deserialize-errors", t_deserialize_errors)
case("rollback-latest-copies", t_rollback_latest_copies)
case("promote-modes", t_promote_modes)
case("load", t_load)
