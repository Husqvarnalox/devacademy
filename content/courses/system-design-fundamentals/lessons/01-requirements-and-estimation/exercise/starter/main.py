import sys

SECONDS_PER_DAY = 86400
DAYS_PER_YEAR = 365


def ceil_div(a, b):
    """Целочисленное деление с округлением вверх, без float."""
    # TODO
    return 0


def human(n):
    """Размер в десятичных единицах: '2.2 TB', '180.0 GB', '182.5 KB'."""
    # TODO
    return ""


def main():
    spec = {}
    for line in sys.stdin.read().splitlines():
        parts = line.split()
        if len(parts) == 2:
            spec[parts[0]] = int(parts[1])
    # TODO: посчитайте QPS, хранилище и пропускную способность и напечатайте шесть строк
    # в формате из условия (write_qps, read_qps, storage_year, storage_retained, ingress_peak, egress_peak)


main()
