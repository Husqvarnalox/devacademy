// this и функции. Файл - ES-модуль, поэтому код в нём всегда выполняется в строгом режиме.

/**
 * Возвращает функцию, которая вызывает obj[name] с this === obj и всеми переданными аргументами.
 * Метод ищется в момент вызова, а не в момент связывания.
 */
export function bindMethod(obj, name) {
  return (...args) => obj[name](...args);
}

/**
 * partial(fn, a)(b, c) вызывает fn(a, b, c). Значение this вызова возвращённой функции
 * передаётся в fn.
 */
export function partial(fn, ...preset) {
  return function (...args) {
    return fn.apply(this, [...preset, ...args]);
  };
}

export class Logger {
  constructor(prefix) {
    this.prefix = prefix;
  }

  /** Возвращает строку "<prefix>: <msg>". */
  log(msg) {
    return `${this.prefix}: ${msg}`;
  }

  /** Возвращает массив результатов this.log для каждого сообщения. */
  logAll(msgs) {
    return msgs.map((msg) => this.log(msg));
  }
}
