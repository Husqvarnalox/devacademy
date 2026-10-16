const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0).
// Ответ до ~2e10 и суммы до ~2e14 безопасно помещаются в Number.
function countSegments(a, limit) {
  let total = 0;
  let left = 0; // окно a[left..right]; инвариант: его сумма cur <= limit (или окно пусто)
  let cur = 0;
  for (let right = 0; right < a.length; right++) {
    cur += a[right];
    while (cur > limit) {
      cur -= a[left];
      left++;
    }
    total += right - left + 1; // подходящие отрезки, оканчивающиеся в right
  }
  return total;
}

const n = data[0];
const limit = data[1];
console.log(String(countSegments(data.slice(2, 2 + n), limit)));
