// Строгий разбор и сравнение значений. Не используйте ==.

/**
 * Разбирает целое из строки: знак, цифры, пробелы по краям.
 * Возвращает безопасное целое или null. Для "-0" возвращает 0.
 */
export function parseIntStrict(text) {
  // TODO: Number("") равно 0, а Number("1.5") и Number("0x10") - числа
  return Number(text);
}

/**
 * Сравнение как у includes: === , но NaN равен NaN; 0 равен -0; типы не приводятся.
 */
export function sameValueZero(a, b) {
  // TODO: NaN === NaN ложно
  return a === b;
}

/**
 * "true"/"yes"/"1"/1/true -> true; "false"/"no"/"0"/0/-0/false -> false; остальное -> null.
 */
export function parseBool(value) {
  // TODO: Boolean("false") истинно, поэтому приведение здесь не подходит
  return Boolean(value);
}
