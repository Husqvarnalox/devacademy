import sqlite3

SCHEMA = """
CREATE TABLE students(
    id    INTEGER PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    name  TEXT NOT NULL
);
CREATE TABLE courses(
    id    INTEGER PRIMARY KEY,
    title TEXT NOT NULL UNIQUE
);
CREATE TABLE enrollments(
    student_id INTEGER NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    course_id  INTEGER NOT NULL REFERENCES courses(id) ON DELETE RESTRICT,
    grade      INTEGER CHECK (grade BETWEEN 1 AND 5),
    PRIMARY KEY (student_id, course_id)
);
"""


def connect(path=":memory:"):
    conn = sqlite3.connect(path)
    conn.execute("PRAGMA foreign_keys = ON")
    conn.executescript(SCHEMA)
    return conn
