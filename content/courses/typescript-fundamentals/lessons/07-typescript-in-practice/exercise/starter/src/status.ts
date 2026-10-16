// Файлы этого упражнения должны запускаться Node напрямую (удаление типов), поэтому enum и свойства-параметры недопустимы.

// TODO: замените enum на объект as const и одноимённый тип-объединение значений
export enum Status {
  Active = "active",
  Blocked = "blocked",
  Pending = "pending",
}

export function isStatus(v: unknown): v is Status {
  // TODO: v - одно из значений Status (строка "active", "blocked" или "pending")
  return false;
}

export function label(s: Status): string {
  // TODO: "Активен", "Заблокирован", "Ожидает"; switch с проверкой исчерпываемости
  return "";
}

// TODO: уберите свойство-параметр (public value), объявите поле и присвойте его в конструкторе
export class Counter {
  constructor(public value: number = 0) {}

  increment(): Counter {
    return new Counter(this.value + 1);
  }
}
