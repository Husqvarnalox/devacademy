// Модуль артикулов. Формат артикула: три заглавные латинские буквы, дефис, четыре цифры (ABC-0001).

const SKU_PATTERN = /^[A-Z]{3}-\d{4}$/;

/** true, только если value - строка ровно такого формата (без пробелов и без смены регистра). */
export function isValidSku(value) {
  return typeof value === "string" && SKU_PATTERN.test(value);
}

/**
 * Приводит артикул к каноническому виду: обрезает пробелы и переводит в верхний регистр.
 * Не строка - TypeError. Если результат не соответствует формату - RangeError.
 */
export function normalizeSku(value) {
  if (typeof value !== "string") {
    throw new TypeError(`Артикул должен быть строкой, получено: ${typeof value}`);
  }
  const sku = value.trim().toUpperCase();
  if (!isValidSku(sku)) {
    throw new RangeError(`Некорректный артикул: "${value}"`);
  }
  return sku;
}
