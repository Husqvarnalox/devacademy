export type RouteParams = Record<string, string | string[]>;

export interface ResolvedRoute {
  pattern: string;
  params: RouteParams;
}

const PAGE_FILE = /^page\.(tsx|ts|jsx|js)$/;
const ROUTE_FILE = /^route\.(ts|js)$/;
const LAYOUT_FILE = /^layout\.(tsx|ts|jsx|js)$/;

// Части пути без пустых строк: "/blog/hello/" -> ["blog", "hello"].
function splitPath(path: string): string[] {
  return path.split("/").filter((part) => part !== "");
}

function decode(part: string): string {
  try {
    return decodeURIComponent(part);
  } catch {
    return part;
  }
}

function isGroup(folder: string): boolean {
  return folder.startsWith("(") && folder.endsWith(")");
}

// 0 - статический, 1 - [x], 2 - [...x], 3 - [[...x]]
function kindOf(segment: string): number {
  if (/^\[\[\.\.\..+\]\]$/.test(segment)) return 3;
  if (/^\[\.\.\..+\]$/.test(segment)) return 2;
  if (/^\[.+\]$/.test(segment)) return 1;
  return 0;
}

export function routesFromFiles(files: string[]): string[] {
  const owners = new Map<string, string>();
  for (const file of new Set(files)) {
    const parts = file.split("/");
    const name = parts[parts.length - 1];
    if (!PAGE_FILE.test(name) && !ROUTE_FILE.test(name)) continue;
    const folders = parts.slice(0, -1);
    if (folders.some((folder) => folder.startsWith("_"))) continue;
    const url = "/" + folders.filter((folder) => !isGroup(folder)).join("/");
    const other = owners.get(url);
    if (other !== undefined) {
      throw new Error(`Конфликт маршрута ${url}: ${other} и ${file}`);
    }
    owners.set(url, file);
  }
  return [...owners.keys()].sort();
}

export function matchRoute(pattern: string, pathname: string): RouteParams | null {
  const segments = splitPath(pattern);
  const parts = splitPath(pathname).map(decode);
  const params: RouteParams = {};
  for (let i = 0; i < segments.length; i++) {
    const segment = segments[i];
    const kind = kindOf(segment);
    if (kind === 3) {
      const rest = parts.slice(i);
      if (rest.length > 0) params[segment.slice(5, -2)] = rest;
      return params;
    }
    if (kind === 2) {
      const rest = parts.slice(i);
      if (rest.length === 0) return null;
      params[segment.slice(4, -1)] = rest;
      return params;
    }
    if (i >= parts.length) return null;
    if (kind === 1) {
      params[segment.slice(1, -1)] = parts[i];
    } else if (decode(segment) !== parts[i]) {
      return null;
    }
  }
  return parts.length === segments.length ? params : null;
}

// Меньше число - конкретнее шаблон: сегменты сравниваются слева направо.
function compareSpecificity(a: string, b: string): number {
  const left = splitPath(a);
  const right = splitPath(b);
  const common = Math.min(left.length, right.length);
  for (let i = 0; i < common; i++) {
    const diff = kindOf(left[i]) - kindOf(right[i]);
    if (diff !== 0) return diff;
  }
  return left.length - right.length;
}

export function resolveRoute(patterns: string[], pathname: string): ResolvedRoute | null {
  let best: ResolvedRoute | null = null;
  for (const pattern of patterns) {
    const params = matchRoute(pattern, pathname);
    if (params === null) continue;
    if (best === null || compareSpecificity(pattern, best.pattern) < 0) {
      best = { pattern, params };
    }
  }
  return best;
}

export function layoutsFor(files: string[], pageFile: string): string[] {
  const folders = pageFile.split("/").slice(0, -1);
  const result: string[] = [];
  for (let depth = 0; depth <= folders.length; depth++) {
    const folder = folders.slice(0, depth).join("/");
    const layout = files.find((file) => {
      const slash = file.lastIndexOf("/");
      const dir = slash === -1 ? "" : file.slice(0, slash);
      return dir === folder && LAYOUT_FILE.test(file.slice(slash + 1));
    });
    if (layout !== undefined) result.push(layout);
  }
  return result;
}
