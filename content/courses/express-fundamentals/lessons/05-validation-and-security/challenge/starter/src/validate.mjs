// Проверка тела запроса по схеме.
//
// schema: { имяПоля: { type, required?, min?, max?, enum?, pattern?, items? } }
//   type: "string" | "integer" | "number" | "boolean" | "array"
//   min / max: длина строки, значение числа или длина массива
//   enum: список допустимых значений; pattern: RegExp (только для строк)
//   items: правило для каждого элемента массива (те же ключи)
//
// validate(body, schema) возвращает МАССИВ ошибок { field, code, message } (пустой, если всё хорошо):
//   - body не объект (null, массив, строка, undefined): единственная ошибка { field: "", code: "invalid_body", ... };
//   - сначала поля схемы в порядке схемы, затем неизвестные поля тела в порядке ключей;
//   - поле отсутствует (undefined): если required - code "required", иначе пропустить; null - это ошибка "type";
//   - неверный тип: code "type" и остальные проверки этого поля пропускаются;
//   - коды min, max, enum, pattern - по одной ошибке на каждое нарушенное правило;
//   - элементы массива проверяются с полем вида "tags[1]";
//   - неизвестное поле: code "unknown_field" (в том числе "__proto__" из JSON.parse);
//   - message - любая непустая строка. Ошибок может быть несколько сразу.
export function validate(body, schema) {
  throw new Error("TODO: validate");
}

// validateBody(schema) - middleware. Есть ошибки: 422 и
// { "error": { "code": "validation_failed", "message": "Request body is invalid", "details": { "errors": [...] } } },
// иначе next().
export function validateBody(schema) {
  return (req, res, next) => {
    throw new Error("TODO: validateBody");
  };
}
