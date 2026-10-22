// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./utility_check.ts");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `файл не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

const show = (v) => {
  if (typeof v === "string") return JSON.stringify(v);
  if (Object.is(v, -0)) return "-0";
  if (Array.isArray(v) || (v && typeof v === "object")) return JSON.stringify(v);
  return String(v);
};
const same = (a, b) => {
  if (Array.isArray(a) && Array.isArray(b)) return a.length === b.length && a.every((x, i) => same(x, b[i]));
  if (a && b && typeof a === "object" && typeof b === "object" && !Array.isArray(a) && !Array.isArray(b)) {
    const ka = Object.keys(a).sort(), kb = Object.keys(b).sort();
    return ka.length === kb.length && ka.every((k, i) => k === kb[i] && same(a[k], b[k]));
  }
  return Object.is(a, b);
};
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!same(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

function expectThrows(name, args, message) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  let got;
  try {
    got = fn(...args);
  } catch (e) {
    if (!(e instanceof Error)) return `${name}(${args.map(show).join(", ")}) выбросило не Error, а ${show(e)}`;
    if (e.message !== message) return `${name}(${args.map(show).join(", ")}) выбросило сообщение ${show(e.message)}, ожидалось ${show(message)}`;
    return null;
  }
  return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, а ожидалось исключение с сообщением ${show(message)}`;
}

await test("patch-basic", () => {
  const user = { id: 1, name: "Аня", age: 30 };
  const got = m.applyPatch(user, { name: "Анна", email: "a@x.ru" });
  return same(got, { id: 1, name: "Анна", age: 30, email: "a@x.ru" }) ? null : `applyPatch вернуло ${show(got)}`;
});
await test("pick-basic", () =>
  expectEach("pick", [[[{ a: 1, b: "x", c: true }, ["a", "c"]], { a: 1, c: true }], [[{ a: 1 }, []], {}]]));
await test("omit-basic", () =>
  expectEach("omit", [[[{ a: 1, b: "x", c: true }, ["b"]], { a: 1, c: true }], [[{ a: 1 }, []], { a: 1 }]]));
await test("tally-basic", () =>
  expectEach("tally", [[[["a", "b", "a"]], { a: 2, b: 1 }], [[[]], {}]]));

await test("patch-edge", () => {
  const user = Object.freeze({ id: 1, name: "Аня", age: 30 });
  const got = m.applyPatch(user, { age: 0, name: undefined, email: undefined });
  if (got === user) return "applyPatch должна вернуть новый объект, а не исходный";
  if (got.id !== 1 || got.email !== undefined) return `undefined в патче должен игнорироваться, получено ${show(got)}`;
  if (got.name !== "Аня") return "name со значением undefined в патче должен остаться прежним";
  if (got.age !== 0) return "age равный 0 - допустимое значение патча, его нужно применить";
  return same(user, { id: 1, name: "Аня", age: 30 }) ? null : "исходный объект менять нельзя";
});
await test("pick-edge", () => {
  const src = { a: 1, b: { x: 1 } };
  const got = m.pick(src, ["b"]);
  if (got === src || !same(got, { b: { x: 1 } })) return `pick вернул ${show(got)}`;
  if (!same(m.pick({ a: 0, b: undefined }, ["a", "b"]), { a: 0, b: undefined })) return "значения 0 и undefined должны сохраняться";
  return same(m.pick({ a: 1 }, ["a", "a"]), { a: 1 }) ? null : "повтор ключа не должен менять результат";
});
await test("omit-edge", () => {
  const src = { a: 1, b: 2 };
  const got = m.omit(src, ["a"]);
  if (!same(got, { b: 2 })) return `omit вернул ${show(got)}`;
  if (!same(src, { a: 1, b: 2 })) return "исходный объект менять нельзя";
  return same(m.omit({ a: 1, b: 2 }, ["a", "b"]), {}) ? null : "если убраны все ключи, нужен пустой объект";
});
await test("tally-edge", () => {
  const got = m.tally(["constructor", "a", "toString", "constructor", "valueOf"]);
  const want = { constructor: 2, a: 1, toString: 1, valueOf: 1 };
  for (const key of Object.keys(want)) {
    if (!Object.hasOwn(got, key) || got[key] !== want[key]) return `tally: для слова ${show(key)} ожидалось ${want[key]}, получено ${show(Object.hasOwn(got, key) ? got[key] : "(нет)")}`;
  }
  return Object.keys(got).length === 4 ? null : `в результате ${Object.keys(got).length} ключей, ожидалось 4`;
});
