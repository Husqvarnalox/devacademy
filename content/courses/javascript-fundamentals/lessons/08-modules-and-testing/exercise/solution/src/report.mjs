// Модуль отчётов. Работает только с экземплярами Inventory из inventory.mjs.
import { Inventory } from "./inventory.mjs";

function checkInventory(inventory) {
  if (!(inventory instanceof Inventory)) {
    throw new TypeError("Ожидался экземпляр Inventory");
  }
}

/** Отсортированный массив артикулов, остаток которых строго меньше threshold. */
export function lowStock(inventory, threshold) {
  checkInventory(inventory);
  return inventory.skus().filter((sku) => inventory.stock(sku) < threshold);
}

/**
 * Строки отчёта: по строке "ABC-0001: 5" на артикул (по возрастанию артикула),
 * затем итог "Всего: <позиций> поз., <единиц> ед.". Для пустого склада - только итог.
 */
export function formatReport(inventory) {
  checkInventory(inventory);
  const skus = inventory.skus();
  const lines = skus.map((sku) => `${sku}: ${inventory.stock(sku)}`);
  lines.push(`Всего: ${skus.length} поз., ${inventory.total} ед.`);
  return lines;
}
