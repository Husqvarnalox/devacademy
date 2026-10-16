// Замыкания: счётчики, функции по индексам, once и memoize.

/** Независимый счётчик. inc() и dec() возвращают новое значение, value() - текущее. */
export function makeCounter(start = 0) {
  let count = start;
  return {
    inc: () => ++count,
    dec: () => --count,
    value: () => count,
  };
}

/** Массив из n функций; функция с индексом i всегда возвращает i. */
export function makeAdders(n) {
  const fns = [];
  for (let i = 0; i < n; i++) {
    fns.push(() => i);
  }
  return fns;
}

/** Обёртка, вызывающая fn только при первом вызове; позже возвращает тот же результат. */
export function once(fn) {
  let called = false;
  let result;
  return function (...args) {
    if (!called) {
      called = true;
      result = fn.apply(this, args);
    }
    return result;
  };
}

/** Кэш для функции одного аргумента: для каждого значения аргумента fn вызывается один раз. */
export function memoize(fn) {
  const cache = new Map();
  return (arg) => {
    if (!cache.has(arg)) cache.set(arg, fn(arg));
    return cache.get(arg);
  };
}
