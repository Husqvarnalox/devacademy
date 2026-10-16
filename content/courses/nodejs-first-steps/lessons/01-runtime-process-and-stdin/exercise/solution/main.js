async function readAll() {
  process.stdin.setEncoding("utf8");
  let text = "";
  for await (const chunk of process.stdin) text += chunk;
  return text;
}

async function main() {
  const text = await readAll();
  const lines = (text.match(/\n/g) ?? []).length;
  const words = (text.match(/\S+/g) ?? []).length;
  const bytes = Buffer.byteLength(text, "utf8");
  console.log("lines=" + lines);
  console.log("words=" + words);
  console.log("bytes=" + bytes);
}

main();
