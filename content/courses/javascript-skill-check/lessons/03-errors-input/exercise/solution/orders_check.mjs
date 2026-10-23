export class ParseError extends Error {
  constructor(message, field) {
    super(message);
    this.name = "ParseError";
    this.field = field;
  }
}

export function parseOrderLine(line) {
  const parts = line.split(",");
  if (parts.length !== 3) throw new ParseError("expected 3 fields", "fields");
  const sku = parts[0].trim();
  const qty = parts[1].trim();
  const price = parts[2].trim();
  if (!/^[A-Z]{2}-\d{3}$/.test(sku)) throw new ParseError("bad sku", "sku");
  if (!/^[1-9]\d*$/.test(qty)) throw new ParseError("bad qty", "qty");
  if (!/^\d+(\.\d{1,2})?$/.test(price)) throw new ParseError("bad price", "price");
  return { sku, qty: Number(qty), priceCents: Math.round(Number(price) * 100) };
}

export function parseOrders(text) {
  if (typeof text !== "string") throw new TypeError("text must be a string");
  const orders = [];
  const errors = [];
  let totalCents = 0;
  const lines = text.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].trim() === "") continue;
    try {
      const order = parseOrderLine(lines[i]);
      orders.push(order);
      totalCents += order.qty * order.priceCents;
    } catch (e) {
      if (!(e instanceof ParseError)) throw e;
      errors.push({ line: i + 1, field: e.field });
    }
  }
  return { orders, errors, totalCents };
}
