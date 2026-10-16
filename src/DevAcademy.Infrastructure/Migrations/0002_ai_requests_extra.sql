-- AI request metadata: completion time and error kind (still no prompt/response text).
ALTER TABLE ai_requests ADD COLUMN finished_at TEXT;
ALTER TABLE ai_requests ADD COLUMN error_kind  TEXT;
CREATE INDEX idx_ai_requests_started ON ai_requests (started_at);
