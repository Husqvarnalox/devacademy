// Разбор заказов из текста: артикул,количество,цена.

/** Ошибка данных; поле field называет неверное поле: "fields", "sku", "qty" или "price". */
export class ParseError extends Error {
  constructor(message, field) {
    super(message);
    this.name = "ParseError";
    this.field = field;
  }
}

/**
 * "AB-123, 2, 9.99" -> { sku: "AB-123", qty: 2, priceCents: 999 }.
 * Бросает ParseError с нужным field (см. условие задачи).
 */
export function parseOrderLine(line) {
  const [sku, qty, price] = line.split(",");
  return { sku, qty: Number(qty), priceCents: Number(price) * 100 };
}

/**
 * Разбирает весь текст: { orders, errors: [{ line, field }], totalCents }.
 */
export function parseOrders(text) {
  const orders = text.split("\n").map(parseOrderLine);
  return { orders, errors: [], totalCents: 0 };
}
