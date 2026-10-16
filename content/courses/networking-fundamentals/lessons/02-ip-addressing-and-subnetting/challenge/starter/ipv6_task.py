import sys

# Вход: по одному IPv6-адресу на строку (в любой допустимой записи). Пустые строки пропускаются.
for raw in sys.stdin.read().splitlines():
    line = raw.strip()
    if not line:
        continue
    # TODO: разверните адрес в 8 групп, затем сожмите по RFC 5952 и выведите
    # "<полная запись> <сжатая запись>" либо "<строка> -> invalid".
    print(line + " -> invalid")
