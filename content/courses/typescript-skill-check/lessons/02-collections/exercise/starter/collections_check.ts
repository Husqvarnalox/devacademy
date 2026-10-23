// Четыре функции о массивах, строках и объектах. Слово export делает объявление видимым для проверки.

export interface CartItem {
  name: string;
  price: number;
  qty?: number; // количество; если свойства нет, берётся 1
}

// Новый массив: числа без повторов по возрастанию. Исходный массив менять нельзя.
export function uniqueSorted(values: readonly number[]): number[] {
  // TODO: проходите по значениям, добавляйте в result те, которых там ещё нет, затем sort с компаратором
  return [];
}

// Самое длинное слово текста (слова разделены любыми пробельными символами).
// Если таких несколько, нужно первое. Для пустого текста (или из одних пробелов) - пустая строка.
export function longestWord(text: string): string {
  // TODO: trim(), split(/\s+/)
  return "";
}

// Сумма price * qty по всем товарам; если qty не указано, количество равно 1.
export function totalPrice(items: readonly CartItem[]): number {
  // TODO: ?? 1 для необязательного количества
  return 0;
}

// Товар с указанным названием без учёта регистра и пробелов по краям. Нет такого - undefined.
export function findItem(items: readonly CartItem[], name: string): CartItem | undefined {
  // TODO: find и сравнение через toLowerCase
  return undefined;
}
