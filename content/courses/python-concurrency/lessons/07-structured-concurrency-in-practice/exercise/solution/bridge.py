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
        self._thread = threading.Thread(target=self._thread_main, name="async-bridge")
        self._thread.start()
        self._ready.wait()
        return self

    def _thread_main(self):
        asyncio.run(self._main())

    async def _main(self):
        self._loop = asyncio.get_running_loop()
        self._stop = asyncio.Event()
        self._ready.set()
        await self._stop.wait()

    def run(self, coro_func, *args, timeout=None):
        if self._closed or self._loop is None:
            raise RuntimeError("bridge is closed")
        if threading.current_thread() is self._thread:
            raise RuntimeError("run() cannot be called from the bridge thread")
        future = asyncio.run_coroutine_threadsafe(coro_func(*args), self._loop)
        try:
            return future.result(timeout)
        except TimeoutError:
            future.cancel()
            raise

    def close(self):
        if self._closed:
            return
        self._closed = True
        if self._loop is not None:
            self._loop.call_soon_threadsafe(self._stop.set)
            self._thread.join()

    def __enter__(self):
        return self.start()

    def __exit__(self, *exc):
        self.close()
