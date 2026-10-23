export interface Product {
  name: string;
  price: number;
  category: string;
  discount?: number;
}

export function findByName(items: Product[], name: string): Product | undefined {
  for (const item of items) {
    if (item.name === name) {
      return item;
    }
  }
  return undefined;
}

export function finalPrice(p: Product): number {
  return p.price - (p.discount ?? 0);
}

export function totalInCategory(items: Product[], category: string): number {
  let total: number = 0;
  for (const item of items) {
    if (item.category === category) {
      total = total + finalPrice(item);
    }
  }
  return total;
}

export function namesByPrice(items: Product[]): string[] {
  const sorted = [...items].sort((a, b) => finalPrice(a) - finalPrice(b));
  return sorted.map((p) => p.name);
}
