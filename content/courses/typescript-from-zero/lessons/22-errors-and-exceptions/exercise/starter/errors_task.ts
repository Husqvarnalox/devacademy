// Исключения: throw, try / catch. Слово export делает функцию видимой для проверки.

// Деление. Если b равно 0, выбросьте Error с сообщением "деление на ноль".
export function safeDivide(a: number, b: number): number {
  // TODO
  return a / b;
}

// Проверка возраста. Возраст должен быть целым числом от 0 до 150 включительно.
// Если число не целое, выбросьте Error("возраст должен быть целым числом").
// Если число вне диапазона, выбросьте Error("возраст вне диапазона 0-150").
// Иначе верните возраст без изменений.
export function checkAge(age: number): number {
  // TODO: Math.floor(age) !== age показывает, что число не целое
  return age;
}

// Результат деления в виде текста: "результат: 2.5".
// Если деление невозможно, верните "ошибка: деление на ноль" (то есть "ошибка: " и сообщение исключения).
export function divideText(a: number, b: number): string {
  // TODO: try { ... } catch (error) { ... }, в catch проверьте error instanceof Error
  return "?";
}

// Из массива возрастов оставить только допустимые (по правилам checkAge).
// Порядок сохраняется. Недопустимые значения пропускаются без остановки программы.
export function validAges(ages: number[]): number[] {
  // TODO: в цикле вызывайте checkAge внутри try / catch
  return ages;
}
