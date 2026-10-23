// Пять функций: три бросают ошибки (throw), две перехватывают их (try/catch).
// Слово export делает функцию видимой для проверки.

/**
 * Частное a / b. Если b равно 0 - бросить new Error("Деление на ноль").
 */
export function divide(a, b) {
}

/**
 * Возвращает age, если это число от 0 до 150 включительно.
 * Не число (typeof) - бросить new Error("Возраст должен быть числом").
 * Меньше 0 или больше 150 - бросить new Error("Возраст вне диапазона 0-150").
 */
export function checkAge(age) {
}

/**
 * Первый элемент массива. Для пустого массива - бросить new Error("Список пуст").
 */
export function firstOrThrow(items) {
}

/**
 * Вызывает divide(a, b) и возвращает результат.
 * Если divide бросила ошибку - вернуть строку "Ошибка: " + сообщение ошибки.
 * safeDivide(6, 3) -> 2, safeDivide(1, 0) -> "Ошибка: Деление на ноль".
 */
export function safeDivide(a, b) {
}

/**
 * true, если checkAge(age) не бросает ошибку, иначе false.
 */
export function isValidAge(age) {
}
