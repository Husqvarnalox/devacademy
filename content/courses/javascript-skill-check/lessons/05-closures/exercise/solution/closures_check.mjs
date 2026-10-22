export function makeCounter(start = 0, step = 1) {
  let current = start;
  return {
    inc() {
      current += step;
      return current;
    },
    dec() {
      current -= step;
      return current;
    },
    value() {
      return current;
    },
    reset() {
      current = start;
      return current;
    },
  };
}

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

export function bindMethod(obj, name) {
  return (...args) => obj[name](...args);
}
