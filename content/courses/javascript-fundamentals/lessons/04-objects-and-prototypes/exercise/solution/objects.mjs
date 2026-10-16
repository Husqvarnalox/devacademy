// Объекты и прототипы: дескрипторы, цепочка прототипов, класс с приватным полем, копирование.

/**
 * Определяет у obj свойство key со значением value: только для чтения (writable: false),
 * перечисляемое (enumerable: true) и неудаляемое (configurable: false). Возвращает obj.
 */
export function defineReadOnly(obj, key, value) {
  return Object.defineProperty(obj, key, {
    value,
    writable: false,
    enumerable: true,
    configurable: false,
  });
}

/** Сколько объектов в цепочке прототипов obj (сам obj не считается). {} -> 1, [] -> 2, Object.create(null) -> 0. */
export function chainLength(obj) {
  let n = 0;
  for (let p = Object.getPrototypeOf(obj); p !== null; p = Object.getPrototypeOf(p)) n++;
  return n;
}

/**
 * Температура. Значение в градусах Цельсия хранится в приватном поле #celsius.
 * Меньше -273.15 нельзя: RangeError (в конструкторе и в сеттере celsius).
 */
export class Temperature {
  #celsius;

  constructor(celsius) {
    this.celsius = celsius;
  }

  get celsius() {
    return this.#celsius;
  }

  set celsius(value) {
    if (typeof value !== "number" || Number.isNaN(value) || value < -273.15) {
      throw new RangeError(`Недопустимая температура: ${value}`);
    }
    this.#celsius = value;
  }

  /** Градусы Фаренгейта (только чтение). */
  get fahrenheit() {
    return (this.#celsius * 9) / 5 + 32;
  }

  /** Создаёт Temperature из градусов Фаренгейта. */
  static fromFahrenheit(f) {
    return new Temperature(((f - 32) * 5) / 9);
  }
}

/** Независимая глубокая копия состояния (вложенные объекты, массивы, Date, Map, Set). */
export function copyState(state) {
  return structuredClone(state);
}
