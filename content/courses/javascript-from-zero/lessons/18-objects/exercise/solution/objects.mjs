// Пять функций для работы с объектами. Слово export делает функцию видимой для проверки.

/**
 * Объект товара: свойства name, price и inStock (всегда true).
 * makeProduct("Чай", 120) -> { name: "Чай", price: 120, inStock: true }.
 */
export function makeProduct(name, price) {
  return { name: name, price: price, inStock: true };
}

/**
 * Значение свойства с именем key; если свойства нет - undefined.
 * getField({ a: 1 }, "a") -> 1.
 * Идея: имя лежит в переменной, поэтому нужны квадратные скобки.
 */
export function getField(obj, key) {
  return obj[key];
}

/**
 * true, если свойство с именем key есть в объекте (даже если его значение undefined).
 * hasField({ a: 1 }, "a") -> true, hasField({ a: 1 }, "b") -> false.
 */
export function hasField(obj, key) {
  return key in obj;
}

/**
 * Записывает свойство key со значением value в тот же объект и возвращает его.
 * addField({ a: 1 }, "b", 2) -> { a: 1, b: 2 }.
 */
export function addField(obj, key, value) {
  obj[key] = value;
  return obj;
}

/**
 * Массив имён свойств в порядке добавления.
 * fieldNames({ x: 1, y: 2 }) -> ["x", "y"].
 */
export function fieldNames(obj) {
  return Object.keys(obj);
}
