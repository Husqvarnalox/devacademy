import hashlib
import json


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    # TODO: json.dumps с параметрами, которые делают запись однозначной (см. условие)
    return json.dumps(obj)


def config_fingerprint(cfg):
    """Первые 12 шестнадцатеричных символов SHA-256 от канонической формы конфигурации."""
    # TODO: хеш от canonical_json(cfg), закодированной в UTF-8
    return ""


def bucket(object_id, salt):
    """Номер корзины 0..99 для объекта: зависит только от id и соли."""
    digest = hashlib.sha256(f"{salt}:{object_id}".encode("utf-8")).hexdigest()
    return int(digest, 16) % 100


def hash_split(ids, test_percent, salt):
    """Делит ids на (train, test) по хешу: объект в test, если его корзина меньше test_percent."""
    # TODO: проверка диапазона test_percent и разбиение с сохранением порядка
    return list(ids), []
