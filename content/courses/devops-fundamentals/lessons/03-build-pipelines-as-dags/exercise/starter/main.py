import sys


def fixed(num, den, places):
    """num/den, округлённое half-up до places знаков, строкой (без float)."""
    q = (2 * num * 10**places + den) // (2 * den)
    s = str(q).rjust(places + 1, "0")
    return s[:-places] + "." + s[-places:]


def main():
    order_in, dur, needs, errors = [], {}, {}, []
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        name = p[0]
        # TODO: повтор имени -> ошибка "ERROR duplicate job: <имя>"
        order_in.append(name)
        dur[name] = int(p[1])
        needs[name] = p[2:]
    # TODO: 1) неизвестные зависимости -> ошибки; 2) стадии (уровни) по зависимостям,
    #       при зацикливании "ERROR cycle: ..."; 3) печать стадий, порядка, длительностей и критического пути


main()
