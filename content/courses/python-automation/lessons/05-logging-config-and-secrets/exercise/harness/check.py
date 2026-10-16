import io
import json
import logging
import os
import sys

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


import appconfig as m

TOKEN = "s3cr3t-T0KEN"
FILE = """
log_level = "debug"
port = 9000
api_token = "from-file"
"""


def t_dotenv():
    text = '''# комментарий
A=1
export B = two words
C="line1\\nline2 \\"q\\""
D='raw \\n stays'
E=value # хвостовой комментарий
F=
A=3
'''
    want = {"A": "3", "B": "two words", "C": 'line1\nline2 "q"', "D": "raw \\n stays", "E": "value", "F": ""}
    bad = expect(m.parse_dotenv(text), want, "parse_dotenv(...)")
    if bad:
        return bad
    for broken in ["no equals sign", "1BAD=x", "=x"]:
        try:
            m.parse_dotenv("OK=1\n" + broken)
        except ValueError as exc:
            if "2" not in str(exc):
                return f"сообщение для {broken!r} должно содержать номер строки (2): {exc}"
        else:
            return f"parse_dotenv с {broken!r} должен поднять ValueError"


def t_precedence():
    cfg = m.load_config("", {"APP_API_TOKEN": TOKEN})
    bad = expect((cfg.log_level, cfg.port, cfg.debug, cfg.api_token.reveal()), ("INFO", 8080, False, TOKEN), "только env + умолчания")
    if bad:
        return bad
    cfg = m.load_config(FILE, {})
    bad = expect((cfg.log_level, cfg.port, cfg.debug, cfg.api_token.reveal()), ("DEBUG", 9000, False, "from-file"), "файл переопределяет умолчания")
    if bad:
        return bad
    env = {"APP_PORT": "443", "APP_DEBUG": "Yes", "APP_API_TOKEN": TOKEN, "PATH": "/bin", "OTHER_X": "1"}
    cfg = m.load_config(FILE, env)
    bad = expect((cfg.log_level, cfg.port, cfg.debug, cfg.api_token.reveal()), ("DEBUG", 443, True, TOKEN), "env переопределяет файл")
    if bad:
        return bad
    cfg = m.load_config(None, {"X_API_TOKEN": "t", "X_LOG_LEVEL": "warning"}, prefix="X_")
    return expect((cfg.log_level, cfg.api_token.reveal()), ("WARNING", "t"), "другой префикс")


def make_logger(name, formatter, flt=None):
    stream = io.StringIO()
    handler = logging.StreamHandler(stream)
    handler.setFormatter(formatter)
    if flt:
        handler.addFilter(flt)
    logger = logging.getLogger("check." + name)
    logger.handlers[:] = [handler]
    logger.setLevel(logging.DEBUG)
    logger.propagate = False
    return logger, stream


def t_json_formatter():
    logger, stream = make_logger("json", m.JsonFormatter())
    logger.info("вход %s #%d", "Анна", 7, extra={"user_id": 42})
    logger.warning("плохо")
    lines = stream.getvalue().splitlines()
    if len(lines) != 2:
        return f"две записи должны дать две строки, получено {len(lines)}"
    first = json.loads(lines[0])
    bad = expect(first, {"level": "INFO", "logger": "check.json", "message": "вход Анна #7", "user_id": 42}, "первая запись")
    if bad:
        return bad
    if "\\u" in lines[0]:
        return "кириллица должна быть записана буквами (ensure_ascii=False)"
    if lines[0] != json.dumps(first, ensure_ascii=False, sort_keys=True):
        return "ключи должны быть отсортированы (sort_keys=True)"


def t_config_errors():
    env = {"APP_PORT": "abc", "APP_DEBUG": "maybe", "APP_LOG_LEVEL": "LOUD", "APP_COLOUR": "red"}
    text = f'api_token = "{TOKEN}"\ntheme = "dark"\n'
    try:
        m.load_config(text, env)
    except m.ConfigError as exc:
        names = [p.split(":")[0] for p in exc.problems]
        bad = expect(names, ["log_level", "port", "debug", "APP_COLOUR", "theme"], "имена проблем (порядок: параметры, затем неизвестные)")
        if bad:
            return bad
        if TOKEN in str(exc) or any(TOKEN in p for p in exc.problems):
            return "значение секрета попало в сообщение об ошибке"
    else:
        return "ожидался ConfigError"
    try:
        m.load_config("", {})
    except m.ConfigError as exc:
        bad = expect([p.split(":")[0] for p in exc.problems], ["api_token"], "обязательный api_token отсутствует")
        if bad:
            return bad
    for text, env in [("port = 0", {}), ("port = true", {}), ("debug = 1", {}), ("api_token = ''", {}), ("[[", {})]:
        env = dict(env, APP_API_TOKEN="x") if "api_token" not in text else env
        try:
            m.load_config(text, env)
        except m.ConfigError:
            continue
        return f"load_config({text!r}, ...) должен поднять ConfigError"
    try:
        m.load_config("", {"APP_API_TOKEN": "x", "APP_PORT": "70000"})
    except m.ConfigError:
        return None
    return "порт 70000 вне диапазона должен давать ConfigError"


def t_secret():
    s = m.Secret(TOKEN)
    cfg = m.load_config("", {"APP_API_TOKEN": TOKEN})
    for rendered in [repr(s), str(s), f"{s}", f"{s!r}", repr(cfg), str(cfg), f"{cfg}"]:
        if TOKEN in rendered:
            return f"секрет виден в представлении: {rendered!r}"
    bad = expect((repr(s), str(s), s.reveal()), ("Secret(***)", "***", TOKEN), "Secret: repr, str, reveal")
    if bad:
        return bad
    if m.Secret("a") != m.Secret("a") or m.Secret("a") == m.Secret("b") or m.Secret("a") == "a":
        return "Secret равен только Secret с тем же значением"
    try:
        cfg.port = 1
    except Exception:
        return None
    return "Config должен быть неизменяемым (frozen dataclass)"


def t_redact():
    flt = m.RedactingFilter([TOKEN, "", None])
    logger, stream = make_logger("redact", m.JsonFormatter(), flt)
    logger.info("вызов с токеном %s", TOKEN)
    logger.info("url=https://x/?key=" + TOKEN)
    logger.info("extra", extra={"password": "p@ss", "Authorization": "Bearer abc", "api_key": 123, "note": f"id {TOKEN}", "obj": m.Secret("zzz"), "user": "bob"})
    try:
        raise RuntimeError(f"не удалось с {TOKEN}")
    except RuntimeError:
        logger.exception("сбой")
    out = stream.getvalue()
    if TOKEN in out or "p@ss" in out or "Bearer abc" in out or "zzz" in out:
        return f"в журнале остался секрет: {out[:300]}"
    recs = [json.loads(line) for line in out.splitlines()]
    bad = expect(recs[0]["message"], "вызов с токеном ***", "сообщение после подстановки args")
    if bad:
        return bad
    bad = expect(
        (recs[2]["password"], recs[2]["Authorization"], recs[2]["api_key"], recs[2]["note"], recs[2]["obj"], recs[2]["user"]),
        ("***", "***", "***", "id ***", "***", "bob"),
        "extra-поля",
    )
    if bad:
        return bad
    if "RuntimeError" not in recs[3]["exc"] or TOKEN in recs[3]["exc"]:
        return "traceback должен сохраняться, но без секрета"
    record = logging.LogRecord("n", logging.INFO, "p", 1, "ok", (), None)
    return None if flt.filter(record) is True else "filter должен возвращать True"


def t_formatter_edge():
    fmt = m.JsonFormatter(include_time=True)
    record = logging.LogRecord("svc", logging.ERROR, "p", 1, "не %s", ("работает",), None)
    record.created = 1704067200.123
    record.level = "custom"
    out = json.loads(fmt.format(record))
    bad = expect(
        out,
        {"level": "ERROR", "logger": "svc", "message": "не работает", "time": "2024-01-01T00:00:00.123+00:00", "extra.level": "custom"},
        "запись со временем и конфликтным extra",
    )
    if bad:
        return bad
    try:
        raise ValueError("boom")
    except ValueError:
        record = logging.LogRecord("svc", logging.ERROR, "p", 1, "x", (), sys.exc_info())
    line = m.JsonFormatter().format(record)
    out = json.loads(line)
    if "\n" in line or "ValueError: boom" not in out.get("exc", ""):
        return "исключение должно попадать в поле exc, а запись - оставаться одной строкой"
    record = logging.LogRecord("svc", logging.INFO, "p", 1, "x", (), None)
    record.blob = {1, 2}
    return None if "blob" in json.loads(m.JsonFormatter().format(record)) else "нестандартные типы extra должны сериализоваться через default=str"


case("dotenv", t_dotenv)
case("precedence", t_precedence)
case("json-formatter", t_json_formatter)
case("config-errors", t_config_errors)
case("secret", t_secret)
case("redact", t_redact)
case("formatter-edge", t_formatter_edge)
