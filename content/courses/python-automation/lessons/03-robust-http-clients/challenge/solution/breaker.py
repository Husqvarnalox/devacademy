"""Предохранитель (circuit breaker) с внедряемыми часами."""

import time


class CircuitOpenError(Exception):
    """Вызов отклонён: предохранитель разомкнут."""


class CircuitBreaker:
    def __init__(self, failure_threshold=3, reset_timeout=30.0, clock=time.monotonic, failure_types=(Exception,)):
        if failure_threshold < 1 or reset_timeout <= 0:
            raise ValueError("failure_threshold must be >= 1 and reset_timeout > 0")
        self.failure_threshold = failure_threshold
        self.reset_timeout = reset_timeout
        self.failure_types = failure_types
        self._clock = clock
        self._failures = 0
        self._opened_at = None

    @property
    def state(self):
        if self._opened_at is None:
            return "closed"
        if self._clock() - self._opened_at >= self.reset_timeout:
            return "half_open"
        return "open"

    def call(self, fn, *args, **kwargs):
        state = self.state
        if state == "open":
            raise CircuitOpenError("circuit is open")
        try:
            result = fn(*args, **kwargs)
        except self.failure_types:
            if state == "half_open":
                self._opened_at = self._clock()
            else:
                self._failures += 1
                if self._failures >= self.failure_threshold:
                    self._opened_at = self._clock()
            raise
        self._failures = 0
        self._opened_at = None
        return result
