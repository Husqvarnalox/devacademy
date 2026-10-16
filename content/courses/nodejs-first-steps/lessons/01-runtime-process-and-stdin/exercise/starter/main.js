async function readAll() {
  // TODO: прочитайте весь stdin как строку (setEncoding + for await)
  return "";
}

async function main() {
  const text = await readAll();
  // TODO: посчитайте строки, слова и байты
  console.log("lines=" + 0);
  console.log("words=" + 0);
  console.log("bytes=" + 0);
}

main();
