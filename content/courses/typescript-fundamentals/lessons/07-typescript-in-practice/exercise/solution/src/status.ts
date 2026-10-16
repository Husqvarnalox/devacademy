export const Status = {
  Active: "active",
  Blocked: "blocked",
  Pending: "pending",
} as const;

export type Status = (typeof Status)[keyof typeof Status];

export function isStatus(v: unknown): v is Status {
  return Object.values(Status).some((s) => s === v);
}

function assertNever(x: never): never {
  throw new Error(`Необработанный статус: ${String(x)}`);
}

export function label(s: Status): string {
  switch (s) {
    case Status.Active:
      return "Активен";
    case Status.Blocked:
      return "Заблокирован";
    case Status.Pending:
      return "Ожидает";
    default:
      return assertNever(s);
  }
}

export class Counter {
  value: number;

  constructor(value: number = 0) {
    this.value = value;
  }

  increment(): Counter {
    return new Counter(this.value + 1);
  }
}
