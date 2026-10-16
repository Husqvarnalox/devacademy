const { text } = require("node:stream/consumers");

function decodeFrames(hex) {
  if (!/^([0-9a-fA-F]{2})*$/.test(hex)) return { lines: ["invalid hex"], frames: 0 };
  const data = Buffer.from(hex, "hex");
  const lines = [];
  let frames = 0;
  let offset = 0;
  while (offset < data.length) {
    if (offset + 3 > data.length) {
      lines.push(`truncated at offset ${offset}`);
      break;
    }
    const type = data.readUInt8(offset);
    const length = data.readUInt16BE(offset + 1);
    const start = offset + 3;
    if (start + length > data.length) {
      lines.push(`truncated at offset ${offset}`);
      break;
    }
    const payload = data.subarray(start, start + length);
    frames++;
    switch (type) {
      case 0x01:
        lines.push(`text: ${payload.toString("utf8")}`);
        break;
      case 0x02: {
        if (length % 4 !== 0) {
          lines.push("bad u32 payload");
          break;
        }
        const values = [];
        for (let i = 0; i < length; i += 4) values.push(payload.readUInt32BE(i));
        lines.push(`u32: ${values.join(" ")}`);
        break;
      }
      case 0x03: {
        if (length % 2 !== 0) {
          lines.push("bad i16 payload");
          break;
        }
        const values = [];
        for (let i = 0; i < length; i += 2) values.push(payload.readInt16LE(i));
        lines.push(`i16le: ${values.join(" ")}`);
        break;
      }
      case 0x04:
        lines.push(`b64: ${payload.toString("base64")}`);
        break;
      case 0x05:
        lines.push(`latin1: ${payload.toString("latin1")}`);
        break;
      default:
        lines.push(`unknown type 0x${type.toString(16).padStart(2, "0")}`);
    }
    offset = start + length;
  }
  return { lines, frames };
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
