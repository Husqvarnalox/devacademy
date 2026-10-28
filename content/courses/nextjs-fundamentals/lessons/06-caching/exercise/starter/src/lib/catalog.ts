import { fetchPrices, fetchProduct, fetchProducts } from "./source";
import type { Product } from "./source";

// TODO: каждой функции нужны директива "use cache" (первым оператором тела), теги и срок жизни.

export async function getProducts(): Promise<Product[]> {
  return fetchProducts();
}

export async function getProduct(id: string): Promise<Product | null> {
  return fetchProduct(id);
}

export async function getPrices(currency: string): Promise<Record<string, number>> {
  return fetchPrices(currency);
}
