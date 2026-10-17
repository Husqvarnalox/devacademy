import express from "express";
import { createBooksRouter } from "./routes/books.mjs";
import { createAuthorsRouter } from "./routes/authors.mjs";

// createApp({ store }) собирает приложение из модулей. Само приложение только монтирует роутеры.
//
//   GET /api              -> { name: "library", resources: ["authors", "books"] }
//   /api/books/...        -> createBooksRouter(store)
//   /api/authors/...      -> createAuthorsRouter(store)
//   всё остальное         -> 404 { error: "not found" }
export function createApp({ store }) {
  const app = express();

  // TODO: смонтируйте роутеры и обработчик 404

  return app;
}
