// Ленивый конвейер поверх итераторов.
//
// lazy(iterable) возвращает объект с методами:
//   map(fn), filter(predicate), take(n)  - каждый возвращает новый ленивый конвейер;
//   toArray()                            - выполняет конвейер и возвращает массив;
// а сам конвейер итерируемый (for...of, spread).
// Ничего не вычисляется, пока конвейер не начали обходить, и каждый элемент проходит
// через всю цепочку, прежде чем будет прочитан следующий. take(n) не читает лишний элемент
// и закрывает источник (его блок finally выполняется).

export function lazy(iterable) {
  // TODO: эта версия жадная и ломается на бесконечных источниках
  const items = [...iterable];
  return {
    map: (fn) => lazy(items.map(fn)),
    filter: (predicate) => lazy(items.filter(predicate)),
    take: (n) => lazy(items.slice(0, n)),
    toArray: () => items,
    [Symbol.iterator]: () => items[Symbol.iterator](),
  };
}
