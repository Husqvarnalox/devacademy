// Модуль денег. Цены хранятся целым числом копеек, чтобы не копить ошибки округления дробных чисел.

/**
 * Разбирает строку цены в копейки: "12.5" -> 1250, "7" -> 700, "0.05" -> 5.
 * Допустимый вид: целая часть из цифр и необязательная дробная часть из одной или двух цифр.
 * Пробелы по краям игнорируются. Любая другая строка - RangeError; не строка - TypeError.
 */
export function parsePrice(text) {
  if (typeof text !== "string") {
    throw new TypeError(`Цена должна быть строкой, получено: ${typeof text}`);
  }
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(text.trim());
  if (match === null) {
    throw new RangeError(`Некорректная цена: "${text}"`);
  }
  const [, whole, fraction = ""] = match;
  const cents = Number(whole) * 100 + Number(fraction.padEnd(2, "0"));
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError(`Слишком большая цена: "${text}"`);
  }
  return cents;
}

/**
 * Превращает копейки в строку с двумя знаками: 1250 -> "12.50", 5 -> "0.05".
 * Допустимы только неотрицательные целые числа, иначе RangeError.
 */
export function formatPrice(cents) {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError(`Некорректная сумма в копейках: ${cents}`);
  }
  const whole = Math.floor(cents / 100);
  const fraction = String(cents % 100).padStart(2, "0");
  return `${whole}.${fraction}`;
}
