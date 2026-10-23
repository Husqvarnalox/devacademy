// Данные снаружи: unknown, предикаты и ручная проверка формы. Слово export делает объявление видимым для проверки.

export interface User {
  name: string;
  age: number;
}

// true, если value - массив, и все его элементы строки (пустой массив подходит).
export function isStringArray(value: unknown): value is string[] {
  // TODO
  return false;
}

// Разбирает JSON вида {"name": "Аня", "age": 25}.
// Вернуть User, если: JSON корректен, это объект (не массив и не null),
// name - непустая строка (после trim; сохранить имя уже без пробелов), age - целое число 0 и больше.
// Иначе вернуть undefined. Исключение наружу выбрасывать нельзя.
export function parseUser(text: string): User | undefined {
  // TODO
  return { name: "", age: -1 };
}

// Описание значения: "строка: abc", "число: 5", "логическое: true", "null", "undefined",
// "массив из 3" (для массива), "объект" (для остального).
export function describeUnknown(value: unknown): string {
  // TODO
  return "";
}

// Сумма всех конечных чисел списка; остальные значения (в том числе NaN и Infinity) пропускаются.
export function sumNumbers(values: readonly unknown[]): number {
  // TODO
  return -1;
}
