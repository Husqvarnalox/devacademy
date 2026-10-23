// Замыкания и this: счётчик, once и привязка метода.

/**
 * Счётчик с собственным состоянием: inc(), dec(), value(), reset().
 * Методы не должны использовать this.
 */
export function makeCounter(start = 0, step = 1) {
  // TODO: состояние должно быть своим у каждого вызова фабрики
  return {
    inc: () => start + step,
    dec: () => start - step,
    value: () => start,
    reset: () => start,
  };
}

/**
 * Возвращает функцию, вызывающую fn не более одного раза (с this и аргументами первого вызова).
 * Дальше возвращает сохранённый результат.
 */
export function once(fn) {
  // TODO: запомните, был ли вызов, и результат
  return function (...args) {
    return fn.apply(this, args);
  };
}

/**
 * Возвращает функцию, вызывающую obj[name](...args) с this === obj.
 * Метод ищется в момент вызова.
 */
export function bindMethod(obj, name) {
  // TODO: потерянный this и ранний поиск метода - две частые ошибки
  const method = obj[name];
  return (...args) => method(...args);
}
