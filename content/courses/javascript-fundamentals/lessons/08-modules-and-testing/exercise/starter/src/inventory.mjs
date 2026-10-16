// Модуль склада. Артикулы нормализуются через sku.mjs, количества - положительные целые.
import { normalizeSku } from "./sku.mjs";

export class Inventory {
  // TODO: заведите приватную Map #stock (артикул -> остаток)

  /** Добавляет qty единиц товара sku. Возвращает this, чтобы вызовы можно было сцеплять. */
  add(sku, qty) {
    throw new Error("add не реализован");
  }

  /** Списывает qty единиц. Если на складе меньше (или товара нет) - RangeError. Нулевой остаток удаляет позицию. */
  remove(sku, qty) {
    throw new Error("remove не реализован");
  }

  /** Остаток артикула (0 для неизвестного). Некорректный артикул - ошибка normalizeSku. */
  stock(sku) {
    throw new Error("stock не реализован");
  }

  /** Отсортированный массив артикулов с положительным остатком. */
  skus() {
    throw new Error("skus не реализован");
  }

  /** Всего единиц на складе. */
  get total() {
    throw new Error("total не реализован");
  }
}
