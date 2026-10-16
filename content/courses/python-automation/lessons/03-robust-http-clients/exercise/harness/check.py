import json
import os
from datetime import datetime, timezone

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


import resilient as m

NOW = datetime(2015, 10, 21, 7, 28, 0, tzinfo=timezone.utc)


class Script:
    """Фейковый сервер: отдаёт заготовленные ответы/исключения по порядку и считает вызовы."""

    def __init__(self, *items):
        self.items = list(items)
        self.calls = 0

    def __call__(self, request):
        self.calls += 1
        item = self.items.pop(0) if len(self.items) > 1 else self.items[0]
        if isinstance(item, Exception):
            raise item
        return item


def R(status, **headers):
    return m.Response(status, headers)


def run(script, method="GET", **kw):
    slept = []
    kw.setdefault("rng", lambda: 1.0)
    kw.setdefault("now", lambda: NOW)
    result = None
    error = None
    try:
        result = m.send_with_retry(script, m.Request(method, "https://api.example/x"), sleep=slept.append, **kw)
    except Exception as exc:
        error = exc
    return result, error, slept


def t_retry_after_parse():
    for value, want in [
        ("120", 120.0), (" 5 ", 5.0), ("0", 0.0),
        ("Wed, 21 Oct 2015 07:28:30 GMT", 30.0),
        ("Wed, 21 Oct 2015 07:00:00 GMT", 0.0),
        ("soon", None), ("", None), (None, None), ("-5", None), ("1.5", None),
    ]:
        bad = expect(m.parse_retry_after(value, NOW), want, f"parse_retry_after({value!r}, now)")
        if bad:
            return bad


def t_backoff():
    for attempt, want in [(0, 0.5), (1, 1.0), (2, 2.0), (5, 16.0), (6, 30.0), (50, 30.0)]:
        bad = expect(m.backoff_delay(attempt, rng=lambda: 1.0), want, f"backoff_delay({attempt}, rng=1.0)")
        if bad:
            return bad
    bad = expect(m.backoff_delay(3, rng=lambda: 0.5), 2.0, "backoff_delay(3, rng=0.5)")
    return bad or expect(m.backoff_delay(2, base=1, cap=3, rng=lambda: 1.0), 3, "backoff_delay с cap")


def t_retry_basic():
    server = Script(R(503), R(503), R(200))
    result, error, slept = run(server)
    if error:
        return f"неожиданное исключение {error!r}"
    return expect((result.status, server.calls, slept), (200, 3, [0.5, 1.0]), "GET: 503, 503, 200 -> (статус, вызовы, паузы)")


def t_token_bucket():
    t = [0.0]
    bucket = m.TokenBucket(rate=2, capacity=4, clock=lambda: t[0])
    got = [bucket.try_acquire() for _ in range(5)]
    bad = expect(got, [True, True, True, True, False], "5 try_acquire подряд")
    if bad:
        return bad
    bad = expect(bucket.wait_time(), 0.5, "wait_time() при пустой корзине")
    if bad:
        return bad
    t[0] = 0.5
    bad = expect((bucket.try_acquire(), bucket.try_acquire()), (True, False), "после 0.5 с")
    if bad:
        return bad
    t[0] = 1000.0
    return expect([bucket.try_acquire() for _ in range(5)], [True] * 4 + [False], "ёмкость ограничивает накопление")


def t_retry_after_header():
    server = Script(R(429, **{"retry-after": "7"}), R(200))
    result, error, slept = run(server)
    bad = expect((getattr(result, "status", None), slept), (200, [7.0]), "429 + retry-after: 7 (имя заголовка в нижнем регистре)")
    if bad:
        return bad
    server = Script(R(503, **{"Retry-After": "Wed, 21 Oct 2015 07:28:09 GMT"}), R(200))
    result, error, slept = run(server)
    bad = expect(slept, [9.0], "Retry-After с HTTP-датой")
    if bad:
        return bad
    server = Script(R(429, **{"Retry-After": "3600"}), R(200))
    result, error, slept = run(server, max_retry_after=60)
    return expect((result.status, server.calls, slept), (429, 1, []), "слишком длинный Retry-After: вернуть ответ без ожидания")


def t_non_idempotent():
    server = Script(R(503), R(200))
    result, error, slept = run(server, method="POST")
    bad = expect((result.status, server.calls, slept), (503, 1, []), "POST + 503: повторов быть не должно")
    if bad:
        return bad
    server = Script(TimeoutError("t"), R(200))
    result, error, slept = run(server, method="post")
    if not isinstance(error, TimeoutError) or server.calls != 1:
        return f"POST + таймаут: ожидалось TimeoutError после одной попытки, вызовов {server.calls}, ошибка {error!r}"
    server = Script(ConnectionResetError("r"), R(204))
    result, error, slept = run(server, method="delete")
    return expect((getattr(result, "status", None), server.calls), (204, 2), "DELETE идемпотентен (регистр метода не важен)")


def t_exhaustion():
    server = Script(R(500))
    result, error, slept = run(server, max_attempts=4)
    bad = expect((result.status, server.calls, slept), (500, 4, [0.5, 1.0, 2.0]), "всегда 500, 4 попытки")
    if bad:
        return bad
    server = Script(TimeoutError("again"))
    result, error, slept = run(server, max_attempts=3)
    if not isinstance(error, TimeoutError):
        return f"после последней попытки исключение должно быть поднято, получено {error!r}"
    return expect((server.calls, slept), (3, [0.5, 1.0]), "всегда таймаут, 3 попытки")


def t_non_retryable():
    server = Script(R(404), R(200))
    result, error, slept = run(server)
    bad = expect((result.status, server.calls, slept), (404, 1, []), "404 не повторяется")
    if bad:
        return bad
    server = Script(ValueError("bug"), R(200))
    result, error, slept = run(server)
    if not isinstance(error, ValueError) or server.calls != 1:
        return "посторонние исключения (ValueError) не должны перехватываться"
    try:
        m.send_with_retry(Script(R(200)), m.Request("GET", "u"), sleep=lambda s: None, max_attempts=0)
    except ValueError:
        return None
    return "max_attempts=0 должен давать ValueError"


def t_token_bucket_acquire():
    t = [0.0]
    sleeps = []

    def sleep(seconds):
        sleeps.append(seconds)
        t[0] += seconds

    bucket = m.TokenBucket(rate=4, capacity=2, clock=lambda: t[0])
    bucket.acquire(2, sleep=sleep)
    bucket.acquire(1, sleep=sleep)
    bad = expect(sleeps, [0.25], "acquire при пустой корзине")
    if bad:
        return bad
    bad = expect(bucket.wait_time(2), 0.5, "wait_time(2)")
    if bad:
        return bad
    try:
        bucket.acquire(3, sleep=sleep)
    except ValueError:
        pass
    else:
        return "acquire(n > capacity) никогда не завершится - ожидался ValueError"
    try:
        m.TokenBucket(rate=0, capacity=1)
    except ValueError:
        return None
    return "rate <= 0 должен давать ValueError"


case("retry-after-parse", t_retry_after_parse)
case("backoff", t_backoff)
case("retry-basic", t_retry_basic)
case("token-bucket", t_token_bucket)
case("retry-after-header", t_retry_after_header)
case("non-idempotent", t_non_idempotent)
case("exhaustion", t_exhaustion)
case("non-retryable", t_non_retryable)
case("token-bucket-acquire", t_token_bucket_acquire)
