export interface User {
  id: number;
  name: string;
  email: string | null;
  tags: string[];
}

export type ParseResult = { ok: true; value: User } | { ok: false; error: string };

export function fail(message: string): never {
  throw new Error(message);
}

export function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}

export function isStringArray(v: unknown): v is string[] {
  return Array.isArray(v) && v.every((item) => typeof item === "string");
}

export function parseUser(input: unknown): ParseResult {
  if (!isRecord(input)) return { ok: false, error: "ожидался объект" };
  const { id, name, email, tags } = input;
  if (typeof id !== "number" || !Number.isInteger(id)) return { ok: false, error: "поле id: ожидалось целое число" };
  if (typeof name !== "string" || name.length === 0) return { ok: false, error: "поле name: ожидалась непустая строка" };
  if (email !== null && typeof email !== "string") return { ok: false, error: "поле email: ожидалась строка или null" };
  if (!isStringArray(tags)) return { ok: false, error: "поле tags: ожидался массив строк" };
  return { ok: true, value: { id, name, email, tags: [...tags] } };
}

export function parseUserJson(text: string): ParseResult {
  let data: unknown;
  try {
    data = JSON.parse(text);
  } catch {
    return { ok: false, error: "некорректный JSON" };
  }
  return parseUser(data);
}

export function describeError(e: unknown): string {
  if (e instanceof Error) return e.message;
  if (typeof e === "string") return e;
  return "неизвестная ошибка: " + String(e);
}

export function expectUser(input: unknown): User {
  const r = parseUser(input);
  return r.ok ? r.value : fail(r.error);
}
