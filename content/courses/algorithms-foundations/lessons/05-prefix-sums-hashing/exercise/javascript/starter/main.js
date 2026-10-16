const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными).
// Префиксные суммы до ~2e14 и ответ до ~2e10 безопасно помещаются в Number (ключи Map сравниваются по значению).
function countSegments(a, k) {
  // TODO: префиксные суммы + Map "сумма -> сколько раз встречалась", O(n)
  return 0;
}

const n = data[0];
const k = data[1];
console.log(String(countSegments(data.slice(2, 2 + n), k)));
