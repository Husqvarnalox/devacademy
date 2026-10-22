export interface Expense {
  category: string;
  amount: number;
  note?: string;
}

export interface Product {
  name: string;
  price?: number;
}

export function sortedNumbers(values: readonly number[]): number[] {
  return [...values].sort((a, b) => a - b);
}

export function totalsByCategory(expenses: readonly Expense[]): Map<string, number> {
  const totals = new Map<string, number>();
  for (const expense of expenses) {
    totals.set(expense.category, (totals.get(expense.category) ?? 0) + expense.amount);
  }
  return totals;
}

export function cheapest(products: readonly Product[]): Product | undefined {
  let best: Product | undefined = undefined;
  for (const product of products) {
    if (product.price === undefined) {
      continue;
    }
    if (best === undefined || best.price === undefined || product.price < best.price) {
      best = product;
    }
  }
  return best;
}

export function sameItems(a: readonly unknown[], b: readonly unknown[]): boolean {
  if (a.length !== b.length) {
    return false;
  }
  for (let i = 0; i < a.length; i++) {
    const same = a[i] === b[i] || (Number.isNaN(a[i]) && Number.isNaN(b[i]));
    if (!same) {
      return false;
    }
  }
  return true;
}
