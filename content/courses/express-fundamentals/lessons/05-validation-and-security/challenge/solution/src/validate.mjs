// Проверка тела запроса по схеме. Возвращает список ошибок, а не первую найденную.

const isPlainObject = (v) => v !== null && typeof v === "object" && !Array.isArray(v);

function typeOk(type, value) {
  switch (type) {
    case "string": return typeof value === "string";
    case "integer": return Number.isInteger(value);
    case "number": return typeof value === "number" && Number.isFinite(value);
    case "boolean": return typeof value === "boolean";
    case "array": return Array.isArray(value);
    default: return false;
  }
}

function sizeOf(type, value) {
  return type === "string" || type === "array" ? value.length : value;
}

function checkValue(field, rule, value, errors) {
  if (!typeOk(rule.type, value)) {
    errors.push({ field, code: "type", message: `${field} must be of type ${rule.type}` });
    return;
  }
  const size = sizeOf(rule.type, value);
  if (rule.min !== undefined && size < rule.min) errors.push({ field, code: "min", message: `${field} must be at least ${rule.min}` });
  if (rule.max !== undefined && size > rule.max) errors.push({ field, code: "max", message: `${field} must be at most ${rule.max}` });
  if (rule.enum !== undefined && !rule.enum.includes(value)) errors.push({ field, code: "enum", message: `${field} must be one of: ${rule.enum.join(", ")}` });
  if (rule.pattern !== undefined && rule.type === "string" && !rule.pattern.test(value)) errors.push({ field, code: "pattern", message: `${field} has an invalid format` });
  if (rule.type === "array" && rule.items !== undefined) {
    value.forEach((item, index) => checkValue(`${field}[${index}]`, rule.items, item, errors));
  }
}

export function validate(body, schema) {
  if (!isPlainObject(body)) {
    return [{ field: "", code: "invalid_body", message: "Body must be a JSON object" }];
  }
  const errors = [];
  for (const [field, rule] of Object.entries(schema)) {
    const value = Object.hasOwn(body, field) ? body[field] : undefined;
    if (value === undefined) {
      if (rule.required) errors.push({ field, code: "required", message: `${field} is required` });
      continue;
    }
    checkValue(field, rule, value, errors);
  }
  for (const key of Object.keys(body)) {
    if (!Object.hasOwn(schema, key)) errors.push({ field: key, code: "unknown_field", message: `${key} is not allowed` });
  }
  return errors;
}

export function validateBody(schema) {
  return (req, res, next) => {
    const errors = validate(req.body, schema);
    if (errors.length > 0) {
      return res.status(422).json({
        error: { code: "validation_failed", message: "Request body is invalid", details: { errors } },
      });
    }
    next();
  };
}
