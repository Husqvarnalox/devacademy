// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./generics_check.ts");
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

await test("first-basic", () =>
  expectEach("first", [[[[7, 8]], 7], [[["a"]], "a"], [[[]], undefined]]));
await test("pluck-basic", () =>
  expectEach("pluck", [[[[{ a: 1, b: "x" }, { a: 2, b: "y" }], "a"], [1, 2]], [[[{ a: 1, b: "x" }, { a: 2, b: "y" }], "b"], ["x", "y"]]]));
await test("group-basic", () => {
  const got = m.groupBy([1, 2, 3, 4, 5], (n) => (n % 2 === 0 ? "чёт" : "нечёт"));
  const text = JSON.stringify([...got]);
  const want = '[["нечёт",[1,3,5]],["чёт",[2,4]]]';
  return text === want ? null : `groupBy вернуло ${text}, ожидалось ${want}`;
});
await test("stack-basic", () => {
  const s = new m.Stack();
  s.push("a");
  s.push("b");
  if (s.size !== 2) return `после двух push size равен ${s.size}, ожидалось 2`;
  if (s.peek() !== "b") return `peek вернул ${s.peek()}, ожидалось "b"`;
  if (s.pop() !== "b") return "pop должен вернуть последний добавленный элемент";
  return s.size === 1 ? null : `после pop size равен ${s.size}, ожидалось 1`;
});

await test("first-edge", () =>
  expectEach("first", [[[[0]], 0], [[[""]], ""], [[[null, 1]], null], [[[undefined, 1]], undefined]]));
await test("pluck-edge", () =>
  expectEach("pluck", [[[[], "a"], []], [[[{ a: 0 }, { a: null }, { a: undefined }], "a"], [0, null, undefined]]]));
await test("group-edge", () => {
  const empty = m.groupBy([], (x) => x);
  if (!(empty instanceof Map) || empty.size !== 0) return "для пустого списка нужен пустой Map";
  const words = ["лук", "рис", "мёд", "лимон", "ром"];
  const got = m.groupBy(words, (w) => w.length);
  const text = JSON.stringify([...got]);
  const want = '[[3,["лук","рис","мёд","ром"]],[5,["лимон"]]]';
  if (text !== want) return `groupBy по длине вернуло ${text}, ожидалось ${want}`;
  const objects = [{ id: 1, k: "constructor" }, { id: 2, k: "constructor" }, { id: 3, k: "__proto__" }];
  const g2 = m.groupBy(objects, (o) => o.k);
  const t2 = JSON.stringify([...g2].map(([k, v]) => [k, v.map((o) => o.id)]));
  const w2 = '[["constructor",[1,2]],["__proto__",[3]]]';
  return t2 === w2 ? null : `ключи constructor и __proto__ должны работать как обычные: ${t2}, ожидалось ${w2}`;
});
await test("stack-edge", () => {
  const s = new m.Stack();
  if (s.pop() !== undefined || s.peek() !== undefined) return "для пустого стека pop и peek должны вернуть undefined";
  if (s.size !== 0) return "пустой стек: size должен быть 0";
  s.push(0);
  s.push(undefined);
  s.push(null);
  if (s.size !== 3) return `после трёх push size равен ${s.size}, ожидалось 3`;
  if (s.pop() !== null || s.pop() !== undefined || s.pop() !== 0) return "pop должен снимать элементы в обратном порядке, включая 0, undefined и null";
  if (s.size !== 0) return "после снятия всех элементов size должен быть 0";
  const a = new m.Stack();
  const b = new m.Stack();
  a.push(1);
  return b.size === 0 ? null : "у каждого стека своё хранилище элементов";
});
