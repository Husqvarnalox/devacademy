import express from "express";
import { requestId, accessLog, requireBearer } from "./middleware.mjs";

export function createApp({ generateId, log, tokens }) {
  const app = express();

  app.use(requestId({ generate: generateId }));
  app.use(accessLog(log));
  app.use(express.json());

  app.get("/health", (req, res) => {
    res.json({ status: "ok" });
  });

  const api = express.Router();
  api.use(requireBearer({ tokens }));
  api.get("/me", (req, res) => {
    res.json({ user: req.user, requestId: req.id });
  });
  api.post("/echo", (req, res) => {
    res.json({ echo: req.body ?? null });
  });
  app.use("/api", api);

  return app;
}
