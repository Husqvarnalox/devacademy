// Ленивый конвейер поверх итераторов.

class Lazy {
  #source;

  constructor(source) {
    this.#source = source;
  }

  [Symbol.iterator]() {
    return this.#source[Symbol.iterator]();
  }

  map(fn) {
    const source = this.#source;
    return new Lazy({
      *[Symbol.iterator]() {
        for (const item of source) yield fn(item);
      },
    });
  }

  filter(predicate) {
    const source = this.#source;
    return new Lazy({
      *[Symbol.iterator]() {
        for (const item of source) if (predicate(item)) yield item;
      },
    });
  }

  take(count) {
    const source = this.#source;
    return new Lazy({
      *[Symbol.iterator]() {
        if (count <= 0) return;
        let taken = 0;
        for (const item of source) {
          yield item;
          if (++taken >= count) return; // return из for...of закрывает источник (вызывает return() итератора)
        }
      },
    });
  }

  toArray() {
    return [...this];
  }
}

/** Оборачивает любой итерируемый объект в ленивый конвейер с методами map, filter, take, toArray. */
export function lazy(iterable) {
  return new Lazy(iterable);
}
