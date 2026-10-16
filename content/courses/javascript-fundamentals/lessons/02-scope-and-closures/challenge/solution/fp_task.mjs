// Функциональные утилиты на замыканиях: compose, curry и маленькое хранилище.

/** compose(f, g, h)(x) === f(g(h(x))). Без аргументов - тождественная функция. */
export function compose(...fns) {
  return (x) => fns.reduceRight((acc, fn) => fn(acc), x);
}

/**
 * Каррирование по fn.length. Аргументы накапливаются между вызовами; за один вызов можно
 * передать несколько. Как только накоплено не меньше fn.length аргументов, вызывается fn.
 */
export function curry(fn) {
  return function curried(...args) {
    if (args.length >= fn.length) return fn(...args);
    return (...more) => curried(...args, ...more);
  };
}

/**
 * Хранилище значения. get() - текущее значение. set(next): next - значение или функция prev => новое.
 * subscribe(listener) - подписка; listener(новое, старое) вызывается только когда значение изменилось
 * (сравнение Object.is). Возвращает функцию отписки.
 */
export function createStore(initial) {
  let value = initial;
  const listeners = new Set();
  return {
    get: () => value,
    set(next) {
      const newValue = typeof next === "function" ? next(value) : next;
      if (Object.is(newValue, value)) return;
      const old = value;
      value = newValue;
      for (const listener of [...listeners]) listener(value, old);
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}
