import hashlib
import json


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def config_fingerprint(cfg):
    """Первые 12 шестнадцатеричных символов SHA-256 от канонической формы конфигурации."""
    return hashlib.sha256(canonical_json(cfg).encode("utf-8")).hexdigest()[:12]


def bucket(object_id, salt):
    """Номер корзины 0..99 для объекта: зависит только от id и соли."""
    digest = hashlib.sha256(f"{salt}:{object_id}".encode("utf-8")).hexdigest()
    return int(digest, 16) % 100


def hash_split(ids, test_percent, salt):
    """Делит ids на (train, test) по хешу: объект в test, если его корзина меньше test_percent."""
    if not 0 <= test_percent <= 100:
        raise ValueError(f"test_percent должен быть от 0 до 100, получено {test_percent}")
    train, test = [], []
    for object_id in ids:
        (test if bucket(object_id, salt) < test_percent else train).append(object_id)
    return train, test
