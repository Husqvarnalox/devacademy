import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    parts_count = int(lines[0])
    log = [[] for _ in range(parts_count)]
    committed = [0] * parts_count   # зафиксированные смещения группы
    position = [0] * parts_count    # текущая позиция чтения
    members = []                    # имена потребителей по алфавиту
    assign = {}                     # потребитель -> его партиции
    for line in lines[1:]:
        t = line.split()
        # TODO: PRODUCE / JOIN / LEAVE / CRASH / POLL / COMMIT по условию
    # TODO: итоговые строки committed, lag, processed/redelivered


main()
