export interface Product {
  name: string;
  price: number;
  quantity: number;
}

export function makeProduct(name: string, price: number, quantity: number): Product {
  return { name: name, price: price, quantity: quantity };
}

export function lineTotal(p: Product): number {
  return p.price * p.quantity;
}

export function cartTotal(items: Product[]): number {
  let total: number = 0;
  for (const item of items) {
    total = total + lineTotal(item);
  }
  return total;
}

export function mostExpensive(items: Product[]): string {
  if (items.length === 0) {
    return "";
  }
  let best: Product = items[0];
  for (const item of items) {
    if (item.price > best.price) {
      best = item;
    }
  }
  return best.name;
}
