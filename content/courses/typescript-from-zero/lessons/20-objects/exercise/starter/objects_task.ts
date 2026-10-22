// Объекты и интерфейс. Слово export делает функцию видимой для проверки.

export interface Product {
  name: string;
  price: number;
  quantity: number;
}

// Создать объект товара из трёх значений.
// makeProduct("хлеб", 40, 2) = { name: "хлеб", price: 40, quantity: 2 }
export function makeProduct(name: string, price: number, quantity: number): Product {
  // TODO: верните объект с тремя свойствами
  return { name: "", price: 0, quantity: 0 };
}

// Стоимость позиции: цена * количество.
export function lineTotal(p: Product): number {
  // TODO
  return -1;
}

// Сумма стоимостей всех позиций корзины. Для пустой корзины 0.
export function cartTotal(items: Product[]): number {
  // TODO: пройдите по items и используйте lineTotal
  return -1;
}

// Название товара с наибольшей ценой за единицу (price, без учёта количества).
// Если цены равны, берите того, кто раньше в массиве. Для пустого массива результат "" (пустая строка).
export function mostExpensive(items: Product[]): string {
  // TODO: запоминайте лучший товар; пустой массив обработайте отдельно
  return "?";
}
