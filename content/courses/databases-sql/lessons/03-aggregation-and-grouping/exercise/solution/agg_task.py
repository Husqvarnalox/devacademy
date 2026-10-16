Q_DEPT_STATS = """
SELECT dept, COUNT(*) AS n, SUM(salary) AS total
FROM employees
WHERE dept IS NOT NULL
GROUP BY dept
ORDER BY dept
"""

Q_BIG_DEPTS = """
SELECT dept FROM employees
WHERE dept IS NOT NULL
GROUP BY dept
HAVING COUNT(*) >= 2
ORDER BY dept
"""

Q_COUNTS = """
SELECT COUNT(*) AS total_rows, COUNT(salary) AS with_salary, COUNT(DISTINCT dept) AS distinct_depts
FROM employees
"""

Q_RANK = """
SELECT id, dept,
       ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC, id) AS rn
FROM employees
WHERE dept IS NOT NULL
ORDER BY dept, rn
"""

Q_RUNNING = """
SELECT id, SUM(COALESCE(salary, 0)) OVER (ORDER BY id) AS running
FROM employees
ORDER BY id
"""

Q_TOP_PER_DEPT = """
SELECT dept, name FROM (
  SELECT dept, name,
         ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC, id) AS rn
  FROM employees
  WHERE dept IS NOT NULL
)
WHERE rn = 1
ORDER BY dept
"""
