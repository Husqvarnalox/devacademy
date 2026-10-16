import sys

SECONDS_PER_DAY = 86400
DAYS_PER_YEAR = 365
UNITS = ["B", "KB", "MB", "GB", "TB", "PB"]


def ceil_div(a, b):
    return -(-a // b)


def human(n):
    """Decimal units (1 KB = 1000 B), one decimal, rounded half up."""
    k = 0
    while k < len(UNITS) - 1 and n >= 1000 ** (k + 1):
        k += 1
    scale = 1000 ** k
    tenths = (n * 10 * 2 + scale) // (2 * scale)
    return f"{tenths // 10}.{tenths % 10} {UNITS[k]}"


def main():
    spec = {}
    for line in sys.stdin.read().splitlines():
        parts = line.split()
        if len(parts) == 2:
            spec[parts[0]] = int(parts[1])
    writes_day = spec["dau"] * spec["writes_per_user_day"]
    reads_day = writes_day * spec["reads_per_write"]
    peak = spec["peak_factor"]
    w_avg = ceil_div(writes_day, SECONDS_PER_DAY)
    w_peak = ceil_div(writes_day * peak, SECONDS_PER_DAY)
    r_avg = ceil_div(reads_day, SECONDS_PER_DAY)
    r_peak = ceil_div(reads_day * peak, SECONDS_PER_DAY)
    per_write = spec["write_bytes"] * spec["replication"]
    storage_year = writes_day * DAYS_PER_YEAR * per_write
    storage_retained = writes_day * spec["retention_days"] * per_write
    ingress_mbit = ceil_div(w_peak * spec["write_bytes"] * 8, 1_000_000)
    egress_mbit = ceil_div(r_peak * spec["read_bytes"] * 8, 1_000_000)
    print(f"write_qps avg={w_avg} peak={w_peak}")
    print(f"read_qps avg={r_avg} peak={r_peak}")
    print(f"storage_year {human(storage_year)}")
    print(f"storage_retained {human(storage_retained)}")
    print(f"ingress_peak {ingress_mbit} Mbit/s")
    print(f"egress_peak {egress_mbit} Mbit/s")


main()
