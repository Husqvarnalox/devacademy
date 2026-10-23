// Структурные типы и необязательные свойства. Слово export делает объявление видимым для проверки.

export interface Person {
  name: string;
  age: number;
  email?: string;
}

// Приветствие "Здравствуйте, <имя>!" для любого объекта, у которого есть строка name.
// Параметр описан структурно: подойдёт и Person, и объект с другими полями.
export function greet(who: { name: string }): string {
  // TODO
  return "";
}

// Имя; если возраст указан - добавьте его в скобках: "Аня (25)". Возраст 0 тоже указан.
export function describe(entity: { name: string; age?: number }): string {
  // TODO: сравнивайте age с undefined, а не проверяйте истинность
  return "";
}

// Имена совершеннолетних (возраст 18 и больше) в исходном порядке.
export function adultNames(people: readonly Person[]): string[] {
  // TODO
  return [];
}

// Самый старший человек; при равном возрасте - тот, что раньше в списке. Для пустого списка - undefined.
export function oldest(people: readonly Person[]): Person | undefined {
  // TODO
  return undefined;
}
