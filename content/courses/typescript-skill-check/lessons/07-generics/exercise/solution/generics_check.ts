export function first<T>(items: readonly T[]): T | undefined {
  if (items.length === 0) {
    return undefined;
  }
  return items[0];
}

export function pluck<T, K extends keyof T>(items: readonly T[], key: K): T[K][] {
  return items.map((item) => item[key]);
}

export function groupBy<T, K>(items: readonly T[], keyOf: (item: T) => K): Map<K, T[]> {
  const groups = new Map<K, T[]>();
  for (const item of items) {
    const key = keyOf(item);
    const group = groups.get(key);
    if (group === undefined) {
      groups.set(key, [item]);
    } else {
      group.push(item);
    }
  }
  return groups;
}

export class Stack<T> {
  #items: T[] = [];

  push(item: T): void {
    this.#items.push(item);
  }

  pop(): T | undefined {
    return this.#items.pop();
  }

  peek(): T | undefined {
    return this.#items.length === 0 ? undefined : this.#items[this.#items.length - 1];
  }

  get size(): number {
    return this.#items.length;
  }
}
