// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./arrow_task.ts");
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

await test("arrows-basic", () =>
  expectEach("double", [[[4], 8], [[10], 20]]) ?? expectEach("isEven", [[[4], true], [[7], false]]));
await test("apply-basic", () => {
  const input = [1, 2, 3];
  const got = m.applyAll?.(input, (n) => n + 10);
  if (typeof m.applyAll !== "function") return "функция applyAll не найдена: проверьте имя и слово export";
  if (!same(got, [11, 12, 13])) return `applyAll([1, 2, 3], n => n + 10) вернуло ${show(got)}, ожидалось [11,12,13]`;
  if (!same(input, [1, 2, 3])) return "applyAll изменила исходный массив: нужен новый массив";
  return null;
});
await test("count-basic", () => {
  if (typeof m.countWhere !== "function") return "функция countWhere не найдена: проверьте имя и слово export";
  const got = m.countWhere([1, 2, 3, 4], (n) => n % 2 === 0);
  if (got !== 2) return `countWhere([1, 2, 3, 4], чётное) вернуло ${show(got)}, ожидалось 2`;
  return null;
});

await test("arrows-edge", () =>
  expectEach("double", [[[0], 0], [[-3], -6], [[0.5], 1]]) ?? expectEach("isEven", [[[0], true], [[-2], true], [[-3], false], [[1], false]]));
await test("apply-edge", () => {
  if (typeof m.applyAll !== "function") return "функция applyAll не найдена: проверьте имя и слово export";
  const a = m.applyAll([], (n) => n * 100);
  if (!same(a, [])) return `applyAll([], f) вернуло ${show(a)}, ожидался пустой массив`;
  const b = m.applyAll([3, -1], m.double ?? ((n) => n * 2));
  if (!same(b, [6, -2])) return `applyAll([3, -1], double) вернуло ${show(b)}, ожидалось [6,-2]`;
  const calls = [];
  const c = m.applyAll([5, 6, 7], (n) => { calls.push(n); return n; });
  if (!same(calls, [5, 6, 7]) || !same(c, [5, 6, 7])) return "функция f должна вызываться по одному разу для каждого элемента, по порядку";
  return null;
});
await test("count-edge", () => {
  if (typeof m.countWhere !== "function") return "функция countWhere не найдена: проверьте имя и слово export";
  const cases = [
    [[], (n) => true, 0],
    [[1, 2, 3], (n) => false, 0],
    [[1, 2, 3], (n) => true, 3],
    [[-4, -3, 0, 5], (n) => n < 0, 2],
    [[2, 4, 6], m.isEven ?? ((n) => n % 2 === 0), 3],
  ];
  for (const [values, f, want] of cases) {
    const got = m.countWhere(values, f);
    if (got !== want) return `countWhere(${show(values)}, f) вернуло ${show(got)}, ожидалось ${want}`;
  }
  return null;
});
