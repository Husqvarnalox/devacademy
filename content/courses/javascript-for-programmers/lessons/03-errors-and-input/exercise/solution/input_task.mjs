// Разбор текстового ввода: записи "имя,возраст", JSON и числа.

export class ValidationError extends Error {
  constructor(message, line = null) {
    super(message);
    this.name = "ValidationError";
    this.line = line;
  }
}

export function parseLine(line, lineNo) {
  const fields = line.split(",");
  if (fields.length !== 2) throw new ValidationError("expected 2 fields", lineNo);
  const name = fields[0].trim();
  if (name === "") throw new ValidationError("empty name", lineNo);
  const ageText = fields[1].trim();
  if (!/^\d+$/.test(ageText)) throw new ValidationError("age must be a whole number", lineNo);
  const age = Number(ageText);
  if (age > 150) throw new ValidationError("age out of range", lineNo);
  return { name, age };
}

export function parseRecords(text) {
  if (typeof text !== "string") throw new TypeError("text must be a string");
  const records = [];
  const errors = [];
  const lines = text.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].trim() === "") continue;
    try {
      records.push(parseLine(lines[i], i + 1));
    } catch (e) {
      if (!(e instanceof ValidationError)) throw e;
      errors.push({ line: e.line, message: e.message });
    }
  }
  return { records, errors };
}

export function parseJsonSafe(text, fallback) {
  if (typeof text !== "string") return fallback;
  try {
    return JSON.parse(text);
  } catch (e) {
    if (e instanceof SyntaxError) return fallback;
    throw e;
  }
}

export function parseNumbers(text) {
  const tokens = text.split(/\s+/).filter((t) => t !== "");
  return tokens.map((token) => {
    if (!/^[+-]?\d+(\.\d+)?$/.test(token)) throw new ValidationError(`not a number: ${token}`);
    return Number(token);
  });
}
