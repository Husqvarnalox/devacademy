export function findFirstNegative(values: number[]): number | undefined {
  for (const v of values) {
    if (v < 0) {
      return v;
    }
  }
  return undefined;
}

export function describePrice(price: number | undefined): string {
  if (price === undefined) {
    return "нет цены";
  }
  return `${price} руб.`;
}

export function itemOrDefault(values: number[], index: number, fallback: number): number {
  return values.at(index) ?? fallback;
}
