const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0).
// Ответ до ~2e10 и суммы до ~2e14 безопасно помещаются в Number.
function countSegments(a, limit) {
  // TODO: два указателя (скользящее окно), O(n)
  return 0;
}

const n = data[0];
const limit = data[1];
console.log(String(countSegments(data.slice(2, 2 + n), limit)));
