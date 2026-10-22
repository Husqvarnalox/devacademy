import { readFileSync } from "node:fs";

interface Entry {
  category: string;
  amount: number;
}

interface Total {
  category: string;
  sum: number;
}

// Разбор одной строки вида "категория;сумма".
// Для неверной строки выбросьте ошибку (throw new Error("...")).
function parseEntry(line: string): Entry {
  // TODO: split(";"), проверка числа частей, trim категории, Number суммы
  return { category: "", amount: 0 };
}

// Добавляет сумму к итогу категории; если категории ещё нет, создаёт её.
function addToTotals(totals: Total[], category: string, amount: number): void {
  // TODO: totals.find(...), ветки для undefined и для найденного итога
}

const text: string = readFileSync(0, "utf8");
const lines: string[] = text.trim().split("\n");

// TODO: цикл по строкам: trim, пропуск пустых, try { parseEntry ... } catch { считаем неверную }
// Затем вывод: категории по убыванию суммы ("еда: 350.00"), потом
// "Итого: ...", "Записей: ...", "Неверных строк: ...".
