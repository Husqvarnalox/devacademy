// this и функции. Файл - ES-модуль, поэтому код в нём всегда выполняется в строгом режиме.

/**
 * Возвращает функцию, которая вызывает obj[name] с this === obj и всеми переданными аргументами.
 * Метод ищется в момент вызова, а не в момент связывания.
 */
export function bindMethod(obj, name) {
  // TODO: obj[name] вызывается как «голая» функция, this теряется
  const method = obj[name];
  return (...args) => method(...args);
}

/**
 * partial(fn, a)(b, c) вызывает fn(a, b, c). Значение this вызова возвращённой функции
 * передаётся в fn.
 */
export function partial(fn, ...preset) {
  // TODO: стрелочная функция не имеет своего this
  return (...args) => fn(...preset, ...args);
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
    // TODO: внутри обычной function значение this другое (в строгом режиме - undefined)
    return msgs.map(function (msg) {
      return this.log(msg);
    });
  }
}
