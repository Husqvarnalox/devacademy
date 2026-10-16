"""Устойчивый HTTP-клиент поверх внедряемой функции send(request) -> response."""

import random
import time
from dataclasses import dataclass, field

IDEMPOTENT_METHODS = frozenset({"GET", "HEAD", "OPTIONS", "TRACE", "PUT", "DELETE"})
RETRYABLE_STATUSES = frozenset({429, 500, 502, 503, 504})


@dataclass
class Request:
    method: str
    url: str
    headers: dict = field(default_factory=dict)


@dataclass
class Response:
    status: int
    headers: dict = field(default_factory=dict)
    body: str = ""


def parse_retry_after(value, now):
    """value - значение заголовка Retry-After (str или None), now - aware datetime.
    '120' -> 120.0; HTTP-дата -> секунды до неё (не меньше 0.0); None или мусор -> None."""
    raise NotImplementedError


def backoff_delay(attempt, base=0.5, cap=30.0, rng=random.random):
    """Полный джиттер: rng() * min(cap, base * 2**attempt). attempt начинается с 0."""
    raise NotImplementedError


def send_with_retry(send, request, *, sleep, max_attempts=4, base=0.5, cap=30.0, max_retry_after=60.0,
                    rng=random.random, now=None):
    """Вызывать send(request) до max_attempts раз (всего).
    Повторять только идемпотентные методы (GET, HEAD, OPTIONS, TRACE, PUT, DELETE; регистр не важен);
    остальные - ровно одна попытка. Повтор при TimeoutError/ConnectionError и статусах из RETRYABLE_STATUSES.
    Между попытками sleep(delay): Retry-After (заголовок ищите без учёта регистра; now() - aware datetime,
    по умолчанию текущее UTC), иначе backoff_delay(номер_повтора, base, cap, rng).
    Если Retry-After больше max_retry_after - сразу вернуть ответ без ожидания.
    После последней попытки вернуть последний ответ или поднять последнее исключение.
    Остальные исключения не перехватывать."""
    raise NotImplementedError


class TokenBucket:
    """Ограничитель частоты. Начинается полным (capacity токенов); пополняется со скоростью rate токенов
    в секунду, но не выше capacity; время берётся из clock()."""

    def __init__(self, rate, capacity, clock=time.monotonic):
        raise NotImplementedError

    def try_acquire(self, n=1):
        """Забрать n токенов, если они есть, и вернуть True; иначе False (токены не менять)."""
        raise NotImplementedError

    def wait_time(self, n=1):
        """Сколько секунд ждать, чтобы накопилось n токенов (0.0, если уже хватает)."""
        raise NotImplementedError

    def acquire(self, n=1, *, sleep):
        """Блокирующий вариант: пока токенов не хватает, вызывать sleep(wait_time(n))."""
        raise NotImplementedError
