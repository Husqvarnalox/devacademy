<?php
declare(strict_types=1);

// Допишите перечисление Priority и классы Task и TaskList. Код ниже читает команды - менять его не нужно.

interface Describable
{
    public function describe(): string;
}

/**
 * Приоритет задачи: Low = 1 ("низкий"), Normal = 2 ("обычный"), High = 3 ("высокий").
 */
enum Priority: int
{
    case Low = 1;
    case Normal = 2;
    case High = 3;

    public function label(): string
    {
        return match ($this) {
            self::Low => "низкий",
            self::Normal => "обычный",
            self::High => "высокий",
        };
    }
}

/**
 * Задача: название и приоритет задаются при создании и больше не меняются (readonly);
 * флаг "выполнена" меняется методом complete().
 * describe(): "[ ] название (высокий)" или "[x] название (высокий)" для выполненной.
 */
final class Task implements Describable
{
    private bool $done = false;

    public function __construct(
        public readonly string $title,
        public readonly Priority $priority,
    ) {
    }

    public function complete(): void
    {
        $this->done = true;
    }

    public function isDone(): bool
    {
        return $this->done;
    }

    public function describe(): string
    {
        return ($this->done ? "[x] " : "[ ] ") . "{$this->title} ({$this->priority->label()})";
    }
}

/**
 * Список задач.
 */
final class TaskList
{
    /** @var list<Task> */
    private array $tasks = [];

    /** Добавляет задачу. */
    public function add(Task $task): void
    {
        $this->tasks[] = $task;
    }

    /** Находит задачу по точному названию или возвращает null. */
    public function find(string $title): ?Task
    {
        foreach ($this->tasks as $task) {
            if ($task->title === $title) {
                return $task;
            }
        }
        return null;
    }

    /**
     * Невыполненные задачи: сначала с высоким приоритетом; при равном приоритете - в порядке добавления.
     *
     * @return list<Task>
     */
    public function pending(): array
    {
        $pending = array_values(array_filter($this->tasks, fn(Task $task): bool => !$task->isDone()));
        usort($pending, fn(Task $a, Task $b): int => $b->priority->value <=> $a->priority->value);
        return $pending;
    }
}

function show(Describable $item): string
{
    return $item->describe();
}

$list = new TaskList();
while (($line = fgets(STDIN)) !== false) {
    $parts = explode(" ", trim($line), 3);
    if ($parts[0] === "add" && count($parts) === 3) {
        $priority = Priority::tryFrom((int) $parts[1]);
        if ($priority === null) {
            echo "неизвестный приоритет: {$parts[1]}\n";
            continue;
        }
        $task = new Task($parts[2], $priority);
        $list->add($task);
        echo "добавлено: ", show($task), "\n";
    } elseif ($parts[0] === "done" && count($parts) >= 2) {
        $title = implode(" ", array_slice($parts, 1));
        $task = $list->find($title);
        if ($task === null) {
            echo "нет задачи: {$title}\n";
            continue;
        }
        $task->complete();
        echo "готово: ", show($task), "\n";
    } elseif ($parts[0] === "list") {
        $pending = $list->pending();
        if ($pending === []) {
            echo "пусто\n";
        }
        foreach ($pending as $task) {
            echo show($task), "\n";
        }
    }
}
