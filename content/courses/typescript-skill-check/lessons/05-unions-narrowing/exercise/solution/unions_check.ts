export type Shape =
  | { kind: "circle"; radius: number }
  | { kind: "rect"; width: number; height: number }
  | { kind: "square"; side: number };

export type Payment =
  | { method: "cash"; amount: number }
  | { method: "card"; amount: number; last4: string }
  | { method: "refund"; amount: number; reason?: string };

export function normalizeId(id: string | number): string {
  if (typeof id === "number") {
    return String(id).padStart(5, "0");
  }
  return id.trim().toUpperCase();
}

export function area(shape: Shape): number {
  switch (shape.kind) {
    case "circle":
      return Math.round(Math.PI * shape.radius * shape.radius * 100) / 100;
    case "rect":
      return shape.width * shape.height;
    case "square":
      return shape.side * shape.side;
  }
}

export function describePayment(payment: Payment): string {
  switch (payment.method) {
    case "cash":
      return "Наличные: " + payment.amount;
    case "card":
      return "Карта *" + payment.last4 + ": " + payment.amount;
    case "refund":
      if (payment.reason === undefined) {
        return "Возврат: " + payment.amount;
      }
      return "Возврат: " + payment.amount + " (" + payment.reason + ")";
  }
}

export function firstString(items: readonly (string | number | null)[]): string | undefined {
  for (const item of items) {
    if (typeof item === "string") {
      return item;
    }
  }
  return undefined;
}
