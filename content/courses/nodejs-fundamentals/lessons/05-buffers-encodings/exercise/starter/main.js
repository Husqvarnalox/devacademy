const { text } = require("node:stream/consumers");

// Разбирает одно сообщение: строку из шестнадцатеричных цифр.
// Возвращает { lines, frames }: строки вывода и число полностью разобранных кадров.
function decodeFrames(hex) {
  // TODO: превратите hex в Buffer (проверьте, что в строке чётное число hex-цифр),
  // затем в цикле читайте кадры: тип (1 байт), длина (2 байта, big-endian), данные.
  return { lines: [], frames: 0 };
}

async function main() {
  const input = await text(process.stdin);
  const hexLines = input.split("\n").map((l) => l.trim()).filter((l) => l !== "");
  hexLines.forEach((hex, i) => {
    const { lines, frames } = decodeFrames(hex);
    console.log(`message ${i + 1}`);
    for (const line of lines) console.log(line);
    console.log(`frames=${frames}`);
  });
}

main();
