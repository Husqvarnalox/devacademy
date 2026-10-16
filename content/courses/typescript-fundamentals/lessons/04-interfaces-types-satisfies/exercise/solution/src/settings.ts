export type Theme = "light" | "dark";

export interface Settings {
  readonly name: string;
  theme?: Theme;
  readonly limits: Readonly<Record<string, number>>;
}

export const ROUTES = {
  home: "/",
  user: "/users/:id",
  post: "/users/:id/posts/:postId",
} satisfies Record<string, string>;

export type RouteName = keyof typeof ROUTES;

export function buildUrl(name: RouteName, params: Record<string, string | number> = {}): string {
  return ROUTES[name].replace(/:(\w+)/g, (whole, key: string) => {
    const value = params[key];
    return value === undefined ? whole : String(value);
  });
}

export function resolveTheme(s: Settings): Theme {
  return s.theme ?? "light";
}

export function withTheme(s: Settings, theme: Theme): Settings {
  return { ...s, theme };
}

export function limitOf(s: Settings, key: string): number {
  return s.limits[key] ?? 0;
}

export function countBy(words: readonly string[]): Record<string, number> {
  // Map, а не обычный объект: слово "constructor" не должно находить Object.prototype.constructor.
  const counts = new Map<string, number>();
  for (const w of words) {
    counts.set(w, (counts.get(w) ?? 0) + 1);
  }
  return Object.fromEntries(counts);
}
