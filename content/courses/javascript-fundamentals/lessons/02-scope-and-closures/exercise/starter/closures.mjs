// Замыкания: счётчики, функции по индексам, once и memoize.

let shared = 0; // TODO: состояние счётчика должно быть своим у каждого вызова makeCounter

/** Независимый счётчик. inc() и dec() возвращают новое значение, value() - текущее. */
export function makeCounter(start = 0) {
  shared = start;
  return {
    inc: () => ++shared,
    dec: () => --shared,
    value: () => shared,
  };
}

/** Массив из n функций; функция с индексом i всегда возвращает i. */
export function makeAdders(n) {
  const fns = [];
  // TODO: все функции видят одну и ту же переменную i. Почему и как это исправить?
  for (var i = 0; i < n; i++) {
    fns.push(() => i);
  }
  return fns;
}

/** Обёртка, вызывающая fn только при первом вызове; позже возвращает тот же результат. */
export function once(fn) {
  return (...args) => fn(...args); // TODO: запомните, был ли вызов, и сам результат
}

/** Кэш для функции одного аргумента: для каждого значения аргумента fn вызывается один раз. */
export function memoize(fn) {
  return (arg) => fn(arg); // TODO: хранилище результатов живёт в замыкании
}
