// Учебный источник данных: считает обращения, чтобы было видно, сколько раз «база» действительно читалась.
export type Product = { id: string; name: string; price: number };

const initialProducts: Product[] = [
  { id: "p1", name: "Кофемолка", price: 3500 },
  { id: "p2", name: "Чайник", price: 2490.5 },
  { id: "p3", name: "Турка", price: 990 },
];

// Курсы относительно рубля.
const rates: Record<string, number> = { RUB: 1, USD: 0.011, EUR: 0.01 };

let products: Product[] = initialProducts.map((product) => ({ ...product }));

export const calls = { fetchProducts: 0, fetchProduct: 0, fetchPrices: 0, savePrice: 0, importAll: 0 };

export async function fetchProducts(): Promise<Product[]> {
  calls.fetchProducts += 1;
  return products.map((product) => ({ ...product }));
}

export async function fetchProduct(id: string): Promise<Product | null> {
  calls.fetchProduct += 1;
  const found = products.find((product) => product.id === id);
  return found ? { ...found } : null;
}

// Цены всех товаров в выбранной валюте: { p1: 38.5, ... }.
export async function fetchPrices(currency: string): Promise<Record<string, number>> {
  calls.fetchPrices += 1;
  const rate = rates[currency];
  if (rate === undefined) {
    throw new Error(`Неизвестная валюта: ${currency}`);
  }
  const prices: Record<string, number> = {};
  for (const product of products) {
    prices[product.id] = Math.round(product.price * rate * 100) / 100;
  }
  return prices;
}

// Возвращает false, если товара нет.
export async function savePrice(id: string, price: number): Promise<boolean> {
  calls.savePrice += 1;
  const found = products.find((product) => product.id === id);
  if (!found) return false;
  found.price = price;
  return true;
}

// Имитация загрузки каталога из внешней системы; возвращает число товаров.
export async function importAll(): Promise<number> {
  calls.importAll += 1;
  return products.length;
}

// Нужна тестам: возвращает данные и счётчики к начальному состоянию.
export function resetSource(): void {
  products = initialProducts.map((product) => ({ ...product }));
  for (const key of Object.keys(calls) as (keyof typeof calls)[]) {
    calls[key] = 0;
  }
}
