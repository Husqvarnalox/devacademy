-- Project workspace: recently opened project directories and the editor layout per project.
-- Only paths are stored (never file contents).
CREATE TABLE recent_projects (
    path           TEXT PRIMARY KEY,
    last_opened_at TEXT NOT NULL
);

CREATE TABLE project_sessions (
    project_path TEXT PRIMARY KEY,
    session_json TEXT NOT NULL,
    updated_at   TEXT NOT NULL
);
