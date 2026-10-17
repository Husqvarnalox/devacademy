import re
import sys

TRUE = {"true", "1", "yes", "on"}
FALSE = {"false", "0", "no", "off"}


def redact_url(value):
    """postgres://user:pass@host/db -> postgres://user:***@host/db (пароль только в userinfo)."""
    # TODO
    return value


def main():
    types, values, sources, errors = {}, {}, {}, []
    section = None
    for raw in sys.stdin.read().splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1].lower()
            continue
        # TODO: [defaults] ключ:тип=значение | ключ:тип (обязательный);
        #       [file] ключ=значение; [env] APP_КЛЮЧ=значение; [flags] --ключ=значение или --ключ
        #       Запоминайте values[ключ] и sources[ключ] ("default", "file", "env", "flags").
    # TODO: приведите типы, соберите ошибки и напечатайте результат (см. условие)


main()
