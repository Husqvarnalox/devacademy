// Ошибки и исключения: свои классы, cause, retry и гарантированная очистка.

/** Ошибка проверки значения. Поле field - имя неверного поля. name === "ValidationError". */
export class ValidationError extends Error {
  constructor(message, field, options) {
    super(message, options);
    this.name = "ValidationError";
    this.field = field;
  }
}

/** Ошибка конфигурации (например, неверный JSON). name === "ConfigError". */
export class ConfigError extends Error {
  constructor(message, options) {
    super(message, options);
    this.name = "ConfigError";
  }
}

/**
 * Разбирает JSON-конфигурацию и возвращает объект.
 * Неверный JSON - ConfigError("Некорректный JSON") с cause: исходная SyntaxError.
 * Если результат не объект или port не целое число от 1 до 65535 - ValidationError с field === "port".
 */
export function parseConfig(text) {
  let config;
  try {
    config = JSON.parse(text);
  } catch (error) {
    throw new ConfigError("Некорректный JSON", { cause: error });
  }
  const port = config !== null && typeof config === "object" ? config.port : undefined;
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new ValidationError("port должен быть целым числом от 1 до 65535", "port");
  }
  return config;
}

/**
 * Вызывает fn(номерПопытки) до attempts раз (номера с 1) и возвращает результат первого успеха.
 * Если все попытки неудачны - выбрасывает ошибку последней попытки. attempts меньше 1 - RangeError.
 */
export function retry(fn, attempts) {
  if (!Number.isInteger(attempts) || attempts < 1) {
    throw new RangeError("attempts должно быть целым числом не меньше 1");
  }
  let lastError;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return fn(attempt);
    } catch (error) {
      lastError = error;
    }
  }
  throw lastError;
}

/**
 * Вызывает fn() и возвращает результат. cleanup() вызывается ровно один раз в любом случае.
 * Если fn бросила исключение, оно пробрасывается дальше, даже если cleanup тоже бросит.
 * Если fn успешна, а cleanup бросил - пробрасывается ошибка cleanup.
 */
export function withCleanup(fn, cleanup) {
  let result;
  try {
    result = fn();
  } catch (error) {
    try {
      cleanup();
    } catch {
      // ошибка очистки не должна скрыть исходную
    }
    throw error;
  }
  cleanup();
  return result;
}
