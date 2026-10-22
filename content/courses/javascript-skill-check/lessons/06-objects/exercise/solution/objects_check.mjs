export class Stack {
  #items = [];

  push(...values) {
    this.#items.push(...values);
    return this.#items.length;
  }

  pop() {
    return this.#items.pop();
  }

  peek() {
    return this.#items[this.#items.length - 1];
  }

  get size() {
    return this.#items.length;
  }

  toArray() {
    return [...this.#items];
  }

  static from(iterable) {
    const stack = new this();
    stack.push(...iterable);
    return stack;
  }
}

export function protoChain(obj) {
  const names = [];
  for (let p = Object.getPrototypeOf(obj); p !== null; p = Object.getPrototypeOf(p)) {
    names.push(Object.hasOwn(p, "constructor") && typeof p.constructor === "function" ? p.constructor.name : "(anonymous)");
  }
  return names;
}

export function lookupKind(obj, key) {
  if (Object.hasOwn(obj, key)) return "own";
  return key in obj ? "inherited" : "missing";
}
