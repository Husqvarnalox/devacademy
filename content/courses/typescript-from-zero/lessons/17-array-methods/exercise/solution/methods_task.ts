export function cheapItems(prices: number[], limit: number): number[] {
  return prices.filter((p) => p <= limit);
}

export function withTax(prices: number[], percent: number): number[] {
  return prices.map((p) => Math.round((p * (100 + percent)) / 100));
}

export function sortedDescending(values: number[]): number[] {
  return [...values].sort((a, b) => b - a);
}

export function joinNames(names: string[]): string {
  return names.join(", ");
}

export function hasName(names: string[], name: string): boolean {
  return names.includes(name);
}
