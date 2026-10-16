"""Периодический запуск без накопления долга и корректная остановка по сигналу."""

import signal


class GracefulStop:
    DEFAULT_SIGNALS = (signal.SIGINT, signal.SIGTERM)

    def __init__(self, signals=DEFAULT_SIGNALS):
        self.signals = tuple(signals)
        self.requested = False
        self.signum = None
        self._previous = {}

    def handle(self, signum, frame=None):
        self.requested = True
        self.signum = signum

    def install(self):
        for sig in self.signals:
            self._previous[sig] = signal.signal(sig, self.handle)

    def uninstall(self):
        for sig, handler in self._previous.items():
            signal.signal(sig, handler)
        self._previous = {}

    def __enter__(self):
        self.install()
        return self

    def __exit__(self, exc_type, exc, tb):
        self.uninstall()
        return False


def run_periodically(job, interval, *, now, sleep, should_stop, start=None):
    """Запускать job в моменты start + k*interval; пропущенные слоты не догонять; вернуть число запусков."""
    if interval <= 0:
        raise ValueError("interval must be positive")
    origin = now() if start is None else start
    runs = 0
    while not should_stop():
        job()
        runs += 1
        if should_stop():
            break
        current = now()
        if current < origin:
            target = origin
        else:
            target = origin + ((current - origin) // interval + 1) * interval
        sleep(target - current)
    return runs
