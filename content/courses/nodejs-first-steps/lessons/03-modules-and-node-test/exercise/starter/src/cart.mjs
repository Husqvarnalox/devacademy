// Модуль корзины. Он опирается на money.mjs: цены в строках корзины приходят текстом.
import { formatPrice, parsePrice } from "./money.mjs";

/**
 * Стоимость одной строки { name, price, qty } в копейках: parsePrice(price) * qty.
 * qty должно быть целым числом не меньше 1, иначе RangeError.
 */
export function lineTotal(line) {
  throw new Error("lineTotal не реализована");
}

/** Сумма стоимостей всех строк в копейках. Для пустой корзины - 0. */
export function cartTotal(lines) {
  throw new Error("cartTotal не реализована");
}

/**
 * Чек: массив строк. По одной строке на позицию вида "Чай x2 = 7.00",
 * затем последняя строка "Итого: 7.00". Для пустой корзины - ["Итого: 0.00"].
 */
export function receipt(lines) {
  throw new Error("receipt не реализована");
}
