"""Конфигурация из файла и переменных окружения, секреты и структурное логирование."""

import json
import logging
import re
import tomllib
from dataclasses import dataclass
from datetime import datetime, timezone

LEVELS = ("DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL")
TRUE_WORDS = frozenset({"1", "true", "yes", "on"})
FALSE_WORDS = frozenset({"0", "false", "no", "off"})
_KEY = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")


class ConfigError(ValueError):
    """Все найденные проблемы конфигурации лежат в .problems; значения секретов в них не попадают."""

    def __init__(self, problems):
        self.problems = list(problems)
        super().__init__("; ".join(self.problems))


class Secret:
    """Обёртка для секретного значения: repr и str скрывают его, получить можно только reveal()."""

    __slots__ = ("_value",)

    def __init__(self, value):
        object.__setattr__(self, "_value", value)

    def reveal(self):
        return self._value

    def __repr__(self):
        return "Secret(***)"

    def __str__(self):
        return "***"

    def __eq__(self, other):
        return isinstance(other, Secret) and self._value == other._value

    def __hash__(self):
        return hash(("Secret", self._value))


@dataclass(frozen=True)
class Config:
    log_level: str
    port: int
    debug: bool
    api_token: Secret


def parse_dotenv(text):
    result = {}
    for number, raw in enumerate(text.splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("export "):
            line = line[len("export "):].lstrip()
        key, sep, value = line.partition("=")
        key = key.strip()
        if not sep or not _KEY.fullmatch(key):
            raise ValueError(f"строка {number}: ожидалось KEY=VALUE")
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] == '"':
            value = re.sub(r"\\(.)", lambda m: "\n" if m.group(1) == "n" else m.group(1), value[1:-1])
        elif len(value) >= 2 and value[0] == value[-1] == "'":
            value = value[1:-1]
        else:
            value = value.split(" #", 1)[0].rstrip()
        result[key] = value
    return result


def _as_bool(value):
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        word = value.strip().lower()
        if word in TRUE_WORDS:
            return True
        if word in FALSE_WORDS:
            return False
    raise ValueError("ожидалось логическое значение (true/false, 1/0, yes/no, on/off)")


def _as_port(value):
    if isinstance(value, bool):
        raise ValueError("ожидалось целое число")
    if isinstance(value, str):
        value = value.strip()
        if not re.fullmatch(r"[0-9]+", value):
            raise ValueError("ожидалось целое число")
        value = int(value)
    if not isinstance(value, int):
        raise ValueError("ожидалось целое число")
    if not 1 <= value <= 65535:
        raise ValueError("порт должен быть в диапазоне 1..65535")
    return value


def _as_level(value):
    if not isinstance(value, str) or value.strip().upper() not in LEVELS:
        raise ValueError("допустимы " + ", ".join(LEVELS))
    return value.strip().upper()


def _as_token(value):
    if not isinstance(value, str) or not value.strip():
        raise ValueError("нужна непустая строка")
    return Secret(value)


_FIELDS = (
    ("log_level", _as_level, "INFO"),
    ("port", _as_port, 8080),
    ("debug", _as_bool, False),
    ("api_token", _as_token, None),
)


def load_config(file_text, env, *, prefix="APP_"):
    """Приоритет: значения по умолчанию < файл (TOML) < переменные окружения с префиксом."""
    problems = []
    try:
        from_file = tomllib.loads(file_text) if file_text else {}
    except tomllib.TOMLDecodeError as exc:
        raise ConfigError([f"file: некорректный TOML ({exc})"]) from None
    known = {name for name, _, _ in _FIELDS}
    from_env = {}
    for key, value in env.items():
        if key.startswith(prefix):
            name = key[len(prefix):].lower()
            if name in known:
                from_env[name] = value
            else:
                problems.append(f"{key}: неизвестная переменная")
    for key in from_file:
        if key not in known:
            problems.append(f"{key}: неизвестный ключ в файле")
    values = {}
    for name, convert, default in _FIELDS:
        if name in from_env:
            raw = from_env[name]
        elif name in from_file:
            raw = from_file[name]
        elif default is not None:
            values[name] = default
            continue
        else:
            problems.append(f"{name}: обязательный параметр не задан")
            continue
        try:
            values[name] = convert(raw)
        except ValueError as exc:
            problems.append(f"{name}: {exc}")
    if problems:
        order = {name: i for i, (name, _, _) in enumerate(_FIELDS)}
        problems.sort(key=lambda p: (order.get(p.split(":")[0], 99), p))
        raise ConfigError(problems)
    return Config(**values)


_STANDARD = frozenset(logging.LogRecord("n", 0, "p", 0, "m", (), None).__dict__) | {"message", "asctime", "taskName"}
SENSITIVE_PARTS = ("password", "passwd", "secret", "token", "authorization", "api_key", "apikey")


class JsonFormatter(logging.Formatter):
    """Одна запись - одна строка JSON с отсортированными ключами; extra-поля включаются в запись."""

    RESERVED = ("level", "logger", "message", "time", "exc")

    def __init__(self, include_time=False):
        super().__init__()
        self.include_time = include_time

    def format(self, record):
        payload = {"level": record.levelname, "logger": record.name, "message": record.getMessage()}
        if self.include_time:
            payload["time"] = datetime.fromtimestamp(record.created, timezone.utc).isoformat(timespec="milliseconds")
        for key, value in record.__dict__.items():
            if key in _STANDARD or key.startswith("_"):
                continue
            payload[f"extra.{key}" if key in self.RESERVED else key] = value
        if record.exc_info or record.exc_text:
            payload["exc"] = record.exc_text or self.formatException(record.exc_info)
        return json.dumps(payload, ensure_ascii=False, sort_keys=True, default=str)


class RedactingFilter(logging.Filter):
    def __init__(self, secrets=(), mask="***"):
        super().__init__()
        self.secrets = tuple(s for s in secrets if isinstance(s, str) and s)
        self.mask = mask

    def _scrub(self, text):
        for secret in self.secrets:
            text = text.replace(secret, self.mask)
        return text

    def filter(self, record):
        record.msg = self._scrub(record.getMessage())
        record.args = ()
        for key, value in list(record.__dict__.items()):
            if key in _STANDARD or key.startswith("_"):
                continue
            if any(part in key.lower() for part in SENSITIVE_PARTS) or isinstance(value, Secret):
                record.__dict__[key] = self.mask
            elif isinstance(value, str):
                record.__dict__[key] = self._scrub(value)
        if record.exc_info and not record.exc_text:
            record.exc_text = logging.Formatter().formatException(record.exc_info)
        if record.exc_text:
            record.exc_text = self._scrub(record.exc_text)
        return True
