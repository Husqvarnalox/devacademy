import asyncio


async def run_with_timeout(func, seconds):
    """('ok', результат) или ('timeout', None)."""
    # TODO
    return None


async def worker_with_cleanup(work, log):
    """acquire -> await work() -> release (всегда)."""
    # TODO
    return None


async def cancel_and_wait(task):
    """Отменить задачу, дождаться, вернуть True, если она завершилась отменённой."""
    # TODO
    return None


async def collect_errors(funcs):
    """{имя типа ошибки: количество} по упавшим задачам TaskGroup."""
    # TODO
    return None
