import { cacheLife, cacheTag } from "next/cache";
import { fetchPrices, fetchProduct, fetchProducts } from "./source";
import type { Product } from "./source";

export async function getProducts(): Promise<Product[]> {
  "use cache";
  cacheTag("products");
  cacheLife("hours");
  return fetchProducts();
}

export async function getProduct(id: string): Promise<Product | null> {
  "use cache";
  cacheTag("products", "product-" + id);
  cacheLife("hours");
  return fetchProduct(id);
}

// Валюта приходит аргументом: она входит в ключ кэша. Читать cookie внутри кэшируемой функции нельзя -
// значение берут снаружи (например, на странице) и передают сюда.
export async function getPrices(currency: string): Promise<Record<string, number>> {
  "use cache";
  cacheTag("products");
  cacheLife("hours");
  return fetchPrices(currency);
}
