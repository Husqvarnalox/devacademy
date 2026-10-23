export interface CartItem {
  name: string;
  price: number;
  qty?: number;
}

export function uniqueSorted(values: readonly number[]): number[] {
  const result: number[] = [];
  for (const value of values) {
    if (!result.includes(value)) {
      result.push(value);
    }
  }
  return result.sort((a, b) => a - b);
}

export function longestWord(text: string): string {
  const trimmed: string = text.trim();
  if (trimmed === "") {
    return "";
  }
  let best: string = "";
  for (const word of trimmed.split(/\s+/)) {
    if (word.length > best.length) {
      best = word;
    }
  }
  return best;
}

export function totalPrice(items: readonly CartItem[]): number {
  let total: number = 0;
  for (const item of items) {
    total += item.price * (item.qty ?? 1);
  }
  return total;
}

export function findItem(items: readonly CartItem[], name: string): CartItem | undefined {
  const wanted: string = name.trim().toLowerCase();
  return items.find((item) => item.name.toLowerCase() === wanted);
}
