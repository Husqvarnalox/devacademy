import express from "express";
import type { Express } from "express";

export interface Todo {
  id: number;
  title: string;
  done: boolean;
}

export function createApp(): Express {
  const app = express();
  app.use(express.json());

  const todos: Todo[] = [];
  let nextId = 1;

  app.get("/health", (_req, res) => {
    res.json({ status: "ok" });
  });

  app.get("/todos", (_req, res) => {
    res.json(todos);
  });

  app.post("/todos", (req, res) => {
    const title: unknown = req.body?.title;
    if (typeof title !== "string" || title.trim() === "") {
      res.status(400).json({ error: "title is required" });
      return;
    }
    const todo: Todo = { id: nextId++, title: title.trim(), done: false };
    todos.push(todo);
    res.status(201).json(todo);
  });

  return app;
}
