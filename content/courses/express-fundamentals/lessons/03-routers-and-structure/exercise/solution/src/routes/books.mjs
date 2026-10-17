import express from "express";

// Роутер ресурса «книги». Хранилище приходит снаружи: так роутер не зависит от глобальных данных.
export function createBooksRouter(store) {
  const router = express.Router();

  // Middleware уровня роутера: действует на все запросы, попавшие сюда.
  router.use((req, res, next) => {
    res.set("X-Resource", "books");
    next();
  });

  // Обработчик параметра: выполняется для каждого маршрута с :id, до самого маршрута.
  router.param("id", (req, res, next, value) => {
    const book = /^\d+$/.test(value) ? store.books.find((b) => b.id === Number(value)) : undefined;
    if (!book) return res.status(404).json({ error: "book not found" });
    req.book = book;
    next();
  });

  router.get("/", (req, res) => {
    res.json({ items: store.books });
  });

  router.get("/:id", (req, res) => {
    res.json(req.book);
  });

  router.get("/:id/where", (req, res) => {
    res.json({ baseUrl: req.baseUrl, path: req.path, originalUrl: req.originalUrl });
  });

  return router;
}
