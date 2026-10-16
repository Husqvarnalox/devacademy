// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./arrays.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await Promise.race([
      Promise.resolve().then(check),
      new Promise((resolve) => setTimeout(() => resolve("тест не завершился за 3 секунды"), 3000).unref()),
    ]);
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

function expect(actual, want, what) {
  return Object.is(actual, want) ? null : `${what} вернуло ${String(actual)}, ожидалось ${String(want)}`;
}
const same = (a, b, what) => (JSON.stringify(a) === JSON.stringify(b) ? null : `${what}: получено ${JSON.stringify(a)}, ожидалось ${JSON.stringify(b)}`);

await test("sort-basic", () => same(m.sortNumbers([10, 9, 1, 100, -5]), [-5, 1, 9, 10, 100], "sortNumbers([10, 9, 1, 100, -5])"));
await test("count-by", () => {
  const got = [...m.countBy(["apple", "avocado", "banana", "blueberry", "cherry", "apricot"], (w) => w[0])];
  return same(got, [["a", 3], ["b", 2], ["c", 1]], "countBy по первой букве");
});
await test("chunks-basic", () => same([...m.chunks([1, 2, 3, 4, 5], 2)], [[1, 2], [3, 4], [5]], "chunks([1..5], 2)"));
await test("summarize-basic", () => expect(m.summarize({ name: "Ира", age: 30, tags: ["a", "b"] }), "Ира:30:2", "summarize(полный объект)"));

await test("sort-no-mutation", () => {
  const src = [3, 1, 2];
  const out = m.sortNumbers(src);
  return same(src, [3, 1, 2], "исходный массив не должен меняться") ?? expect(out === src, false, "должен вернуться новый массив")
    ?? same(m.sortNumbers([]), [], "пустой массив") ?? same(m.sortNumbers([0.5, -0.5, 0.25]), [-0.5, 0.25, 0.5], "дробные числа");
});
await test("count-by-edge", () => {
  const empty = m.countBy([], (x) => x);
  const byLen = m.countBy(["aa", "b", "cc", "dd", "e"], (s) => s.length);
  return expect(empty instanceof Map && empty.size === 0, true, "countBy([]) - пустая Map") ?? same([...byLen], [[2, 3], [1, 2]], "ключи в порядке первого появления")
    ?? expect(m.countBy([NaN, NaN], (x) => x).get(NaN), 2, "ключи Map сравниваются по SameValueZero");
});
await test("chunks-lazy", () => {
  let produced = 0;
  function* naturals() { for (let i = 1; ; i++) { produced++; yield i; } }
  const it = m.chunks(naturals(), 3);
  const a = it.next().value;
  const b = it.next().value;
  return same(a, [1, 2, 3], "первая порция") ?? same(b, [4, 5, 6], "вторая порция") ?? expect(produced, 6, "источник прочитан ровно на 6 элементов");
});
await test("chunks-edge", () =>
  same([...m.chunks([], 3)], [], "chunks([], 3)") ?? same([...m.chunks("abcde", 2)], [["a", "b"], ["c", "d"], ["e"]], "chunks по строке")
  ?? same([...m.chunks(new Set([1, 2, 3]), 3)], [[1, 2, 3]], "chunks по Set с size === длине"));
await test("summarize-defaults", () =>
  expect(m.summarize(), "аноним:18:0", "summarize()") ?? expect(m.summarize({}), "аноним:18:0", "summarize({})") ?? expect(m.summarize({ age: 0 }), "аноним:0:0", "age: 0 не заменяется умолчанием")
  ?? expect(m.summarize({ name: "Ян", tags: ["x"] }), "Ян:18:1", "частичный объект") ?? expect(m.summarize({ age: null }), "аноним:null:0", "null не заменяется умолчанием"));
