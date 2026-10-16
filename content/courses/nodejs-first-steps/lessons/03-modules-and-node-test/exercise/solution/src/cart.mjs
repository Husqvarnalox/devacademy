// Модуль корзины. Он опирается на money.mjs: цены в строках корзины приходят текстом.
import { formatPrice, parsePrice } from "./money.mjs";

/**
 * Стоимость одной строки { name, price, qty } в копейках: parsePrice(price) * qty.
 * qty должно быть целым числом не меньше 1, иначе RangeError.
 */
export function lineTotal(line) {
  if (!Number.isInteger(line.qty) || line.qty < 1) {
    throw new RangeError(`Некорректное количество: ${line.qty}`);
  }
  return parsePrice(line.price) * line.qty;
}

/** Сумма стоимостей всех строк в копейках. Для пустой корзины - 0. */
export function cartTotal(lines) {
  return lines.reduce((sum, line) => sum + lineTotal(line), 0);
}

/**
 * Чек: массив строк. По одной строке на позицию вида "Чай x2 = 7.00",
 * затем последняя строка "Итого: 7.00". Для пустой корзины - ["Итого: 0.00"].
 */
export function receipt(lines) {
  const rows = lines.map((line) => `${line.name} x${line.qty} = ${formatPrice(lineTotal(line))}`);
  return [...rows, `Итого: ${formatPrice(cartTotal(lines))}`];
}
