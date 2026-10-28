// Источник данных каталога. Файл не меняйте: тесты рассчитывают на эти позиции.

export interface CatalogItem {
  id: string;
  title: string;
}

const ITEMS: readonly CatalogItem[] = [
  { id: "1", title: "Чайник" },
  { id: "2", title: "Блокнот" },
  { id: "3", title: "Кружка" },
];

export async function getCatalog(): Promise<CatalogItem[]> {
  return ITEMS.map((item) => ({ ...item }));
}
