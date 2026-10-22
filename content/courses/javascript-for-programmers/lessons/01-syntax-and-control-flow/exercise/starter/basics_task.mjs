// Четыре небольшие функции на ловушки JavaScript. Не используйте ==.

/**
 * Сумма двух денежных сумм в долларах (не больше двух знаков после запятой), округлённая до центов.
 * addMoney(0.1, 0.2) должно быть ровно 0.3. Если аргумент не конечное число (строка, NaN, Infinity) - TypeError.
 */
export function addMoney(a, b) {
  return a + b;
}

/**
 * true для null, undefined и строк из одних пробелов (или пустых); для всего остального false
 * (в том числе для 0, false, NaN, "0" и []).
 */
export function isBlank(value) {
  return !value;
}

/**
 * Первый аргумент, который не null и не undefined. Значения 0, "", false и NaN подходят.
 * Если таких нет - undefined.
 */
export function firstDefined(...values) {
  return values.find((v) => v);
}

/**
 * Сумма тех элементов, которые являются конечными числами; строки, null, NaN и Infinity пропускаются.
 */
export function sumNumbers(values) {
  return values.reduce((total, v) => total + v, 0);
}
