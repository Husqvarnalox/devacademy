import { describe, it, expect } from "vitest";
import express from "express";
import inject from "light-my-request";
import { validate, validateBody } from "../src/validate.mjs";

const schema = {
  name: { type: "string", required: true, min: 2, max: 10 },
  age: { type: "integer", min: 0, max: 150 },
  role: { type: "string", enum: ["admin", "user"] },
  email: { type: "string", pattern: /^[^@\s]+@[^@\s]+$/ },
  tags: { type: "array", max: 3, items: { type: "string", min: 1, max: 5 } },
};

const brief = (errors) => errors.map((e) => `${e.field}:${e.code}`);

describe("validate", () => {
  it("корректное тело даёт пустой список", () => {
    expect(validate({ name: "Anna", age: 30, role: "user", email: "a@b.c", tags: ["x", "yy"] }, schema)).toEqual([]);
    expect(validate({ name: "Bo" }, schema)).toEqual([]);
  });

  it("required и type: отсутствующее поле и неверные типы", () => {
    expect(brief(validate({}, schema))).toEqual(["name:required"]);
    expect(brief(validate({ name: 5, age: "30", tags: "x" }, schema))).toEqual(["name:type", "age:type", "tags:type"]);
    expect(brief(validate({ name: "Anna", age: 1.5 }, schema))).toEqual(["age:type"]);
    expect(brief(validate({ name: "Anna", age: null }, schema))).toEqual(["age:type"]);
  });

  it("min, max, enum и pattern", () => {
    expect(brief(validate({ name: "A" }, schema))).toEqual(["name:min"]);
    expect(brief(validate({ name: "Abcdefghijk" }, schema))).toEqual(["name:max"]);
    expect(brief(validate({ name: "Anna", age: -1 }, schema))).toEqual(["age:min"]);
    expect(brief(validate({ name: "Anna", age: 151 }, schema))).toEqual(["age:max"]);
    expect(brief(validate({ name: "Anna", role: "root" }, schema))).toEqual(["role:enum"]);
    expect(brief(validate({ name: "Anna", email: "nope" }, schema))).toEqual(["email:pattern"]);
  });

  it("элементы массива проверяются с индексом в имени поля", () => {
    const errors = validate({ name: "Anna", tags: ["ok", 7, "", "toolong", "z"] }, schema);
    expect(brief(errors)).toEqual(["tags:max", "tags[1]:type", "tags[2]:min", "tags[3]:max"]);
  });

  it("возвращаются все ошибки сразу, сначала поля схемы, потом неизвестные", () => {
    const errors = validate({ extra: 1, role: "root", age: 200, other: true }, schema);
    expect(brief(errors)).toEqual(["name:required", "age:max", "role:enum", "extra:unknown_field", "other:unknown_field"]);
    for (const e of errors) expect(typeof e.message).toBe("string");
    for (const e of errors) expect(e.message.length).toBeGreaterThan(0);
  });

  it("тело не объект - одна ошибка invalid_body", () => {
    for (const body of [null, undefined, [], "text", 42]) {
      expect(validate(body, schema)).toEqual([expect.objectContaining({ field: "", code: "invalid_body" })]);
    }
  });

  it("опасные имена полей: __proto__ из JSON и ключи прототипа", () => {
    const parsed = JSON.parse('{"name":"Anna","__proto__":{"admin":true}}');
    expect(brief(validate(parsed, schema))).toEqual(["__proto__:unknown_field"]);
    expect(brief(validate({}, { constructor: { type: "string", required: true } }))).toEqual(["constructor:required"]);
    expect(brief(validate({ name: "Anna", toString: "x" }, schema))).toEqual(["toString:unknown_field"]);
  });
});

describe("validateBody", () => {
  function makeApp() {
    const app = express();
    app.use(express.json());
    app.post("/users", validateBody(schema), (req, res) => res.status(201).json({ saved: req.body.name }));
    return app;
  }
  const post = (payload) =>
    inject(makeApp(), { method: "POST", url: "/users", headers: { "content-type": "application/json" }, payload: JSON.stringify(payload) });

  it("ошибки - 422 в общем формате со списком", async () => {
    const res = await post({ age: "x", extra: 1 });
    expect(res.statusCode).toBe(422);
    const body = res.json();
    expect(body.error.code).toBe("validation_failed");
    expect(body.error.message).toBe("Request body is invalid");
    expect(brief(body.error.details.errors)).toEqual(["name:required", "age:type", "extra:unknown_field"]);
  });

  it("корректное тело доходит до обработчика", async () => {
    const res = await post({ name: "Anna" });
    expect(res.statusCode).toBe(201);
    expect(res.json()).toEqual({ saved: "Anna" });
  });

  it("запрос без разобранного тела - invalid_body", async () => {
    const res = await inject(makeApp(), { method: "POST", url: "/users" });
    expect(res.statusCode).toBe(422);
    expect(brief(res.json().error.details.errors)).toEqual([":invalid_body"]);
  });
});
