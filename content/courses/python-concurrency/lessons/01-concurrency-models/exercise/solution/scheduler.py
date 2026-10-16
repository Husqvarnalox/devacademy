from collections import deque


def run(tasks):
    """Round-robin планировщик генераторов. Вернуть (trace, results)."""
    queue = deque(tasks.items())
    trace = []
    results = {}
    while queue:
        name, gen = queue.popleft()
        try:
            value = next(gen)
        except StopIteration as stop:
            results[name] = stop.value
        except Exception as exc:
            results[name] = exc
        else:
            trace.append((name, value))
            queue.append((name, gen))
    return trace, results


def recommend(cpu_bound, has_async_library):
    """'processes' | 'asyncio' | 'threads'."""
    if cpu_bound:
        return "processes"
    if has_async_library:
        return "asyncio"
    return "threads"
