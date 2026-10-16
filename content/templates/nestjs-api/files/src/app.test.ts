import "reflect-metadata";
import assert from "node:assert/strict";
import type { AddressInfo } from "node:net";
import { after, before, describe, it } from "node:test";
import type { INestApplication } from "@nestjs/common";
import { Test } from "@nestjs/testing";
import { AppModule } from "./app.module.js";

describe("API", () => {
  let app: INestApplication;
  let base: string;

  before(async () => {
    const moduleRef = await Test.createTestingModule({ imports: [AppModule] }).compile();
    app = moduleRef.createNestApplication();
    await app.listen(0);
    base = `http://127.0.0.1:${(app.getHttpServer().address() as AddressInfo).port}`;
  });

  after(async () => {
    await app.close();
  });

  it("POST /todos создаёт задачу, GET /todos её возвращает", async () => {
    const created = await fetch(`${base}/todos`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ title: "  купить молоко " }),
    });
    assert.equal(created.status, 201);
    assert.deepEqual(await created.json(), { id: 1, title: "купить молоко", done: false });

    const list = await fetch(`${base}/todos`);
    assert.equal(((await list.json()) as unknown[]).length, 1);
  });

  it("POST /todos без title даёт 400", async () => {
    const res = await fetch(`${base}/todos`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({}),
    });
    assert.equal(res.status, 400);
  });
});
