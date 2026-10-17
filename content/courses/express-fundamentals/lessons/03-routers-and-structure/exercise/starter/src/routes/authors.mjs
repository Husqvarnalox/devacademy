import express from "express";

// createAuthorsRouter(store) возвращает Router ресурса «авторы». store.authors - массив { id, name }.
// Монтируется на /api/authors.
//
//   * router.param("authorId", ...): цифры и существование в store.authors, иначе 404 { error: "author not found" };
//     найденного автора кладите в req.author;
//   * GET /           -> { items: store.authors }
//   * GET /:authorId  -> автор (req.author)
//   * GET /:authorId/books -> { author: req.author, items: книги этого автора (b.authorId === автор.id) }.
//     Сделайте это вложенным роутером express.Router({ mergeParams: true }), подключённым через
//     router.use("/:authorId/books", ...): без mergeParams вложенный роутер не увидит :authorId.
export function createAuthorsRouter(store) {
  const router = express.Router();

  // TODO

  return router;
}
