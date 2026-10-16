from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError


def _zone(name):
    try:
        return ZoneInfo(name)
    except (ZoneInfoNotFoundError, ValueError) as exc:
        raise ValueError(f"неизвестный пояс {name!r} (на Windows нужен пакет tzdata)") from exc


def _parse_naive(text):
    dt = datetime.fromisoformat(text)
    if dt.utcoffset() is not None:
        raise ValueError("ожидалось локальное время без смещения")
    return dt


def parse_iso(text):
    dt = datetime.fromisoformat(text)
    if dt.utcoffset() is None:
        raise ValueError("нет смещения от UTC")
    return dt


def to_utc_iso(dt):
    if dt.utcoffset() is None:
        raise ValueError("naive datetime")
    s = dt.astimezone(timezone.utc).isoformat()
    return s[:-6] + "Z" if s.endswith("+00:00") else s


def convert_zone(text, zone_name):
    zone = _zone(zone_name)
    return parse_iso(text).astimezone(zone).isoformat()


def local_to_utc(naive_text, zone_name):
    zone = _zone(zone_name)
    naive = _parse_naive(naive_text)
    first = naive.replace(tzinfo=zone, fold=0)
    second = naive.replace(tzinfo=zone, fold=1)
    if first.utcoffset() != second.utcoffset():
        raise ValueError("локальное время не существует или неоднозначно")
    return to_utc_iso(first)


def add_wall_days(naive_text, zone_name, days):
    zone = _zone(zone_name)
    aware = _parse_naive(naive_text).replace(tzinfo=zone)
    return (aware + timedelta(days=days)).isoformat()


def elapsed_hours(naive_start, naive_end, zone_name):
    zone = _zone(zone_name)
    start = _parse_naive(naive_start).replace(tzinfo=zone).astimezone(timezone.utc)
    end = _parse_naive(naive_end).replace(tzinfo=zone).astimezone(timezone.utc)
    return (end - start).total_seconds() / 3600
