// Объекты и прототипы: приватный стек, цепочка прототипов и тип свойства.

export class Stack {
  // TODO: хранилище должно быть приватным полем (#items), а не обычным свойством
  items = [];

  push(...values) {
    this.items.push(...values);
    return this.items.length;
  }

  pop() {
    return this.items.pop();
  }

  peek() {
    return this.items[this.items.length - 1];
  }

  get size() {
    return this.items.length;
  }

  toArray() {
    return this.items;
  }

  static from(iterable) {
    const s = new Stack();
    s.push(...iterable);
    return s;
  }
}

/**
 * Имена конструкторов вдоль цепочки прототипов obj ("(anonymous)", если у прототипа нет собственного constructor).
 */
export function protoChain(obj) {
  // TODO: пройдите по Object.getPrototypeOf до null
  return [];
}

/** "own" | "inherited" | "missing". */
export function lookupKind(obj, key) {
  // TODO: различите собственное и унаследованное свойство
  return key in obj ? "own" : "missing";
}
