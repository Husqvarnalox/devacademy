import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    limit, window = (int(x) for x in lines[0].split())
    fixed = {}  # номер окна -> число разрешённых запросов
    swc = {}    # то же для скользящего окна со счётчиками
    fixed_ok, swc_ok = [], []
    for line in lines[1:]:
        t = int(line.split()[0])
        # TODO: решите, пропускают ли запрос фиксированное окно и скользящее окно со счётчиками,
        # и напечатайте "<t> FIXED:ALLOW SWC:DENY" (и т.д.)
    # TODO: напечатайте итоговые строки fixed_allowed/swc_allowed и fixed_max_in_window/swc_max_in_window


main()
