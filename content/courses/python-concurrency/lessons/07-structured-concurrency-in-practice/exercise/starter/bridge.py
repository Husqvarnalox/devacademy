import asyncio
import threading


class AsyncBridge:
    """Event loop в фоновом потоке и блокирующий run() для синхронного кода."""

    def __init__(self):
        self._loop = None
        self._stop = None
        self._thread = None
        self._closed = False
        self._ready = threading.Event()

    def start(self):
        # TODO: запустить поток "async-bridge" с asyncio.run и дождаться готовности цикла
        return self

    def run(self, coro_func, *args, timeout=None):
        # TODO: run_coroutine_threadsafe + result(timeout), проверки состояния
        return None

    def close(self):
        # TODO: остановить цикл (отменив работающие корутины) и join потока
        pass

    def __enter__(self):
        return self.start()

    def __exit__(self, *exc):
        self.close()
