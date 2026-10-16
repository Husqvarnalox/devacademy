import { text } from "node:stream/consumers";

const input = await text(process.stdin);

// TODO: сгруппируйте суммы по именам (Map), отсортируйте имена и выведите "имя: сумма"
