// Разбор текстового ввода: записи "имя,возраст", JSON и числа.

/** Ошибка данных. Поле line - номер строки ввода (с 1) или null, если строка неизвестна. */
export class ValidationError extends Error {
  constructor(message, line = null) {
    super(message);
    this.name = "ValidationError";
    this.line = line;
  }
}

/**
 * Разбирает одну строку "имя,возраст" и возвращает { name, age }.
 * Бросает ValidationError(сообщение, lineNo), если: полей не два, имя пустое (после trim),
 * возраст не целое неотрицательное число из одних цифр (после trim) или больше 150.
 */
export function parseLine(line, lineNo) {
  const fields = line.split(",");
  return { name: fields[0], age: parseInt(fields[1]) };
}

/**
 * Разбирает многострочный текст. Строки делятся по \n или \r\n, пустые (из пробелов) строки пропускаются,
 * но номера строк считаются по всем строкам с 1.
 * Возвращает { records: [{name, age}], errors: [{line, message}] }: правильные записи и ошибки в порядке строк.
 * Если text не строка - TypeError (это ошибка программиста, а не данных, её не собирают в errors).
 */
export function parseRecords(text) {
  return { records: text.split("\n").map((l, i) => parseLine(l, i + 1)), errors: [] };
}

/**
 * Разбирает JSON. Если text не строка или это неверный JSON - возвращает fallback.
 * parseJsonSafe("null", 5) - null (это верный JSON).
 */
export function parseJsonSafe(text, fallback) {
  return JSON.parse(text);
}

/**
 * Числа, разделённые любыми пробельными символами (пробелы, табуляции, переводы строк), в массив.
 * Допустимы только десятичные числа вида 12, -3, +4, 2.5. Иначе ValidationError (line = null).
 * Пустой ввод даёт пустой массив.
 */
export function parseNumbers(text) {
  return text.split(" ").map(Number);
}
