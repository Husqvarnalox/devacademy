import express from "express";
import { securityHeaders, cors, rateLimit, setSessionCookie } from "./security.mjs";

export function createApp({ allowedOrigins = [], limit = 3, windowMs = 60_000, now = Date.now } = {}) {
  const app = express();
  app.use(securityHeaders());
  app.use(cors({ allowedOrigins }));
  app.use("/api", rateLimit({ limit, windowMs, now }));

  app.get("/", (req, res) => res.json({ ok: true }));
  app.get("/api/ping", (req, res) => res.json({ pong: true }));
  app.post("/login", (req, res) => {
    setSessionCookie(res, "abc123");
    res.status(204).end();
  });
  app.use((req, res) => res.status(404).json({ error: { code: "not_found", message: "Not found" } }));
  return app;
}
