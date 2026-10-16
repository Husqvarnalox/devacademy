import sys

# Вход: шестнадцатеричный дамп заголовка IPv4 (пробелы и переводы строк игнорируются).
text = "".join(sys.stdin.read().split())
data = bytes.fromhex(text)

# TODO: разберите поля заголовка (version, ihl, dscp/ecn, длины, флаги, TTL, протокол, адреса),
# посчитайте контрольную сумму по RFC 1071 и выведите результат по условию задачи.
print("not implemented")
