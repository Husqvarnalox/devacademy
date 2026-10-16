from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError


def parse_iso(text):
    """ISO 8601 со смещением -> aware datetime; иначе ValueError."""
    # TODO
    return None


def to_utc_iso(dt):
    """aware datetime -> строка ISO в UTC с суффиксом Z."""
    # TODO
    return None


def convert_zone(text, zone_name):
    """Момент (ISO со смещением) -> isoformat() в поясе zone_name."""
    # TODO
    return None


def local_to_utc(naive_text, zone_name):
    """Локальное время (без смещения) в поясе -> UTC ISO с Z; ValueError для пропавшего и повторяющегося времени."""
    # TODO
    return None


def add_wall_days(naive_text, zone_name, days):
    """Прибавить календарные дни, сохранив стенные часы; вернуть isoformat()."""
    # TODO
    return None


def elapsed_hours(naive_start, naive_end, zone_name):
    """Реальное число часов между двумя локальными временами."""
    # TODO
    return None
