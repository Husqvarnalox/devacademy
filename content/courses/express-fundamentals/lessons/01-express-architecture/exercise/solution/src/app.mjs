import express from "express";

const GREETINGS = { en: "Hello", ru: "Привет", es: "Hola" };

function toNumber(value) {
  return typeof value === "string" && value.trim() !== "" ? Number(value) : NaN;
}

export function createApp() {
  const app = express();

  app.get("/hello/:name", (req, res) => {
    const lang = req.query.lang ?? "en";
    if (typeof lang !== "string" || !Object.hasOwn(GREETINGS, lang)) {
      return res.status(400).json({ error: "unsupported lang" });
    }
    res.json({ greeting: `${GREETINGS[lang]}, ${req.params.name}!` });
  });

  app.get("/sum", (req, res) => {
    const a = toNumber(req.query.a);
    const b = toNumber(req.query.b);
    if (!Number.isFinite(a) || !Number.isFinite(b)) {
      return res.status(400).json({ error: "a and b must be numbers" });
    }
    res.json({ sum: a + b });
  });

  app.get("/files/:name{.:ext}", (req, res) => {
    res.json({ name: req.params.name, ext: req.params.ext ?? null });
  });

  app.get("/static/*splat", (req, res) => {
    res.json({ segments: req.params.splat });
  });

  app.get("/tags", (req, res) => {
    res.json({ tags: [].concat(req.query.tag ?? []) });
  });

  app.use((req, res) => {
    res.status(404).json({ error: "not found", path: req.path });
  });

  return app;
}
