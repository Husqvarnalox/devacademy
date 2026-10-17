import sys


def main():
    lines = [l.split() for l in sys.stdin.read().splitlines() if l.strip()]
    _, threshold, timeout, probes = lines[0]      # BREAKER <порог> <таймаут> <пробы>
    threshold, timeout, probes = int(threshold), int(timeout), int(probes)
    state = "closed"
    calls = rejected = opens = 0
    for t, outcome in lines[1:]:
        t = int(t)
        # TODO: открытый размыкатель по истечении таймаута становится half_open;
        #       пока он open - вызов отклоняется (rejected) и в calls не входит;
        #       иначе вызов выполнен с исходом outcome (ok|fail): обновите счётчики и состояние
        print(f"{t} {outcome} state={state}")
    print(f"calls={calls} rejected={rejected} opens={opens}")


main()
