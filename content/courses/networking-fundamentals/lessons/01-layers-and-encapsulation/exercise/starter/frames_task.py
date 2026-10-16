import sys

# Вход: одна строка вида "payload=3000 transport=8 network=20 link=18 mtu=1500 [df=1]".
fields = {}
for token in sys.stdin.read().split():
    key, _, value = token.partition("=")
    fields[key] = int(value)

# TODO: посчитайте ip_payload, число фрагментов и размер каждого кадра по условию задачи.
print("not implemented")
