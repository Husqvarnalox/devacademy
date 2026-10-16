CREATE TABLE settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE learner_profile (
    id          INTEGER PRIMARY KEY CHECK (id = 1),
    goal        TEXT    NOT NULL,
    level       INTEGER NOT NULL,
    depth       TEXT    NOT NULL,
    ui_language TEXT    NOT NULL
);

CREATE TABLE enrolled_courses (
    course_id TEXT PRIMARY KEY,
    position  INTEGER NOT NULL
);

-- No foreign key to content: progress of removed lessons is kept as orphaned history.
CREATE TABLE lesson_progress (
    lesson_id        TEXT PRIMARY KEY,
    started_at       TEXT,
    completed_at     TEXT,
    quiz_best_score  REAL,
    exercise_passed  INTEGER NOT NULL,
    challenge_passed INTEGER NOT NULL,
    theory_read      INTEGER NOT NULL,
    attempts         INTEGER NOT NULL,
    failed_attempts  INTEGER NOT NULL,
    review_step      INTEGER NOT NULL,
    next_review_at   TEXT,
    last_reviewed_at TEXT,
    completion_source TEXT
);

CREATE TABLE submissions (
    id           TEXT PRIMARY KEY,
    lesson_id    TEXT    NOT NULL,
    exercise_id  TEXT    NOT NULL,
    language_id  TEXT    NOT NULL,
    files_json   TEXT    NOT NULL,
    verdict      TEXT    NOT NULL,
    passed       INTEGER NOT NULL,
    total        INTEGER NOT NULL,
    duration_ms  INTEGER NOT NULL,
    submitted_at TEXT    NOT NULL
);
CREATE INDEX ix_submissions_lesson ON submissions (lesson_id, submitted_at);

CREATE TABLE quiz_attempts (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    lesson_id    TEXT    NOT NULL,
    score        REAL    NOT NULL,
    passed       INTEGER NOT NULL,
    attempted_at TEXT    NOT NULL
);
CREATE INDEX ix_quiz_attempts_lesson ON quiz_attempts (lesson_id, attempted_at);

CREATE TABLE drafts (
    exercise_id TEXT NOT NULL,
    file_path   TEXT NOT NULL,
    content     TEXT NOT NULL,
    updated_at  TEXT NOT NULL,
    PRIMARY KEY (exercise_id, file_path)
);

CREATE TABLE revealed_hints (
    exercise_id TEXT PRIMARY KEY,
    level       INTEGER NOT NULL
);

-- Metadata only: no prompt or response text is stored.
CREATE TABLE ai_requests (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    provider        TEXT    NOT NULL,
    model           TEXT    NOT NULL,
    scenario        TEXT    NOT NULL,
    started_at      TEXT    NOT NULL,
    input_tokens    INTEGER,
    output_tokens   INTEGER,
    approx_cost_usd REAL,
    status          TEXT    NOT NULL
);
