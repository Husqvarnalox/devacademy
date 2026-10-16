// Состояние загрузки данных и редьюсер. Типы заданы, реализуйте три функции.

export type State =
  | { status: "idle" }
  | { status: "loading"; startedAt: number; retries: number }
  | { status: "success"; data: string[]; loadedAt: number }
  | { status: "error"; message: string; retries: number };

export type Action =
  | { type: "fetch"; at: number }
  | { type: "resolve"; data: string[]; at: number }
  | { type: "reject"; message: string }
  | { type: "reset" };

export function assertNever(x: never): never {
  throw new Error(`Необработанный вариант: ${JSON.stringify(x)}`);
}

export function reducer(state: State, action: Action): State {
  // TODO: правила в условии задачи. Разбирайте action через switch (action.type) с default: return assertNever(action)
  return state;
}

export function isBusy(state: State): boolean {
  // TODO: true только для загрузки
  return false;
}

export function summary(state: State): string {
  // TODO: switch (state.status) с default: return assertNever(state)
  return "";
}
