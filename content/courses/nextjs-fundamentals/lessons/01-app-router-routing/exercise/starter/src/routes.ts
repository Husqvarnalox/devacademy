export type RouteParams = Record<string, string | string[]>;

export interface ResolvedRoute {
  pattern: string;
  params: RouteParams;
}

// Имена файлов, которые Next.js считает особыми.
const PAGE_FILE = /^page\.(tsx|ts|jsx|js)$/;
const ROUTE_FILE = /^route\.(ts|js)$/;
const LAYOUT_FILE = /^layout\.(tsx|ts|jsx|js)$/;

// Части пути без пустых строк: "/blog/hello/" -> ["blog", "hello"].
function splitPath(path: string): string[] {
  return path.split("/").filter((part) => part !== "");
}

// "%D0%BF%D1%80" -> "пр"; если строку декодировать нельзя, возвращает её как есть.
function decode(part: string): string {
  try {
    return decodeURIComponent(part);
  } catch {
    return part;
  }
}

export function routesFromFiles(files: string[]): string[] {
  // TODO: оставьте файлы page.* и route.*, уберите группы (name) и приватные папки _name,
  // постройте URL-шаблоны, найдите конфликты и верните отсортированный список без повторов.
  return [];
}

export function matchRoute(pattern: string, pathname: string): RouteParams | null {
  // TODO: сравните сегменты шаблона и пути; соберите параметры [x], [...x], [[...x]].
  return null;
}

export function resolveRoute(patterns: string[], pathname: string): ResolvedRoute | null {
  // TODO: из подходящих шаблонов выберите самый конкретный (matchRoute + сравнение сегментов слева направо).
  return null;
}

export function layoutsFor(files: string[], pageFile: string): string[] {
  // TODO: для каждой папки на пути к странице, начиная с корня, найдите файл layout.* в этой папке.
  return [];
}
