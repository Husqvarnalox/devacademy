// Четыре функции о массивах, словарях и необязательных свойствах.
// Слово export делает функцию (и тип) видимой для проверки.

export interface Expense {
  category: string;
  amount: number;
  note?: string; // необязательное свойство: может отсутствовать
}

export interface Product {
  name: string;
  price?: number; // у товара без цены свойства price нет
}

// Новый массив чисел по возрастанию. Исходный массив менять нельзя (в типе он readonly).
export function sortedNumbers(values: readonly number[]): number[] {
  // TODO: скопируйте массив и сортируйте копию с компаратором (a, b) => a - b
  return [...values];
}

// Map "категория -> сумма расходов". Ключи в порядке первого появления категории.
// Названия приходят от пользователя: "constructor" и "__proto__" - обычные категории.
export function totalsByCategory(expenses: readonly Expense[]): Map<string, number> {
  // TODO: new Map<string, number>(), get/set, ?? 0
  return new Map<string, number>();
}

// Самый дешёвый товар среди тех, у которых указана цена (цена 0 - тоже цена).
// При равных ценах выигрывает тот, что раньше в списке. Нет ни одного товара с ценой - undefined.
export function cheapest(products: readonly Product[]): Product | undefined {
  // TODO: проверяйте product.price === undefined, а не "if (product.price)"
  return undefined;
}

// true, если массивы одной длины и элементы на одинаковых местах равны через ===.
// NaN считается равным NaN. Вложенные массивы и объекты сравниваются по ссылке.
export function sameItems(a: readonly unknown[], b: readonly unknown[]): boolean {
  // TODO
  return a === b;
}
