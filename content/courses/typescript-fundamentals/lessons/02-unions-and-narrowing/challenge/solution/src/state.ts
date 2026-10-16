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
  switch (action.type) {
    case "fetch":
      if (state.status === "loading") return state;
      return {
        status: "loading",
        startedAt: action.at,
        retries: state.status === "error" ? state.retries : 0,
      };
    case "resolve":
      return state.status === "loading" ? { status: "success", data: [...action.data], loadedAt: action.at } : state;
    case "reject":
      return state.status === "loading"
        ? { status: "error", message: action.message, retries: state.retries + 1 }
        : state;
    case "reset":
      return { status: "idle" };
    default:
      return assertNever(action);
  }
}

export function isBusy(state: State): boolean {
  return state.status === "loading";
}

export function summary(state: State): string {
  switch (state.status) {
    case "idle":
      return "ожидание";
    case "loading":
      return `загрузка с ${state.startedAt}`;
    case "success":
      return `готово: ${state.data.length} эл.`;
    case "error":
      return `ошибка (попытка ${state.retries}): ${state.message}`;
    default:
      return assertNever(state);
  }
}
