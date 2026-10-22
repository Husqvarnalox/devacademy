// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./collections_check.ts");
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

const same2 = (got, want) => Array.isArray(got) && got.length === want.length && want.every((v, i) => Object.is(got[i], v));
await test("unique-basic", () => {
  const got = m.uniqueSorted([3, 1, 3, 2, 1]);
  return same2(got, [1, 2, 3]) ? null : `uniqueSorted([3, 1, 3, 2, 1]) вернуло ${JSON.stringify(got)}, ожидалось [1,2,3]`;
});
await test("word-basic", () =>
  expectEach("longestWord", [[["я люблю программирование"], "программирование"], [["кот пёс"], "кот"], [["one"], "one"]]));
await test("total-basic", () =>
  expectEach("totalPrice", [[[[{ name: "чай", price: 100, qty: 2 }, { name: "хлеб", price: 40 }]], 240], [[[]], 0]]));
await test("find-basic", () => {
  const tea = { name: "Чай", price: 100 };
  const bread = { name: "хлеб", price: 40 };
  if (m.findItem([tea, bread], "хлеб") !== bread) return "findItem([...], \"хлеб\") должна вернуть тот же объект хлеба";
  return m.findItem([tea, bread], "сыр") === undefined ? null : "для отсутствующего товара ожидалось undefined";
});

await test("unique-edge", () => {
  const input = [5, -1, 5, 20, 0, -1, 3];
  const got = m.uniqueSorted(input);
  if (!same2(input, [5, -1, 5, 20, 0, -1, 3])) return "uniqueSorted изменила исходный массив";
  if (!same2(got, [-1, 0, 3, 5, 20])) return `uniqueSorted([5,-1,5,20,0,-1,3]) вернуло ${JSON.stringify(got)}`;
  if (!same2(m.uniqueSorted([]), [])) return "для пустого массива ожидался пустой массив";
  return same2(m.uniqueSorted([10, 9, 1, 100]), [1, 9, 10, 100]) ? null : "числа нужно сортировать по значению, а не как строки";
});
await test("word-edge", () =>
  expectEach("longestWord", [[[""], ""], [["   "], ""], [["  а   бб  вв  "], "бб"], [["a\tbcd\nef"], "bcd"], [["ааа ббб"], "ааа"]]));
await test("total-edge", () =>
  expectEach("totalPrice", [[[[{ name: "а", price: 10, qty: 0 }]], 0], [[[{ name: "а", price: 7, qty: 3 }, { name: "б", price: 1 }]], 22], [[[{ name: "бесплатно", price: 0 }]], 0]]));
await test("find-edge", () => {
  const a = { name: "Кофе", price: 200 };
  const b = { name: "кофе", price: 150 };
  if (m.findItem([a, b], "  КОФЕ ") !== a) return "при нескольких совпадениях нужен первый; пробелы и регистр в имени запроса не важны";
  return m.findItem([], "чай") === undefined ? null : "для пустого списка ожидалось undefined";
});
