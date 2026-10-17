import operator
import sys

OPS = {">": operator.gt, ">=": operator.ge, "<": operator.lt, "<=": operator.le}


def main():
    lines = [l.split() for l in sys.stdin.read().splitlines() if l.strip()]
    _, name, op, threshold, _, hold = lines[0]      # RULE <имя> <оп> <порог> FOR <секунды>
    cmp, threshold, hold = OPS[op], int(threshold), int(hold)
    ticks = {}                                       # момент -> {ряд: значение}
    for t, series, value in lines[1:]:
        ticks.setdefault(int(t), {})[series] = int(value)
    # TODO: для каждого момента (по возрастанию) и каждого известного ряда (по алфавиту)
    #       выполните переходы inactive -> pending -> firing -> inactive и печатайте их,
    #       затем две итоговые строки "firing: ..." и "pending: ..."


main()
