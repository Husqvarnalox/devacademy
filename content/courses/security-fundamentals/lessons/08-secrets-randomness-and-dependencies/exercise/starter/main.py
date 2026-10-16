import re
import sys

MASK = "[REDACTED]"

# TODO: заготовьте регулярные выражения для правил 1-4 из условия
#   1. пароль в URL:      scheme://user:pass@host
#   2. Bearer-токен
#   3. формы ключей:      AKIA..., ghp_..., xoxb-...
#   4. поля password/secret/token/api_key в формате key=value и "key": "value"


def redact(line):
    # TODO: примените правила по порядку и верните изменённую строку
    return line


for line in sys.stdin.read().splitlines():
    print(redact(line))
