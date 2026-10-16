// Настройки приложения и таблица маршрутов.
// Задача: сделать типы точными (readonly, необязательное поле, satisfies) и реализовать функции.

export type Theme = "light" | "dark";

export interface Settings {
  // TODO: name и limits после создания менять нельзя (readonly), theme необязательна
  name: string;
  theme: Theme;
  limits: Record<string, number>;
}

// TODO: аннотация Record<string, string> стирает имена ключей. Замените её на satisfies,
// чтобы RouteName стал объединением "home" | "user" | "post", а опечатки в значениях по-прежнему ловились.
export const ROUTES: Record<string, string> = {
  home: "/",
  user: "/users/:id",
  post: "/users/:id/posts/:postId",
};

export type RouteName = keyof typeof ROUTES;

export function buildUrl(name: RouteName, params: Record<string, string | number> = {}): string {
  // TODO: взять шаблон из ROUTES и заменить каждый :ключ значением из params (неизвестные :ключи оставить как есть)
  return "";
}

export function resolveTheme(s: Settings): Theme {
  // TODO: theme настройки или "light", если она не задана
  return "light";
}

export function withTheme(s: Settings, theme: Theme): Settings {
  // TODO: вернуть новый объект с другой темой; s менять нельзя
  return s;
}

export function limitOf(s: Settings, key: string): number {
  // TODO: лимит по ключу или 0, если ключа нет
  return 0;
}

export function countBy(words: readonly string[]): Record<string, number> {
  // TODO: сколько раз встретилось каждое слово. Помните: words[i] и counts[w] могут быть undefined
  return {};
}
