import { reducer, isBusy, summary, type State, type Action } from "../src/state.ts";

export const s1: State = { status: "loading", startedAt: 1, retries: 0 };
// @ts-expect-error у loading обязательны startedAt и retries
export const s2: State = { status: "loading" };
// @ts-expect-error неизвестный статус
export const s3: State = { status: "paused" };
// @ts-expect-error data бывает только у success
export const s4: State = { status: "idle", data: [] };

declare const s: State;
// @ts-expect-error до сужения у State нет общего поля data
export const d0 = s.data;
if (s.status === "success") {
  const d: string[] = s.data;
  void d;
}
if (s.status === "error") {
  const m: string = s.message;
  // @ts-expect-error у error нет loadedAt
  const l = s.loadedAt;
  void [m, l];
}

export const a1: Action = { type: "fetch", at: 5 };
// @ts-expect-error у resolve обязательны data и at
export const a2: Action = { type: "resolve" };
// @ts-expect-error неизвестное действие
reducer(s, { type: "nope" });

export const next: State = reducer(s, a1);
export const busy: boolean = isBusy(s);
export const text: string = summary(s);
