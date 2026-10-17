import express from "express";

// createApp() возвращает приложение Express: это обычная функция (req, res), которую можно
// подать в http.createServer или, как в тестах курса, прямо в light-my-request. Порт не открывается.
//
// Маршруты (все ответы - JSON):
//   GET /hello/:name?lang=en|ru|es   -> { greeting: "Hello, Ann!" } ("Привет", "Hola"); lang по умолчанию en;
//                                       неизвестный lang -> 400 { error: "unsupported lang" }
//   GET /sum?a=1&b=2                 -> { sum: 3 }; не числа или параметр пропущен -> 400 { error: "a and b must be numbers" }
//   GET /files/:name{.:ext}          -> { name: "report", ext: "pdf" }; без расширения ext равен null
//   GET /static/*splat               -> { segments: ["css", "app.css"] }
//   GET /tags?tag=a&tag=b            -> { tags: ["a", "b"] }; один tag даёт массив из одного элемента, ни одного - пустой массив
//   всё остальное                    -> 404 { error: "not found", path: "/то/что/запросили" }
export function createApp() {
  const app = express();

  // TODO: добавьте маршруты и обработчик 404

  return app;
}
