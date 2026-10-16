"""Предохранитель (circuit breaker) с внедряемыми часами."""

import time


class CircuitOpenError(Exception):
    """Вызов отклонён: предохранитель разомкнут."""


class CircuitBreaker:
    """Состояния: 'closed' (вызовы идут), 'open' (вызовы отклоняются CircuitOpenError, fn не вызывается),
    'half_open' (прошло reset_timeout секунд после размыкания: разрешён один пробный вызов).

    * В 'closed' подряд идущие сбои считаются; при failure_threshold сбоях предохранитель размыкается.
    * Успех обнуляет счётчик сбоев и замыкает предохранитель.
    * В 'half_open' успех замыкает, а сбой размыкает снова с новым отсчётом reset_timeout.
    * Сбоем считается исключение из failure_types (оно пробрасывается дальше); остальные исключения
      пробрасываются, но на состояние не влияют.
    * Свойство state вычисляется по clock(), без фоновых потоков.
    * failure_threshold < 1 или reset_timeout <= 0 - ValueError.
    """

    def __init__(self, failure_threshold=3, reset_timeout=30.0, clock=time.monotonic, failure_types=(Exception,)):
        raise NotImplementedError

    @property
    def state(self):
        raise NotImplementedError

    def call(self, fn, *args, **kwargs):
        raise NotImplementedError
