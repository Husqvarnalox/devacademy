Q_LOWEST_PAID = """
SELECT name, salary FROM employees
WHERE salary IS NOT NULL
ORDER BY salary ASC, name ASC
LIMIT 2
"""

Q_NO_MANAGER = """
SELECT name FROM employees
WHERE manager_id IS NULL
ORDER BY name
"""

Q_PAGE2 = """
SELECT id, name FROM employees
ORDER BY id
LIMIT 2 OFFSET 2
"""

Q_NOT_SALES = """
SELECT id FROM employees
WHERE dept IS NULL OR dept <> 'Sales'
ORDER BY id
"""

Q_ANNUAL = """
SELECT name, COALESCE(salary, 0) * 12 AS annual FROM employees
ORDER BY name
"""

Q_DEPTS = """
SELECT DISTINCT dept FROM employees
WHERE dept IS NOT NULL
ORDER BY dept
"""
