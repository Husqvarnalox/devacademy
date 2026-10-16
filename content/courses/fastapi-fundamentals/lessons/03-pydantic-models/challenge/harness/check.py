import json
import os
import warnings
from datetime import date

warnings.filterwarnings("ignore")

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from pydantic import ValidationError

import booking_task as m

TEXT = '{"guestName":"Ann","checkIn":"2025-03-01","checkOut":"2025-03-04"}'


def error_types(fn):
    try:
        fn()
    except ValidationError as exc:
        return {(".".join(map(str, e["loc"])), e["type"]) for e in exc.errors()}
    return None


def t_roundtrip():
    b = m.booking_from_json(TEXT)
    bad = expect((b.guest_name, b.check_in, b.nights), ("Ann", date(2025, 3, 1), 3), "разобранное бронирование")
    if bad:
        return bad
    out = json.loads(m.booking_to_json(b))
    return expect(
        out,
        {"guestName": "Ann", "checkIn": "2025-03-01", "checkOut": "2025-03-04", "nights": 3},
        "JSON результата",
    )


def t_python_names():
    b = m.Booking(guest_name="Bob", check_in="2025-01-31", check_out="2025-02-02")
    bad = expect(b.nights, 2, "nights через границу месяца")
    if bad:
        return bad
    b2 = m.Booking(guestName="Bob", checkIn=date(2025, 1, 31), checkOut=date(2025, 2, 2))
    return expect(b2, b, "создание по псевдонимам и по именам Python")


def t_date_order():
    for out in ("2025-03-01", "2025-02-28"):
        got = error_types(lambda: m.booking_from_json(TEXT.replace("2025-03-04", out)))
        bad = expect(got, {("", "value_error")}, f"check_out={out}")
        if bad:
            return bad


def t_forbid_blank():
    got = error_types(lambda: m.booking_from_json(TEXT[:-1] + ',"vip":true}'))
    bad = expect(got, {("vip", "extra_forbidden")}, "лишний ключ")
    if bad:
        return bad
    got = error_types(lambda: m.booking_from_json(TEXT.replace("Ann", "")))
    return expect(got, {("guestName", "string_too_short")}, "пустое имя")


def t_nights():
    b = m.Booking(guest_name="x", check_in="2024-02-28", check_out="2024-03-01")
    bad = expect(b.nights, 2, "високосный год: 2024-02-28 -> 2024-03-01")
    if bad:
        return bad
    b = m.Booking(guest_name="x", check_in="2025-02-28", check_out="2025-03-01")
    return expect(b.nights, 1, "невисокосный год")


case("roundtrip", t_roundtrip)
case("python-names", t_python_names)
case("date-order", t_date_order)
case("forbid-and-blank", t_forbid_blank)
case("nights", t_nights)
