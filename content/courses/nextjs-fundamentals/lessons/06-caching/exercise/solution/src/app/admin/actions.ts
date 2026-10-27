"use server";

import { revalidateTag, updateTag } from "next/cache";
import { importAll, savePrice } from "../../lib/source";

export type ActionResult = { ok: true } | { ok: false; error: string };

export async function updatePrice(id: string, price: number): Promise<ActionResult> {
  if (typeof price !== "number" || !Number.isFinite(price) || price <= 0) {
    return { ok: false, error: "Цена должна быть числом больше нуля" };
  }
  if (price > 1_000_000) {
    return { ok: false, error: "Цена не может быть больше 1000000" };
  }
  if (Math.round(price * 100) / 100 !== price) {
    return { ok: false, error: "Не больше двух знаков после запятой" };
  }

  const saved = await savePrice(id, price);
  if (!saved) {
    return { ok: false, error: "Товар не найден" };
  }

  // Администратор сразу увидит новую цену этого товара...
  updateTag("product-" + id);
  // ...а общие списки обновятся в фоне: пока идёт пересборка, отдаётся прежняя версия.
  revalidateTag("products", "max");
  return { ok: true };
}

export async function importCatalog(): Promise<ActionResult> {
  await importAll();
  revalidateTag("products", "max");
  return { ok: true };
}
