import express from "express";
import { createBooksRouter } from "./routes/books.mjs";
import { createAuthorsRouter } from "./routes/authors.mjs";

export function createApp({ store }) {
  const app = express();

  app.get("/api", (req, res) => {
    res.json({ name: "library", resources: ["authors", "books"] });
  });
  app.use("/api/books", createBooksRouter(store));
  app.use("/api/authors", createAuthorsRouter(store));

  app.use((req, res) => {
    res.status(404).json({ error: "not found" });
  });

  return app;
}
