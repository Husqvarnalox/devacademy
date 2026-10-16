// Типы и приведение: четыре небольшие функции, на которых легко споткнуться.

/**
 * Точное имя типа: как typeof, но "null" для null, "array" для массивов и "nan" для NaN.
 */
export function typeOf(value) {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  if (Number.isNaN(value)) return "nan";
  return typeof value;
}

/**
 * Строгий разбор числа из строки. Возвращает конечное число или null.
 * "" и строки из пробелов, "12px", "Infinity", "abc" и любые не-строки дают null.
 */
export function toNumberStrict(text) {
  if (typeof text !== "string") return null;
  const trimmed = text.trim();
  if (trimmed === "") return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

/** true только для числа -0. */
export function isNegativeZero(x) {
  return Object.is(x, -0);
}

/**
 * Точная сумма целых чисел, записанных строками (они могут быть больше Number.MAX_SAFE_INTEGER).
 * Результат - строка. Пустой массив даёт "0".
 */
export function bigSum(digitStrings) {
  return digitStrings.reduce((sum, s) => sum + BigInt(s), 0n).toString();
}
