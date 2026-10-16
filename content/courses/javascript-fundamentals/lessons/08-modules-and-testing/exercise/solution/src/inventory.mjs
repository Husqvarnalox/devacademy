// Модуль склада. Артикулы нормализуются через sku.mjs, количества - положительные целые.
import { normalizeSku } from "./sku.mjs";

function checkQty(qty) {
  if (!Number.isInteger(qty) || qty < 1) {
    throw new RangeError(`Количество должно быть целым числом не меньше 1, получено: ${qty}`);
  }
}

export class Inventory {
  #stock = new Map();

  /** Добавляет qty единиц товара sku. Возвращает this, чтобы вызовы можно было сцеплять. */
  add(sku, qty) {
    const key = normalizeSku(sku);
    checkQty(qty);
    this.#stock.set(key, (this.#stock.get(key) ?? 0) + qty);
    return this;
  }

  /** Списывает qty единиц. Если на складе меньше (или товара нет) - RangeError. Нулевой остаток удаляет позицию. */
  remove(sku, qty) {
    const key = normalizeSku(sku);
    checkQty(qty);
    const have = this.#stock.get(key) ?? 0;
    if (have < qty) {
      throw new RangeError(`Недостаточно товара ${key}: есть ${have}, нужно ${qty}`);
    }
    if (have === qty) this.#stock.delete(key);
    else this.#stock.set(key, have - qty);
    return this;
  }

  /** Остаток артикула (0 для неизвестного). Некорректный артикул - ошибка normalizeSku. */
  stock(sku) {
    return this.#stock.get(normalizeSku(sku)) ?? 0;
  }

  /** Отсортированный массив артикулов с положительным остатком. */
  skus() {
    return [...this.#stock.keys()].sort();
  }

  /** Всего единиц на складе. */
  get total() {
    let sum = 0;
    for (const qty of this.#stock.values()) sum += qty;
    return sum;
  }
}
