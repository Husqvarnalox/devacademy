// Утилитарные типы на практике. Слово export делает объявление видимым для проверки.

export interface User {
  id: number;
  name: string;
  age: number;
  email?: string;
}

// Возвращает НОВЫЙ объект: поля user, поверх них - поля patch.
// Поля патча со значением undefined игнорируются (значение остаётся прежним). Исходный user не меняется.
export function applyPatch(user: Readonly<User>, patch: Partial<User>): User {
  // TODO
  return user;
}

// Новый объект только с перечисленными ключами (ключи, которых нет в объекте, пропускаются).
export function pick<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Pick<T, K> {
  // TODO
  return obj;
}

// Новый объект без перечисленных ключей. Исходный объект не меняется.
export function omit<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Omit<T, K> {
  // TODO
  return obj;
}

// Сколько раз встречается каждое слово: tally(["a", "b", "a"]) -> { a: 2, b: 1 }.
// Слова могут совпадать с именами вроде "constructor" и "toString": они должны считаться как обычные.
export function tally(words: readonly string[]): Record<string, number> {
  // TODO
  return {};
}
