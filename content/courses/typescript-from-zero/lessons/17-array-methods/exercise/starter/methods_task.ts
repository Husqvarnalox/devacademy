// Пять функций на методах массивов. Слово export делает функцию видимой для проверки.

// Цены не выше limit, в прежнем порядке. cheapItems([30, 10, 50, 20], 30) = [30, 10, 20]
export function cheapItems(prices: number[], limit: number): number[] {
  // TODO: filter
  return [-1];
}

// Цены с налогом: каждая цена увеличивается на percent процентов и округляется до целого (Math.round).
// withTax([100, 250], 20) = [120, 300]; withTax([99], 10) = [109]  (99 * 1.1 = 108.9)
// Исходный массив менять нельзя.
export function withTax(prices: number[], percent: number): number[] {
  // TODO: map; цена * (100 + percent) / 100, затем Math.round
  return [-1];
}

// Копия массива по убыванию. Исходный массив остаётся как был.
// sortedDescending([3, 10, 2]) = [10, 3, 2]
export function sortedDescending(values: number[]): number[] {
  // TODO: скопируйте массив через [...values] и отсортируйте копию с функцией сравнения
  return [-1];
}

// Названия через запятую и пробел. joinNames(["хлеб", "сыр"]) = "хлеб, сыр"; для пустого массива "" (пустая строка).
export function joinNames(names: string[]): string {
  // TODO: join
  return "?";
}

// Есть ли name среди names (точное совпадение, регистр важен).
export function hasName(names: string[], name: string): boolean {
  // TODO: includes
  return false;
}
