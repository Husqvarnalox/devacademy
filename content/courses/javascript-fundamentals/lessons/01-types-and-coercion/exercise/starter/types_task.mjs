// Типы и приведение: четыре небольшие функции, на которых легко споткнуться.

/**
 * Точное имя типа: как typeof, но "null" для null, "array" для массивов и "nan" для NaN.
 */
export function typeOf(value) {
  // TODO: typeof null === "object" и typeof [] === "object" - этого мало
  return typeof value;
}

/**
 * Строгий разбор числа из строки. Возвращает конечное число или null.
 * "" и строки из пробелов, "12px", "Infinity", "abc" и любые не-строки дают null.
 */
export function toNumberStrict(text) {
  // TODO: Number("") равно 0, а Number("  ") тоже 0 - это не число
  return Number(text);
}

/** true только для числа -0. */
export function isNegativeZero(x) {
  // TODO: -0 === 0 истинно, нужен другой способ сравнения
  return x === 0;
}

/**
 * Точная сумма целых чисел, записанных строками (они могут быть больше Number.MAX_SAFE_INTEGER).
 * Результат - строка. Пустой массив даёт "0".
 */
export function bigSum(digitStrings) {
  // TODO: сумма обычных чисел теряет точность за пределами 2**53
  return String(digitStrings.reduce((sum, s) => sum + Number(s), 0));
}
