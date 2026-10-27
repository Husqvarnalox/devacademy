// TODO: сделайте файл файлом серверных действий.
import { importAll, savePrice } from "../../lib/source";

export type ActionResult = { ok: true } | { ok: false; error: string };

export async function updatePrice(id: string, price: number): Promise<ActionResult> {
  // TODO: проверьте цену, сохраните её (savePrice) и сбросьте нужные теги.
  void id;
  void price;
  void savePrice;
  return { ok: true };
}

export async function importCatalog(): Promise<ActionResult> {
  // TODO: после загрузки (importAll) пометьте тег "products" устаревшим.
  void importAll;
  return { ok: true };
}
