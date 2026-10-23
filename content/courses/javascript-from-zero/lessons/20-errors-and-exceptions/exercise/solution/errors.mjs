// Пять функций: три бросают ошибки (throw), две перехватывают их (try/catch).
// Слово export делает функцию видимой для проверки.

/**
 * Частное a / b. Если b равно 0 - бросить new Error("Деление на ноль").
 */
export function divide(a, b) {
  if (b === 0) {
    throw new Error("Деление на ноль");
  }
  return a / b;
}

/**
 * Возвращает age, если это число от 0 до 150 включительно.
 * Не число (typeof) - бросить new Error("Возраст должен быть числом").
 * Меньше 0 или больше 150 - бросить new Error("Возраст вне диапазона 0-150").
 */
export function checkAge(age) {
  if (typeof age !== "number") {
    throw new Error("Возраст должен быть числом");
  }
  if (age < 0 || age > 150) {
    throw new Error("Возраст вне диапазона 0-150");
  }
  return age;
}

/**
 * Первый элемент массива. Для пустого массива - бросить new Error("Список пуст").
 */
export function firstOrThrow(items) {
  if (items.length === 0) {
    throw new Error("Список пуст");
  }
  return items[0];
}

/**
 * Вызывает divide(a, b) и возвращает результат.
 * Если divide бросила ошибку - вернуть строку "Ошибка: " + сообщение ошибки.
 * safeDivide(6, 3) -> 2, safeDivide(1, 0) -> "Ошибка: Деление на ноль".
 */
export function safeDivide(a, b) {
  try {
    return divide(a, b);
  } catch (e) {
    return "Ошибка: " + e.message;
  }
}

/**
 * true, если checkAge(age) не бросает ошибку, иначе false.
 */
export function isValidAge(age) {
  try {
    checkAge(age);
    return true;
  } catch (e) {
    return false;
  }
}
