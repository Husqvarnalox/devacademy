import csv
import datetime
import io
import json
import struct
from decimal import Decimal

_HEADER = struct.Struct(">BI")


def csv_to_json(csv_text):
    reader = csv.DictReader(io.StringIO(csv_text, newline=""), restkey=None, restval="")
    rows = []
    for row in reader:
        if None in row:
            raise ValueError("в строке больше полей, чем в заголовке")
        rows.append(dict(row))
    return json.dumps(rows, ensure_ascii=False)


def _cell(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def json_to_csv(json_text):
    data = json.loads(json_text)
    columns = []
    for obj in data:
        for key in obj:
            if key not in columns:
                columns.append(key)
    if not columns:
        return ""
    out = io.StringIO()
    writer = csv.writer(out, lineterminator="\n")
    writer.writerow(columns)
    for obj in data:
        writer.writerow([_cell(obj.get(c)) for c in columns])
    return out.getvalue()


def _default(o):
    if isinstance(o, (datetime.date, datetime.datetime)):
        return o.isoformat()
    if isinstance(o, Decimal):
        return str(o)
    if isinstance(o, (set, frozenset)):
        return sorted(o)
    raise TypeError(f"{type(o).__name__} не сериализуется в JSON")


def dumps_stable(obj):
    return json.dumps(obj, default=_default, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def pack_message(version, text):
    payload = text.encode("utf-8")
    try:
        return _HEADER.pack(version, len(payload)) + payload
    except struct.error as exc:
        raise ValueError(str(exc)) from exc


def unpack_message(data):
    if len(data) < _HEADER.size:
        raise ValueError("слишком короткое сообщение")
    version, length = _HEADER.unpack_from(data)
    payload = data[_HEADER.size:]
    if len(payload) != length:
        raise ValueError("длина не совпадает с заголовком")
    return version, payload.decode("utf-8")


def sum_prices(json_text):
    items = json.loads(json_text, parse_float=Decimal)
    return sum((Decimal(item["price"]) for item in items), Decimal(0))
