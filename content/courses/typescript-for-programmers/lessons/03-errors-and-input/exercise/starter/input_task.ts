// Разбор заказа из текста. Слово export делает объявление видимым для проверки.

// Ошибка данных: хранит номер строки. Класс уже готов.
export class ParseError extends Error {
  line: number;

  constructor(message: string, line: number) {
    super(message);
    this.name = "ParseError";
    this.line = line;
  }
}

export interface OrderItem {
  name: string;
  quantity: number;
}

export interface LineProblem {
  line: number;
  message: string;
}

// Разбирает строку "название,количество" и возвращает { name, quantity }.
// Бросает new ParseError(сообщение, lineNo) в таком порядке проверок:
//   1. полей не два (после split(","))        - "ожидалось два поля"
//   2. название после trim пусто                - "пустое название"
//   3. количество (после trim) не из одних цифр - "количество должно быть целым числом"
//   4. количество меньше 1 или больше 100       - "количество вне диапазона 1-100"
export function parseLine(line: string, lineNo: number): OrderItem {
  // TODO
  return { name: line, quantity: 0 };
}

// Разбирает многострочный текст (разделители \n и \r\n). Пустые строки и строки из одних пробелов
// пропускаются, но номера строк считаются с 1 по ВСЕМ строкам.
// Верные записи идут в items, ParseError - в problems как { line, message }; остальные ошибки не глотайте.
export function parseOrder(text: string): { items: OrderItem[]; problems: LineProblem[] } {
  // TODO: цикл по строкам, try / catch, проверка error instanceof ParseError
  return { items: [], problems: [] };
}

// Строка с JSON -> массив строк. Если JSON неверный (SyntaxError), это не массив
// или в нём есть не строки, верните null. Пустой массив "[]" - верный результат.
// Результат JSON.parse имеет тип any: присвойте его переменной типа unknown и проверьте.
export function parseStringList(text: string): string[] | null {
  // TODO
  return [];
}

// Текст ошибки из значения, пойманного в catch: у Error - message, у строки - она сама,
// для всего остального "неизвестная ошибка" (в том числе для объекта { message: "..." }, который не Error).
export function errorMessage(error: unknown): string {
  // TODO
  return "";
}
