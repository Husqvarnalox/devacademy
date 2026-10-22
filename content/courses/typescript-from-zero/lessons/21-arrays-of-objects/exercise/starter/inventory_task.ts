// Массив объектов и необязательное свойство. Слово export делает функцию видимой для проверки.

export interface Product {
  name: string;
  price: number;
  category: string;
  discount?: number; // скидка в рублях; может отсутствовать
}

// Найти товар по названию. Если такого нет, вернуть undefined.
export function findByName(items: Product[], name: string): Product | undefined {
  // TODO: пройдите по items и верните первый товар с нужным name
  return { name: "?", price: 0, category: "?" };
}

// Цена с учётом скидки: price минус discount. Если скидки нет, цена не меняется.
export function finalPrice(p: Product): number {
  // TODO: используйте p.discount ?? 0
  return -1;
}

// Сумма цен со скидкой всех товаров категории category. Для категории без товаров 0.
export function totalInCategory(items: Product[], category: string): number {
  // TODO: цикл, условие на category, накопитель, finalPrice
  return -1;
}

// Названия товаров, упорядоченные по цене со скидкой по возрастанию.
// При равных ценах порядок остаётся исходным. Исходный массив менять нельзя.
export function namesByPrice(items: Product[]): string[] {
  // TODO: сортируйте КОПИЮ массива, затем возьмите названия
  return ["?"];
}
