import { createHash } from "node:crypto";

// Строгий ETag: первые 16 шестнадцатеричных символов SHA-1 от JSON.stringify(value), в двойных кавычках.
export function etagOf(value) {
  const digest = createHash("sha1").update(JSON.stringify(value)).digest("hex").slice(0, 16);
  return `"${digest}"`;
}

const splitTags = (header) => header.split(",").map((tag) => tag.trim()).filter((tag) => tag !== "");
const stripWeak = (tag) => (tag.startsWith("W/") ? tag.slice(2) : tag);

// If-None-Match использует слабое сравнение: префикс W/ не учитывается.
export function ifNoneMatch(header, etag) {
  if (header === undefined) return false;
  if (header.trim() === "*") return true;
  return splitTags(header).some((tag) => stripWeak(tag) === stripWeak(etag));
}

// If-Match использует сильное сравнение: слабые теги (W/...) никогда не совпадают.
export function ifMatch(header, etag) {
  if (header === undefined) return true;
  if (header.trim() === "*") return true;
  return splitTags(header).some((tag) => !tag.startsWith("W/") && tag === etag);
}

// Ставит ETag; для GET/HEAD при совпадении If-None-Match отвечает 304 без тела, иначе JSON.
export function sendConditional(req, res, body) {
  const etag = etagOf(body);
  res.set("ETag", etag);
  if ((req.method === "GET" || req.method === "HEAD") && ifNoneMatch(req.get("If-None-Match"), etag)) {
    return res.status(304).end();
  }
  return res.json(body);
}
