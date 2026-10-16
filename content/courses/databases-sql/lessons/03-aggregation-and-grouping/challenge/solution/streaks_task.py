Q_STREAKS = """
WITH d AS (
  SELECT DISTINCT user, day FROM logins
),
g AS (
  SELECT user, day,
         julianday(day) - ROW_NUMBER() OVER (PARTITION BY user ORDER BY day) AS grp
  FROM d
)
SELECT user, MIN(day) AS start_day, MAX(day) AS end_day, COUNT(*) AS days
FROM g
GROUP BY user, grp
HAVING COUNT(*) >= 2
ORDER BY user, start_day
"""
