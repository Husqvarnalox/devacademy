// Ошибки и исключения: свои классы, cause, retry и гарантированная очистка.

/** Ошибка проверки значения. Поле field - имя неверного поля. name === "ValidationError". */
export class ValidationError {
  // TODO: класс должен наследовать Error; сообщение и cause передаются в super
  constructor(message, field, options) {
    this.message = message;
    this.field = field;
  }
}

/** Ошибка конфигурации (например, неверный JSON). name === "ConfigError". */
export class ConfigError {
  // TODO: тоже наследник Error
  constructor(message, options) {
    this.message = message;
  }
}

/**
 * Разбирает JSON-конфигурацию и возвращает объект.
 * Неверный JSON - ConfigError("Некорректный JSON") с cause: исходная SyntaxError.
 * Если результат не объект или port не целое число от 1 до 65535 - ValidationError с field === "port".
 */
export function parseConfig(text) {
  return JSON.parse(text);
}

/**
 * Вызывает fn(номерПопытки) до attempts раз (номера с 1) и возвращает результат первого успеха.
 * Если все попытки неудачны - выбрасывает ошибку последней попытки. attempts меньше 1 - RangeError.
 */
export function retry(fn, attempts) {
  return fn(1);
}

/**
 * Вызывает fn() и возвращает результат. cleanup() вызывается ровно один раз в любом случае.
 * Если fn бросила исключение, оно пробрасывается дальше, даже если cleanup тоже бросит.
 * Если fn успешна, а cleanup бросил - пробрасывается ошибка cleanup.
 */
export function withCleanup(fn, cleanup) {
  const result = fn();
  cleanup();
  return result;
}
