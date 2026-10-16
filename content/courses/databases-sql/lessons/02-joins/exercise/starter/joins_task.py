# Таблицы:
#   customers(id, name)
#   orders(id, customer_id, total)      -- customer_id может быть NULL
#   order_items(order_id, product, qty)
# Внешние ключи в тестовой базе НЕ включены: в orders могут быть строки
# с несуществующим customer_id и с customer_id IS NULL.
# Каждая константа - ОДИН запрос SELECT.

# Столбцы order_id, name, total: заказы вместе с именем клиента; порядок по order_id.
# Заказы без известного клиента в результат не входят.
Q_ORDERS_WITH_NAMES = "SELECT 1"

# Столбцы name, order_id: ВСЕ клиенты, у клиента без заказов order_id равен NULL.
# Порядок: name, затем order_id.
Q_ALL_CUSTOMERS = "SELECT 1"

# Имена клиентов, у которых нет ни одного заказа. Порядок по name.
# Запрос должен давать верный ответ и когда в orders есть customer_id NULL.
Q_NO_ORDERS = "SELECT 1"

# id заказов, у которых customer_id задан, но такого клиента нет в customers. Порядок по id.
Q_ORPHAN_ORDERS = "SELECT 1"

# Столбцы name, product, qty: позиции заказов вместе с именем клиента
# Порядок: name, product, qty.
Q_ITEMS = "SELECT 1"

# Различные имена клиентов, у которых есть хотя бы одна позиция с qty >= 2. Порядок по name.
Q_BIG_BUYERS = "SELECT 1"
