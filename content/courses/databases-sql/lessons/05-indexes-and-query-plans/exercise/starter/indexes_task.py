# Таблица (3000 строк, индексов нет):
#   orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL, status TEXT NOT NULL,
#          created_at TEXT NOT NULL, total INTEGER NOT NULL, note TEXT)
#
# Пять запросов приложения (? - параметр):
#   Q1  SELECT * FROM orders WHERE customer_id = ?
#   Q2  SELECT * FROM orders WHERE customer_id = ? AND status = ?
#   Q3  SELECT * FROM orders WHERE status = ? AND created_at >= ?
#   Q4  SELECT customer_id, total FROM orders WHERE customer_id = ?     (покрывающий индекс!)
#   Q5  SELECT * FROM orders WHERE status = ?
#
# Заполните список INDEXES операторами CREATE INDEX (с уникальными именами) так, чтобы
# EXPLAIN QUERY PLAN показывал для всех пяти запросов SEARCH по индексу с нужными
# столбцами, а для Q4 - COVERING INDEX.
# Ограничения: не больше 3 индексов, и каждый индекс должен использоваться хотя бы
# одним из пяти запросов (лишние индексы замедляют запись и не принимаются).
INDEXES = []
