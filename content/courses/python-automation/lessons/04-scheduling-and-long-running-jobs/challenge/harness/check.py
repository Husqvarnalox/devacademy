import json
import os
import signal

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


import runner as m


class Sim:
    """Виртуальное время: sleep продвигает часы, job может «работать» заданное время."""

    def __init__(self, durations):
        self.t = 0.0
        self.durations = list(durations)
        self.starts = []
        self.sleeps = []

    def now(self):
        return self.t

    def sleep(self, seconds):
        self.sleeps.append(seconds)
        self.t += seconds

    def job(self):
        self.starts.append(self.t)
        self.t += self.durations[min(len(self.starts) - 1, len(self.durations) - 1)]


def t_periodic_basic():
    sim = Sim([3])
    runs = m.run_periodically(sim.job, 10, now=sim.now, sleep=sim.sleep, should_stop=lambda: len(sim.starts) >= 4)
    return expect((runs, sim.starts, sim.sleeps), (4, [0.0, 10.0, 20.0, 30.0], [7.0, 7.0, 7.0]), "4 запуска по 3 с каждые 10 с")


def t_skip_missed():
    sim = Sim([25, 3, 3])
    runs = m.run_periodically(sim.job, 10, now=sim.now, sleep=sim.sleep, should_stop=lambda: len(sim.starts) >= 3)
    return expect((runs, sim.starts), (3, [0.0, 30.0, 40.0]), "долгий первый запуск: слоты 10 и 20 пропущены, не догоняются")


def t_stop_flag():
    sim = Sim([1])
    stop = m.GracefulStop(signals=())
    calls = []

    def job():
        sim.job()
        calls.append(sim.t)
        if len(calls) == 2:
            stop.handle(signal.SIGTERM)

    runs = m.run_periodically(job, 5, now=sim.now, sleep=sim.sleep, should_stop=lambda: stop.requested)
    bad = expect((runs, len(sim.sleeps)), (2, 1), "остановка во время второго запуска -> (запуски, паузы)")
    if bad:
        return bad
    if stop.signum != signal.SIGTERM:
        return "signum должен хранить номер сигнала"
    runs = m.run_periodically(sim.job, 5, now=sim.now, sleep=sim.sleep, should_stop=lambda: True)
    return expect(runs, 0, "should_stop сразу истинно")


def t_signal_install():
    sig = getattr(signal, "SIGUSR1", signal.SIGINT)  # SIGUSR1 does not exist on Windows
    marker = []
    previous = signal.signal(sig, lambda s, f: marker.append(s))
    try:
        stop = m.GracefulStop(signals=(sig,))
        if stop.requested or stop.signum is not None:
            return "до сигнала requested=False, signum=None"
        with stop:
            signal.raise_signal(sig)
            if not stop.requested or stop.signum != sig:
                return "обработчик сигнала должен выставлять requested и signum"
            if marker:
                return "прежний обработчик не должен вызываться, пока установлен наш"
        signal.raise_signal(sig)
        if marker != [sig]:
            return "после выхода из with прежний обработчик должен быть восстановлен"
    finally:
        signal.signal(sig, previous)


def t_interval_validation():
    sim = Sim([1])
    for bad_interval in (0, -5):
        try:
            m.run_periodically(sim.job, bad_interval, now=sim.now, sleep=sim.sleep, should_stop=lambda: True)
        except ValueError:
            continue
        return f"interval={bad_interval} должен давать ValueError"
    return None


def t_start_origin():
    sim = Sim([0])
    runs = m.run_periodically(sim.job, 10, now=sim.now, sleep=sim.sleep, should_stop=lambda: len(sim.starts) >= 2, start=100)
    return expect((runs, sim.starts), (2, [0.0, 100.0]), "start в будущем: первый запуск сразу, второй - в слот start")


case("periodic-basic", t_periodic_basic)
case("skip-missed", t_skip_missed)
case("stop-flag", t_stop_flag)
case("signal-install", t_signal_install)
case("interval-validation", t_interval_validation)
case("start-origin", t_start_origin)
