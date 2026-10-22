// Объединения и сужение типов. Слово export делает объявление видимым для проверки.

export type Shape =
  | { kind: "circle"; radius: number }
  | { kind: "rect"; width: number; height: number }
  | { kind: "square"; side: number };

export type Payment =
  | { method: "cash"; amount: number }
  | { method: "card"; amount: number; last4: string }
  | { method: "refund"; amount: number; reason?: string };

// Число дополняется нулями слева до 5 знаков (42 -> "00042").
// Строка: обрезать пробелы по краям и перевести в верхний регистр.
export function normalizeId(id: string | number): string {
  // TODO
  return "";
}

// Площадь фигуры. Для круга округлите до двух знаков после запятой (Math.PI * r * r).
export function area(shape: Shape): number {
  // TODO: switch по shape.kind
  return -1;
}

// "Наличные: 100", "Карта *1234: 100", "Возврат: 100" или, если указана причина, "Возврат: 100 (брак)".
export function describePayment(payment: Payment): string {
  // TODO
  return "";
}

// Первая строка в списке (число и null пропускаются). Если строк нет - undefined.
export function firstString(items: readonly (string | number | null)[]): string | undefined {
  // TODO
  return "";
}
