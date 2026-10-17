import express from "express";

// Книги одного автора. mergeParams нужен, чтобы увидеть :authorId из пути родителя.
function createAuthorBooksRouter(store) {
  const router = express.Router({ mergeParams: true });
  router.get("/", (req, res) => {
    const items = store.books.filter((b) => b.authorId === req.author.id);
    res.json({ author: req.author, items });
  });
  return router;
}

export function createAuthorsRouter(store) {
  const router = express.Router();

  router.param("authorId", (req, res, next, value) => {
    const author = /^\d+$/.test(value) ? store.authors.find((a) => a.id === Number(value)) : undefined;
    if (!author) return res.status(404).json({ error: "author not found" });
    req.author = author;
    next();
  });

  router.get("/", (req, res) => {
    res.json({ items: store.authors });
  });

  router.get("/:authorId", (req, res) => {
    res.json(req.author);
  });

  router.use("/:authorId/books", createAuthorBooksRouter(store));

  return router;
}
