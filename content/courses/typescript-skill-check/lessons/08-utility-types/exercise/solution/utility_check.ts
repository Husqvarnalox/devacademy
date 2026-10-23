export interface User {
  id: number;
  name: string;
  age: number;
  email?: string;
}

export function applyPatch(user: Readonly<User>, patch: Partial<User>): User {
  const result: User = { ...user };
  if (patch.id !== undefined) {
    result.id = patch.id;
  }
  if (patch.name !== undefined) {
    result.name = patch.name;
  }
  if (patch.age !== undefined) {
    result.age = patch.age;
  }
  if (patch.email !== undefined) {
    result.email = patch.email;
  }
  return result;
}

export function pick<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Pick<T, K> {
  const entries = Object.entries(obj).filter(([key]) => keys.some((k) => k === key));
  return Object.fromEntries(entries) as Pick<T, K>;
}

export function omit<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Omit<T, K> {
  const entries = Object.entries(obj).filter(([key]) => !keys.some((k) => k === key));
  return Object.fromEntries(entries) as Omit<T, K>;
}

export function tally(words: readonly string[]): Record<string, number> {
  const counts = new Map<string, number>();
  for (const word of words) {
    counts.set(word, (counts.get(word) ?? 0) + 1);
  }
  return Object.fromEntries(counts);
}
