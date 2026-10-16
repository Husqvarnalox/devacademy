import { Injectable } from "@nestjs/common";

export interface Todo {
  id: number;
  title: string;
  done: boolean;
}

@Injectable()
export class TodosService {
  private readonly todos: Todo[] = [];
  private nextId = 1;

  list(): Todo[] {
    return this.todos;
  }

  create(title: string): Todo {
    const todo: Todo = { id: this.nextId++, title, done: false };
    this.todos.push(todo);
    return todo;
  }
}
