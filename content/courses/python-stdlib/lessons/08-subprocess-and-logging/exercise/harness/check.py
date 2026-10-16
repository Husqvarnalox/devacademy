import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import logging
import sys

import ops_task as m


class ListHandler(logging.Handler):
    def __init__(self):
        super().__init__()
        self.records = []

    def emit(self, record):
        self.records.append(record)


_counter = [0]


def fresh(level=logging.INFO):
    _counter[0] += 1
    h = ListHandler()
    logger = m.make_logger(f"devacademy.test.{_counter[0]}", h, level)
    if not isinstance(logger, logging.Logger):
        return None, h
    return logger, h


def t_run_basic():
    bad = expect(m.run_python("print(1+1)"), (0, "2\n", ""), "run_python('print(1+1)')")
    if bad:
        return bad
    got = m.run_python("import sys; print('x'); print('e', file=sys.stderr)")
    return expect(got, (0, "x\n", "e\n"), "stdout и stderr разделены")


def t_checked_basic():
    bad = expect(m.run_checked("print(1+1)"), "2", "run_checked('print(1+1)')")
    if bad:
        return bad
    return expect(m.run_checked("print('  a b  ')"), "a b", "run_checked обрезает пробелы")


def t_echo_safe():
    for arg in ("hello", "a; echo hacked", "$(echo pwned)", "`id` | cat > x && ls", "with space  and 'quotes' \"x\"", "-x"):
        bad = expect(m.safe_echo(arg), arg, f"safe_echo({arg!r})")
        if bad:
            return bad


def t_logger_basic():
    logger, h = fresh()
    if logger is None:
        return "make_logger должен вернуть logging.Logger"
    m.log_operation(logger, "ann", "login", True)
    m.log_operation(logger, "bob", "delete", False)
    if len(h.records) != 2:
        return f"ожидалось 2 записи, получено {len(h.records)}"
    r1, r2 = h.records
    bad = expect((r1.levelno, r1.getMessage()), (logging.INFO, "user=ann action=login"), "успешная операция")
    if bad:
        return bad
    return expect((r2.levelno, r2.getMessage()), (logging.ERROR, "user=bob action=delete failed"), "неудачная операция")


def t_checked_fail():
    try:
        m.run_checked("import sys; print('boom', file=sys.stderr); sys.exit(3)")
    except RuntimeError as exc:
        text = str(exc)
        if "3" not in text or "boom" not in text:
            return f"сообщение должно содержать код возврата и stderr, получено {text!r}"
        return None
    except Exception as exc:
        return f"ожидался RuntimeError, получено {type(exc).__name__}"
    return "run_checked должен поднимать RuntimeError при ненулевом коде"


def t_timeout():
    import time

    start = time.monotonic()
    got = m.run_with_timeout("import time; time.sleep(10)", 0.5)
    spent = time.monotonic() - start
    bad = expect(got, "timeout", "run_with_timeout для зависшего процесса")
    if bad:
        return bad
    if spent > 5:
        return f"процесс не остановлен вовремя ({spent:.1f} с)"
    return expect(m.run_with_timeout("print('ok')", 5), "ok", "быстрый процесс")


def t_run_stderr():
    code, out, err = m.run_python("raise ValueError('bad')")
    if code != 1 or out != "" or "ValueError: bad" not in err:
        return f"получено ({code!r}, {out!r}, {err[-40:]!r})"
    return expect(m.run_python("import sys; sys.exit(7)")[0], 7, "код возврата sys.exit(7)")


def t_log_lazy():
    logger, h = fresh()
    m.log_operation(logger, "ann", "login", True)
    m.log_operation(logger, "bob", "delete", False)
    for rec, user, action in ((h.records[0], "ann", "login"), (h.records[1], "bob", "delete")):
        if "%s" not in str(rec.msg):
            return f"record.msg должно быть форматной строкой с %s, получено {rec.msg!r}"
        if tuple(rec.args) != (user, action):
            return f"record.args должно быть {(user, action)!r}, получено {rec.args!r}"
    logger2, h2 = fresh(logging.ERROR)
    m.log_operation(logger2, "x", "y", True)
    if h2.records:
        return "INFO-запись не должна проходить при уровне ERROR"


def t_log_exception():
    logger, h = fresh()
    bad = expect(m.safe_divide(logger, 10, 4), 2.5, "safe_divide(10, 4)")
    if bad:
        return bad
    if h.records:
        return "при успехе записей быть не должно"
    got = m.safe_divide(logger, 1, 0)
    if got is not None:
        return f"при делении на ноль ожидался None, получено {got!r}"
    if len(h.records) != 1:
        return f"ожидалась 1 запись, получено {len(h.records)}"
    rec = h.records[0]
    if rec.levelno != logging.ERROR:
        return "logger.exception пишет запись уровня ERROR"
    if not rec.exc_info or rec.exc_info[0] is not ZeroDivisionError:
        return "в записи должна быть трассировка ZeroDivisionError (exc_info)"
    return expect(rec.getMessage(), "division failed a=1 b=0", "текст записи")


def t_logger_idempotent():
    h = ListHandler()
    name = "devacademy.test.idem"
    l1 = m.make_logger(name, h, logging.WARNING)
    l2 = m.make_logger(name, h, logging.WARNING)
    if l1 is not l2 or l1 is not logging.getLogger(name):
        return "make_logger должен возвращать logging.getLogger(name)"
    if l1.handlers.count(h) != 1:
        return f"handler добавлен {l1.handlers.count(h)} раз, ожидался 1"
    if l1.propagate:
        return "propagate должен быть False"
    if l1.level != logging.WARNING:
        return f"уровень логгера {l1.level}, ожидался {logging.WARNING}"
    l1.info("skip")
    l1.warning("keep")
    return expect([r.getMessage() for r in h.records], ["keep"], "записи после фильтрации по уровню")


case("run-basic", t_run_basic)
case("checked-basic", t_checked_basic)
case("echo-safe", t_echo_safe)
case("logger-basic", t_logger_basic)
case("checked-fail", t_checked_fail)
case("timeout", t_timeout)
case("run-stderr", t_run_stderr)
case("log-lazy", t_log_lazy)
case("log-exception", t_log_exception)
case("logger-idempotent", t_logger_idempotent)
