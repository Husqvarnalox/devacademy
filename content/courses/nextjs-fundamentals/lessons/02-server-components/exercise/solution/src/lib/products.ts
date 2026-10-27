// Источник данных каталога. Файл не меняйте: тесты рассчитывают на эти товары.
// В настоящем приложении здесь был бы запрос к базе данных или внешнему API.

export interface Product {
  id: string;
  name: string;
  price: number; // в рублях
  description: string;
}

const PRODUCTS: readonly Product[] = [
  { id: "1", name: "Чайник", price: 1490, description: "Стальной чайник на 1,7 литра." },
  { id: "2", name: "Блокнот", price: 350, description: "Блокнот в клетку, 96 листов." },
  { id: "3", name: "Лампа настольная", price: 2890, description: "Светодиодная лампа с регулировкой яркости." },
  { id: "4", name: "Кружка", price: 420, description: "Керамическая кружка на 300 мл." },
];

// Асинхронные функции повторяют настоящий доступ к данным: ответ приходит позже.
export async function getProducts(): Promise<Product[]> {
  return PRODUCTS.map((product) => ({ ...product }));
}

export async function getProduct(id: string): Promise<Product | undefined> {
  const found = PRODUCTS.find((product) => product.id === id);
  return found ? { ...found } : undefined;
}
