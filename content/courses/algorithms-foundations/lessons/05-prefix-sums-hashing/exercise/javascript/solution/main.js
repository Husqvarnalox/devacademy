const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными).
// Префиксные суммы до ~2e14 и ответ до ~2e10 безопасно помещаются в Number (ключи Map сравниваются по значению).
function countSegments(a, k) {
  const seen = new Map(); // префиксная сумма -> сколько раз встречалась
  seen.set(0, 1);         // пустой префикс
  let cur = 0;
  let ans = 0;
  for (const x of a) {
    cur += x;
    ans += seen.get(cur - k) ?? 0; // левые границы, для которых сумма отрезка ровно k
    seen.set(cur, (seen.get(cur) ?? 0) + 1);
  }
  return ans;
}

const n = data[0];
const k = data[1];
console.log(String(countSegments(data.slice(2, 2 + n), k)));
