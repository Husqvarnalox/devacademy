import sys


def fixed(num, den, places):
    """num/den, округлённое half-up до places знаков, строкой. Только целые числа."""
    q = (2 * num * 10**places + den) // (2 * den)
    s = str(q).rjust(places + 1, "0")
    return s[:-places] + "." + s[-places:]


def main():
    days = None
    deploys = []  # (deploy_ts, commit_ts, failed, restored_ts)
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        if p[0] == "DAYS":
            days = int(p[1])
        elif p[0] == "D":
            failed = p[3] == "FAIL"
            deploys.append((int(p[1]), int(p[2]), failed, int(p[4]) if failed else 0))
    n = len(deploys)
    print(f"deployments={n}")
    print(f"frequency_per_day={fixed(n, days, 2)}")
    if n == 0:
        print("lead_time_median_s=n/a")
        print("change_failure_rate_pct=n/a")
        print("mttr_s=n/a")
        return
    leads = sorted(d - c for d, c, _, _ in deploys)
    if n % 2:
        print(f"lead_time_median_s={fixed(leads[n // 2], 1, 1)}")
    else:
        print(f"lead_time_median_s={fixed(leads[n // 2 - 1] + leads[n // 2], 2, 1)}")
    failed = [(d, r) for d, _, f, r in deploys if f]
    print(f"change_failure_rate_pct={fixed(100 * len(failed), n, 1)}")
    if failed:
        print(f"mttr_s={fixed(sum(r - d for d, r in failed), len(failed), 1)}")
    else:
        print("mttr_s=n/a")


main()
