// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./collections_check.mjs");
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

const json = (v) => JSON.stringify(v);
function expectEach(fn, name, cases) {
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (json(got) !== json(want) || !Object.is(typeof got, typeof want)) return `${name}(${args.map(json).join(", ")}) вернуло ${json(got)}, ожидалось ${json(want)}`;
  }
  return null;
}
await test("top-basic", () =>
  expectEach(m.topScorers, "topScorers", [[[[{ name: "Ann", score: 40 }, { name: "Bob", score: 75 }, { name: "Cid", score: 90 }], 50], ["Cid", "Bob"]]]));
await test("unique-basic", () =>
  expectEach(m.unique, "unique", [[[[1, 2, 1, 3, 2]], [1, 2, 3]], [[["a", "b", "a"]], ["a", "b"]]]));
await test("total-basic", () =>
  expectEach(m.totalByCategory, "totalByCategory", [[[[{ category: "food", amount: 5 }, { category: "fun", amount: 3 }, { category: "food", amount: 7 }]], { food: 12, fun: 3 }]]));

await test("top-edge", () => {
  const players = [{ name: "Zed", score: 10 }, { name: "Amy", score: 10 }, { name: "Bob", score: 3 }, { name: "Cat", score: 10 }];
  const before = json(players);
  const got = m.topScorers(players, 3);
  if (json(players) !== before) return "topScorers изменила исходный массив players";
  if (json(got) !== json(["Amy", "Cat", "Zed", "Bob"])) return `topScorers вернула ${json(got)}, ожидалось ["Amy","Cat","Zed","Bob"]`;
  if (json(m.topScorers(players, 11)) !== "[]") return "при пороге выше всех очков ожидался пустой массив";
  return json(m.topScorers([], 0)) === "[]" ? null : "для пустого списка ожидался пустой массив";
});
await test("unique-edge", () => {
  const input = [0, "0", 1, "1", 0, NaN, NaN, false, "", ""];
  const got = m.unique(input);
  if (got === input) return "unique должна вернуть новый массив";
  const shown = got.map((v) => (typeof v === "string" ? JSON.stringify(v) : String(v))).join(", ");
  const ok = got.length === 7 && got[0] === 0 && got[1] === "0" && got[2] === 1 && got[3] === "1" && Number.isNaN(got[4]) && got[5] === false && got[6] === "";
  if (!ok) return `unique вернула ${shown}, ожидалось 0, "0", 1, "1", NaN, false, ""`;
  return json(m.unique([])) === "[]" ? null : "для пустого массива ожидался пустой массив";
});
await test("total-edge", () => {
  const items = [{ category: "a", amount: 0.5 }, { category: "a", amount: 0.25 }, { category: "b", amount: -2 }];
  const got = m.totalByCategory(items);
  if (json(got) !== json({ a: 0.75, b: -2 })) return `totalByCategory вернула ${json(got)}, ожидалось {"a":0.75,"b":-2}`;
  return json(m.totalByCategory([])) === "{}" ? null : "для пустого списка ожидался пустой объект";
});
