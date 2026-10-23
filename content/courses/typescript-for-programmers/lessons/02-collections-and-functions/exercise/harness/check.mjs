// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./collections_task.ts");
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

const sameArr = (got, want) =>
  Array.isArray(got) && got.length === want.length && want.every((v, i) => Object.is(got[i], v));

function checkMap(got, wantEntries, label) {
  if (!(got instanceof Map)) return `${label}: нужно вернуть Map`;
  const gotKeys = [...got.keys()].join("|");
  const wantKeys = wantEntries.map(([k]) => k).join("|");
  if (gotKeys !== wantKeys) return `${label}: ключи ${JSON.stringify([...got.keys()])}, ожидалось ${JSON.stringify(wantEntries.map(([k]) => k))} (порядок - порядок первого появления)`;
  for (const [k, v] of wantEntries) {
    if (got.get(k) !== v) return `${label}: для ключа ${JSON.stringify(k)} получено ${show(got.get(k))}, ожидалось ${v}`;
  }
  return null;
}

await test("sort-basic", () => {
  const got = m.sortedNumbers([10, 9, 1, 100]);
  return sameArr(got, [1, 9, 10, 100]) ? null : `sortedNumbers([10, 9, 1, 100]) вернуло ${JSON.stringify(got)}, ожидалось [1,9,10,100]`;
});
await test("totals-basic", () =>
  checkMap(
    m.totalsByCategory([
      { category: "еда", amount: 200 },
      { category: "транспорт", amount: 120 },
      { category: "еда", amount: 150, note: "обед" },
    ]),
    [["еда", 350], ["транспорт", 120]],
    "totalsByCategory",
  ));
await test("cheapest-basic", () => {
  const a = { name: "чай", price: 120 };
  const b = { name: "кофе", price: 90 };
  const c = { name: "какао", price: 150 };
  const got = m.cheapest([a, b, c]);
  return got === b ? null : `cheapest вернула ${JSON.stringify(got)}, ожидался товар кофе (90)`;
});
await test("same-basic", () =>
  expectEach("sameItems", [[[[1, 2], [1, 2]], true], [[[1, 2], [2, 1]], false], [[[1], [1, 2]], false], [[[], []], true]]));

await test("sort-edge", () => {
  const input = [3, -1, 20, 3, 0];
  const got = m.sortedNumbers(input);
  if (!sameArr(input, [3, -1, 20, 3, 0])) return "sortedNumbers изменила исходный массив";
  if (got === input) return "sortedNumbers вернула тот же массив, нужен новый";
  if (!sameArr(got, [-1, 0, 3, 3, 20])) return `sortedNumbers([3,-1,20,3,0]) вернуло ${JSON.stringify(got)}`;
  const frozen = Object.freeze([2, 1]);
  if (!sameArr(m.sortedNumbers(frozen), [1, 2])) return "для замороженного массива [2, 1] ожидалось [1, 2]";
  return sameArr(m.sortedNumbers([]), []) ? null : "для пустого массива ожидался пустой массив";
});
await test("totals-edge", () => {
  const got = m.totalsByCategory([
    { category: "constructor", amount: 10 },
    { category: "toString", amount: 5 },
    { category: "__proto__", amount: 7 },
    { category: "constructor", amount: 1 },
    { category: "x", amount: 0 },
  ]);
  const problem = checkMap(got, [["constructor", 11], ["toString", 5], ["__proto__", 7], ["x", 0]], "totalsByCategory");
  if (problem) return problem;
  return m.totalsByCategory([]).size === 0 ? null : "для пустого списка ожидалась пустая Map";
});
await test("cheapest-edge", () => {
  const free = { name: "проба", price: 0 };
  const a = { name: "а", price: 50 };
  const b = { name: "б", price: 50 };
  const none = { name: "без цены" };
  if (m.cheapest([]) !== undefined) return "для пустого списка ожидалось undefined";
  if (m.cheapest([none]) !== undefined) return "товар без цены не участвует: ожидалось undefined";
  if (m.cheapest([none, a]) !== a) return "товар без цены нужно пропустить: ожидался товар а";
  if (m.cheapest([a, free, b]) !== free) return "цена 0 - тоже цена: ожидался товар проба";
  return m.cheapest([a, b]) === a ? null : "при равных ценах нужен тот, что раньше в списке";
});
await test("same-edge", () => {
  const inner = [1];
  return expectEach("sameItems", [
    [[[NaN, 1], [NaN, 1]], true], [[[0], [-0]], true], [[[[1]], [[1]]], false], [[[inner], [inner]], true],
    [[[1, "1"], [1, 1]], false], [[[undefined], [null]], false], [[[null], [null]], true],
  ]);
});
