"""Периодический запуск без накопления долга и корректная остановка по сигналу."""

import signal


class GracefulStop:
    """Флаг «пора остановиться», который выставляет обработчик сигнала.

    * requested - False, пока сигнал не получен; signum - номер полученного сигнала (или None).
    * handle(signum, frame=None) - обработчик: только запоминает сигнал и выставляет флаг (больше ничего!).
    * install() ставит handle на все сигналы из signals и запоминает прежние обработчики;
      uninstall() их восстанавливает. Контекстный менеджер вызывает install на входе и uninstall на выходе.
    """

    DEFAULT_SIGNALS = (signal.SIGINT, signal.SIGTERM)

    def __init__(self, signals=DEFAULT_SIGNALS):
        raise NotImplementedError

    def handle(self, signum, frame=None):
        raise NotImplementedError

    def install(self):
        raise NotImplementedError

    def uninstall(self):
        raise NotImplementedError

    def __enter__(self):
        raise NotImplementedError

    def __exit__(self, exc_type, exc, tb):
        raise NotImplementedError


def run_periodically(job, interval, *, now, sleep, should_stop, start=None):
    """Запускать job() в моменты origin + k*interval, где origin = start или now() при входе.

    * Первый запуск сразу. Перед каждым запуском проверяется should_stop() - при True выйти.
    * После запуска: если should_stop() уже True - выйти без ожидания. Иначе sleep(разница) до ближайшего
      слота, строго позже now(). Если job работала дольше interval, пропущенные слоты не догоняются.
    * Вернуть число выполненных запусков. interval <= 0 - ValueError.
    now и sleep - внедряемые функции (now() -> число секунд, sleep(секунды))."""
    raise NotImplementedError
