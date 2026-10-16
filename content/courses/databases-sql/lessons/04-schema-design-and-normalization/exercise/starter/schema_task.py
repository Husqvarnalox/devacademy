import sqlite3

# Нормализованная схема учебной системы (студенты, курсы, записи на курсы).
#
#   students(id, email, name)
#       id     - целочисленный первичный ключ (автоматически)
#       email  - обязателен и уникален
#       name   - обязательно
#   courses(id, title)
#       id     - целочисленный первичный ключ (автоматически)
#       title  - обязательно и уникально
#   enrollments(student_id, course_id, grade)
#       первичный ключ - пара (student_id, course_id): студент записывается на курс один раз
#       student_id -> students(id): при удалении студента его записи удаляются (CASCADE)
#       course_id  -> courses(id):  курс с записями удалить нельзя (RESTRICT)
#       grade      - необязательная оценка, если задана - целое от 1 до 5
SCHEMA = """
CREATE TABLE students(id INTEGER PRIMARY KEY, name TEXT);
"""


def connect(path=":memory:"):
    """Открыть базу, включить проверку внешних ключей и создать таблицы из SCHEMA."""
    return sqlite3.connect(path)
