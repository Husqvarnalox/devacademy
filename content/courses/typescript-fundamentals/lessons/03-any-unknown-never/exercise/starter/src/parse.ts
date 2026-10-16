// Граница программы: данные приходят как unknown, а внутрь попадает только проверенный User.
// Проверки пишите руками, без приведений `as User` и без any.

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
  // TODO: объект, не null и не массив
  return false;
}

export function isStringArray(v: unknown): v is string[] {
  // TODO: массив, все элементы которого - строки
  return false;
}

export function parseUser(input: unknown): ParseResult {
  // TODO: проверить поля id (целое число), name (непустая строка), email (строка или null), tags (массив строк).
  // Для первой найденной проблемы вернуть { ok: false, error: "поле <имя>: ..." }; если input не объект - error "ожидался объект".
  return { ok: false, error: "не реализовано" };
}

export function parseUserJson(text: string): ParseResult {
  // TODO: JSON.parse в try/catch; при синтаксической ошибке - { ok: false, error: "некорректный JSON" }; иначе parseUser
  return { ok: false, error: "не реализовано" };
}

export function describeError(e: unknown): string {
  // TODO: Error -> его message; строка -> она сама; иначе "неизвестная ошибка: " + String(e)
  return "";
}

export function expectUser(input: unknown): User {
  // TODO: вернуть value или вызвать fail(error)
  return fail("не реализовано");
}
