import sys

# Вход: полный HTTP/1.1-ответ в виде байтов (строки заканчиваются CRLF).
data = sys.stdin.buffer.read()

# TODO: разберите статусную строку и заголовки, определите способ передачи тела
# (Transfer-Encoding: chunked, Content-Length или до конца потока), декодируйте тело и выведите результат.
print("not implemented")
