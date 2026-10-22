export class Range {
  #start;
  #end;
  #step;

  constructor(start, end, step = 1) {
    if (step === 0) throw new RangeError("step must not be 0");
    this.#start = start;
    this.#end = end;
    this.#step = step;
  }

  *[Symbol.iterator]() {
    if (this.#step > 0) {
      for (let i = this.#start; i < this.#end; i += this.#step) yield i;
    } else {
      for (let i = this.#start; i > this.#end; i += this.#step) yield i;
    }
  }
}

export function* naturals(start = 0) {
  for (let i = start; ; i++) yield i;
}

export function* map(iterable, fn) {
  let index = 0;
  for (const item of iterable) yield fn(item, index++);
}

export function* filter(iterable, pred) {
  let index = 0;
  for (const item of iterable) {
    if (pred(item, index++)) yield item;
  }
}

export function* take(iterable, n) {
  if (n <= 0) return;
  let count = 0;
  for (const item of iterable) {
    yield item;
    if (++count >= n) return;
  }
}
