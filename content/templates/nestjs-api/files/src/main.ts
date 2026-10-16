import "reflect-metadata";
import { NestFactory } from "@nestjs/core";
import { AppModule } from "./app.module.js";

const app = await NestFactory.create(AppModule);
const port = Number(process.env["PORT"] ?? 3000);
await app.listen(port);
console.log(`Server listening on http://localhost:${port}`);
