"""Блокировка запуска, расчёт расписания и контрольные точки для долгоживущих задач."""

import json
import os
import time


class LockHeldError(RuntimeError):
    """Блокировка удерживается живым процессом (или файл блокировки не удалось понять)."""


def default_is_alive(pid):
    """Жив ли процесс pid (POSIX). Windows требует другого подхода."""
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


class LockFile:
    def __init__(self, path, *, stale_after=3600.0, now=time.time, pid=os.getpid, is_alive=default_is_alive):
        self.path = os.fspath(path)
        self.stale_after = stale_after
        self._now = now
        self._pid = pid
        self._is_alive = is_alive
        self._held = False

    def _create(self):
        fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o644)
        with os.fdopen(fd, "w", encoding="utf-8") as fh:
            fh.write(f"{self._pid()} {self._now()}\n")

    def _is_stale(self):
        try:
            with open(self.path, encoding="utf-8") as fh:
                text = fh.read()
        except FileNotFoundError:
            return True
        try:
            pid_text, ts_text = text.split()
            pid, stamp = int(pid_text), float(ts_text)
        except ValueError:
            raise LockHeldError(f"непонятное содержимое файла блокировки: {self.path}") from None
        if self._now() - stamp > self.stale_after:
            return True
        return not self._is_alive(pid)

    def acquire(self):
        try:
            self._create()
        except FileExistsError:
            if not self._is_stale():
                raise LockHeldError(f"блокировка занята: {self.path}") from None
            try:
                os.unlink(self.path)
            except FileNotFoundError:
                pass
            try:
                self._create()
            except FileExistsError:
                raise LockHeldError(f"блокировка перехвачена другим процессом: {self.path}") from None
        self._held = True

    def release(self):
        if not self._held:
            return
        self._held = False
        try:
            os.unlink(self.path)
        except FileNotFoundError:
            pass

    def __enter__(self):
        self.acquire()
        return self

    def __exit__(self, exc_type, exc, tb):
        self.release()
        return False


def next_run_time(start, interval, now):
    """Ближайший момент start + k*interval (k >= 0), строго позже now; до старта - сам start."""
    if now < start:
        return start
    return start + ((now - start) // interval + 1) * interval


def save_checkpoint(path, state):
    tmp = f"{os.fspath(path)}.tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(state, fh, sort_keys=True)
        fh.flush()
        os.fsync(fh.fileno())
    os.replace(tmp, path)


def load_checkpoint(path, default=None):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except FileNotFoundError:
        return default
    except json.JSONDecodeError as exc:
        raise ValueError(f"повреждённая контрольная точка {os.fspath(path)}: {exc}") from None


def process_with_checkpoint(items, handler, checkpoint_path):
    state = load_checkpoint(checkpoint_path, {"done": 0})
    done = state.get("done") if isinstance(state, dict) else None
    if not isinstance(done, int) or not 0 <= done <= len(items):
        raise ValueError(f"недопустимая контрольная точка: {state!r}")
    processed = 0
    for index in range(done, len(items)):
        handler(items[index])
        save_checkpoint(checkpoint_path, {"done": index + 1})
        processed += 1
    return processed
