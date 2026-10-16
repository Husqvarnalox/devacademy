import sys

# Вход: шестнадцатеричный дамп одной TLS-записи с ClientHello (пробелы и переводы строк игнорируются).
data = bytes.fromhex("".join(sys.stdin.read().split()))

# TODO: прочитайте заголовок записи (тип, версия, длина), заголовок рукопожатия, поля ClientHello
# и расширения; найдите SNI, supported_versions и ALPN.
print("not implemented")
