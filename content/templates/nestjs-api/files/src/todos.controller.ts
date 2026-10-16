import { BadRequestException, Body, Controller, Get, Post } from "@nestjs/common";
import { TodosService } from "./todos.service.js";
import type { Todo } from "./todos.service.js";

@Controller("todos")
export class TodosController {
  constructor(private readonly todos: TodosService) {}

  @Get()
  list(): Todo[] {
    return this.todos.list();
  }

  @Post()
  create(@Body() body: { title?: unknown }): Todo {
    if (typeof body?.title !== "string" || body.title.trim() === "") {
      throw new BadRequestException("title is required");
    }
    return this.todos.create(body.title.trim());
  }
}
