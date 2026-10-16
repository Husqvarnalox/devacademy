// Цена хранится в копейках. Нужен вид "12.50 ₽": рубли, точка, ровно две цифры копеек, пробел и знак рубля.
export function formatPrice(cents) {
  return String(cents);
}
