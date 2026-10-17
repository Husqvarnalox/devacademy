import express from "express";

// createBooksRouter(store) возвращает Router ресурса «книги». store.books - массив { id, title, authorId }.
//
// Требования (роутер монтируется приложением на /api/books):
//   * router.use(...): всем ответам этого роутера добавляется заголовок X-Resource: books;
//   * router.param("id", ...): значение должно быть целым числом из цифр и существовать в store.books, иначе
//     ответ 404 { error: "book not found" }. Найденную книгу кладите в req.book и вызывайте next();
//   * GET /       -> { items: store.books }
//   * GET /:id    -> книга (req.book)
//   * GET /:id/where -> { baseUrl: req.baseUrl, path: req.path, originalUrl: req.originalUrl }
export function createBooksRouter(store) {
  const router = express.Router();

  // TODO

  return router;
}
