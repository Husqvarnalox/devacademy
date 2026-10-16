// Объекты и прототипы: дескрипторы, цепочка прототипов, класс с приватным полем, копирование.

/**
 * Определяет у obj свойство key со значением value: только для чтения (writable: false),
 * перечисляемое (enumerable: true) и неудаляемое (configurable: false). Возвращает obj.
 */
export function defineReadOnly(obj, key, value) {
  // TODO: обычное присваивание создаёт свойство со всеми флагами true
  obj[key] = value;
  return obj;
}

/** Сколько объектов в цепочке прототипов obj (сам obj не считается). {} -> 1, [] -> 2, Object.create(null) -> 0. */
export function chainLength(obj) {
  // TODO: пройдите по Object.getPrototypeOf до null
  return 0;
}

/**
 * Температура. Значение в градусах Цельсия хранится в приватном поле #celsius.
 * Меньше -273.15 нельзя: RangeError (в конструкторе и в сеттере celsius).
 */
export class Temperature {
  // TODO: заведите приватное поле #celsius, геттер и сеттер celsius, геттер fahrenheit
  constructor(celsius) {
    this.celsius = celsius;
  }

  /** Создаёт Temperature из градусов Фаренгейта: (f - 32) * 5 / 9. */
  static fromFahrenheit(f) {
    throw new Error("fromFahrenheit не реализован");
  }
}

/** Независимая глубокая копия состояния (вложенные объекты, массивы, Date, Map, Set). */
export function copyState(state) {
  // TODO: {...state} копирует только верхний уровень
  return { ...state };
}
