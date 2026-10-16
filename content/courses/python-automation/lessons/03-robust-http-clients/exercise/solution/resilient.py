"""Устойчивый HTTP-клиент поверх внедряемой функции send(request) -> response."""

import random
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime

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


def header(headers, name):
    """Значение заголовка без учёта регистра имени или None."""
    wanted = name.lower()
    for key, value in headers.items():
        if key.lower() == wanted:
            return value
    return None


def is_idempotent(method):
    return method.upper() in IDEMPOTENT_METHODS


def parse_retry_after(value, now):
    """Retry-After: секунды или HTTP-дата -> секунды ожидания (>= 0) или None, если значение непонятно."""
    if value is None:
        return None
    value = value.strip()
    if value.isascii() and value.isdigit():
        return float(int(value))
    try:
        when = parsedate_to_datetime(value)
    except (TypeError, ValueError):
        return None
    if when.tzinfo is None:
        when = when.replace(tzinfo=timezone.utc)
    return max(0.0, (when - now).total_seconds())


def backoff_delay(attempt, base=0.5, cap=30.0, rng=random.random):
    """Экспоненциальная задержка с «полным джиттером»: rng() * min(cap, base * 2**attempt)."""
    return rng() * min(cap, base * 2 ** min(attempt, 60))


def send_with_retry(send, request, *, sleep, max_attempts=4, base=0.5, cap=30.0, max_retry_after=60.0,
                    rng=random.random, now=None):
    if max_attempts < 1:
        raise ValueError("max_attempts must be >= 1")
    now = now or (lambda: datetime.now(timezone.utc))
    attempts = max_attempts if is_idempotent(request.method) else 1
    for attempt in range(attempts):
        last = attempt == attempts - 1
        try:
            response = send(request)
        except (TimeoutError, ConnectionError):
            if last:
                raise
            sleep(backoff_delay(attempt, base, cap, rng))
            continue
        if response.status not in RETRYABLE_STATUSES or last:
            return response
        wait = parse_retry_after(header(response.headers, "Retry-After"), now())
        if wait is None:
            wait = backoff_delay(attempt, base, cap, rng)
        elif wait > max_retry_after:
            return response
        sleep(wait)


class TokenBucket:
    """Ограничитель частоты: rate токенов в секунду, ёмкость capacity, время из clock()."""

    def __init__(self, rate, capacity, clock=time.monotonic):
        if rate <= 0 or capacity <= 0:
            raise ValueError("rate and capacity must be positive")
        self.rate = rate
        self.capacity = capacity
        self._clock = clock
        self._tokens = float(capacity)
        self._last = clock()

    def _refill(self):
        now = self._clock()
        self._tokens = min(self.capacity, self._tokens + (now - self._last) * self.rate)
        self._last = now

    def try_acquire(self, n=1):
        self._refill()
        if self._tokens >= n:
            self._tokens -= n
            return True
        return False

    def wait_time(self, n=1):
        self._refill()
        if self._tokens >= n:
            return 0.0
        return (n - self._tokens) / self.rate

    def acquire(self, n=1, *, sleep):
        if n > self.capacity:
            raise ValueError("n exceeds bucket capacity")
        while not self.try_acquire(n):
            sleep(self.wait_time(n))
