// Функциональные утилиты на замыканиях: compose, curry и маленькое хранилище.

/** compose(f, g, h)(x) === f(g(h(x))). Без аргументов - тождественная функция. */
export function compose(...fns) {
  throw new Error("compose не реализована");
}

/**
 * Каррирование по fn.length. Аргументы накапливаются между вызовами; за один вызов можно
 * передать несколько. Как только накоплено не меньше fn.length аргументов, вызывается fn.
 */
export function curry(fn) {
  throw new Error("curry не реализована");
}

/**
 * Хранилище значения. get() - текущее значение. set(next): next - значение или функция prev => новое.
 * subscribe(listener) - подписка; listener(новое, старое) вызывается только когда значение изменилось
 * (сравнение Object.is). Возвращает функцию отписки.
 */
export function createStore(initial) {
  throw new Error("createStore не реализована");
}
