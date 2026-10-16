Q_ORDERS_WITH_NAMES = """
SELECT o.id AS order_id, c.name, o.total
FROM orders AS o
JOIN customers AS c ON c.id = o.customer_id
ORDER BY o.id
"""

Q_ALL_CUSTOMERS = """
SELECT c.name, o.id AS order_id
FROM customers AS c
LEFT JOIN orders AS o ON o.customer_id = c.id
ORDER BY c.name, o.id
"""

Q_NO_ORDERS = """
SELECT c.name FROM customers AS c
WHERE NOT EXISTS (SELECT 1 FROM orders AS o WHERE o.customer_id = c.id)
ORDER BY c.name
"""

Q_ORPHAN_ORDERS = """
SELECT o.id FROM orders AS o
LEFT JOIN customers AS c ON c.id = o.customer_id
WHERE o.customer_id IS NOT NULL AND c.id IS NULL
ORDER BY o.id
"""

Q_ITEMS = """
SELECT c.name, i.product, i.qty
FROM order_items AS i
JOIN orders AS o ON o.id = i.order_id
JOIN customers AS c ON c.id = o.customer_id
ORDER BY c.name, i.product, i.qty
"""

Q_BIG_BUYERS = """
SELECT DISTINCT c.name
FROM customers AS c
JOIN orders AS o ON o.customer_id = c.id
JOIN order_items AS i ON i.order_id = o.id
WHERE i.qty >= 2
ORDER BY c.name
"""
