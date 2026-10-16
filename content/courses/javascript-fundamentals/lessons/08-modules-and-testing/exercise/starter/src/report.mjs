// Модуль отчётов. Работает только с экземплярами Inventory из inventory.mjs.
import { Inventory } from "./inventory.mjs";

/** Отсортированный массив артикулов, остаток которых строго меньше threshold. */
export function lowStock(inventory, threshold) {
  throw new Error("lowStock не реализована");
}

/**
 * Строки отчёта: по строке "ABC-0001: 5" на артикул (по возрастанию артикула),
 * затем итог "Всего: <позиций> поз., <единиц> ед.". Для пустого склада - только итог.
 * Не экземпляр Inventory - TypeError.
 */
export function formatReport(inventory) {
  throw new Error("formatReport не реализована");
}
