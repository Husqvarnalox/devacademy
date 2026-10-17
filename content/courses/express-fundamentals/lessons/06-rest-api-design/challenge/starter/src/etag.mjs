import { createHash } from "node:crypto";

// etagOf(value) - строгий ETag: первые 16 шестнадцатеричных символов SHA-1 от JSON.stringify(value),
// в двойных кавычках, например "9f86d081884c7d65". Равные значения дают равные теги.
export function etagOf(value) {
  throw new Error("TODO: etagOf");
}

// ifNoneMatch(header, etag) -> boolean (слабое сравнение).
//   header === undefined -> false; "*" -> true;
//   иначе заголовок - список тегов через запятую (пробелы вокруг игнорируются);
//   теги сравниваются без префикса W/ (и в заголовке, и у etag).
export function ifNoneMatch(header, etag) {
  throw new Error("TODO: ifNoneMatch");
}

// ifMatch(header, etag) -> boolean (сильное сравнение).
//   header === undefined -> true (условия нет); "*" -> true;
//   иначе нужен точный тег из списка; слабые теги (W/"...") не совпадают никогда.
export function ifMatch(header, etag) {
  throw new Error("TODO: ifMatch");
}

// sendConditional(req, res, body): всегда ставит заголовок ETag = etagOf(body).
// Для GET и HEAD, если ifNoneMatch(req.get("If-None-Match"), etag) истинно - ответ 304 без тела (ETag сохраняется),
// иначе обычный res.json(body).
export function sendConditional(req, res, body) {
  throw new Error("TODO: sendConditional");
}
