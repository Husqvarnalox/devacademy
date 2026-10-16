// Цена хранится в копейках: так не бывает 0.1 + 0.2 !== 0.3.
export function formatPrice(cents) {
  const rubles = Math.floor(cents / 100);
  const kopecks = String(cents % 100).padStart(2, "0");
  return `${rubles}.${kopecks} ₽`;
}
