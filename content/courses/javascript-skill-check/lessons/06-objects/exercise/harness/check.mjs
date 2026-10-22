// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./objects_check.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

import { readFileSync } from "node:fs";

const eq = (got, want, label) => {
  const g = JSON.stringify(got), w = JSON.stringify(want);
  return g === w ? null : `${label}: получено ${g}, ожидалось ${w}`;
};

await test("stack-basic", () => {
  const s = new m.Stack();
  return (
    eq(s.push(1), 1, "push(1)") ?? eq(s.push(2), 2, "push(2)") ?? eq(s.peek(), 2, "peek()") ??
    eq(s.size, 2, "size") ?? eq(s.pop(), 2, "pop()") ?? eq(s.size, 1, "size после pop") ?? eq(s.toArray(), [1], "toArray()")
  );
});
await test("chain-basic", () => {
  class Animal {}
  class Dog extends Animal {}
  return eq(m.protoChain([]), ["Array", "Object"], "protoChain([])") ?? eq(m.protoChain(new Dog()), ["Dog", "Animal", "Object"], "protoChain(new Dog())");
});
await test("kind-basic", () => {
  const base = { inherited: 1 };
  const o = Object.create(base);
  o.own = 2;
  return eq([m.lookupKind(o, "own"), m.lookupKind(o, "inherited"), m.lookupKind(o, "nope")], ["own", "inherited", "missing"], "lookupKind");
});

await test("stack-private", () => {
  const s = new m.Stack();
  s.push("a", "b");
  const copy = s.toArray();
  copy.push("zzz");
  class Sub extends m.Stack {}
  const sub = Sub.from(new Set([1, 2, 2, 3]));
  const gen = m.Stack.from((function* () { yield 7; yield 8; })());
  let source = "";
  try { source = readFileSync(new URL("./objects_check.mjs", import.meta.url), "utf8"); } catch {}
  return (
    eq(Object.getOwnPropertyNames(s), [], "собственные свойства стека") ??
    eq(JSON.stringify(s), "{}", "JSON.stringify(стек)") ??
    eq([s.items, s._items, s.stack].every((v) => v === undefined), true, "доступ к хранилищу снаружи") ??
    eq(s.size, 2, "размер после изменения копии") ??
    eq(sub instanceof Sub, true, "Sub.from возвращает экземпляр Sub") ??
    eq(sub.toArray(), [1, 2, 3], "Sub.from(new Set(...))") ??
    eq(gen.toArray(), [7, 8], "Stack.from(генератор)") ??
    eq(/#[A-Za-z_]\w*\s*(=|;)/.test(source), true, "в коде нет приватного поля с #")
  );
});
await test("stack-edge", () => {
  const s = new m.Stack();
  return (
    eq(s.pop() === undefined && s.peek() === undefined, true, "pop и peek пустого стека") ??
    eq(s.push(1, 2, 3), 3, "push(1, 2, 3)") ?? eq(s.toArray(), [1, 2, 3], "порядок элементов") ??
    eq(s.pop(), 3, "pop() после push(1, 2, 3)") ?? eq(m.Stack.from([]).size, 0, "Stack.from([])")
  );
});
await test("chain-edge", () => {
  const bare = Object.create(null);
  const plain = Object.create({});
  const withCtor = Object.create(Object.create(Array.prototype));
  return (
    eq(m.protoChain(bare), [], "protoChain(Object.create(null))") ??
    eq(m.protoChain(plain), ["(anonymous)", "Object"], "protoChain(Object.create({}))") ??
    eq(m.protoChain(withCtor), ["(anonymous)", "Array", "Object"], "protoChain(Object.create(Object.create(Array.prototype)))") ??
    eq(m.protoChain(() => 1), ["Function", "Object"], "protoChain(функция)")
  );
});
await test("kind-edge", () => {
  const bare = Object.create(null);
  bare.a = 1;
  const arr = [1];
  const o = { toString: 1 };
  return (
    eq([m.lookupKind(bare, "a"), m.lookupKind(bare, "toString")], ["own", "missing"], "объект с null-прототипом") ??
    eq([m.lookupKind(arr, "length"), m.lookupKind(arr, "map"), m.lookupKind(arr, "0")], ["own", "inherited", "own"], "массив") ??
    eq([m.lookupKind(o, "toString"), m.lookupKind(o, "valueOf")], ["own", "inherited"], "затенение метода")
  );
});
