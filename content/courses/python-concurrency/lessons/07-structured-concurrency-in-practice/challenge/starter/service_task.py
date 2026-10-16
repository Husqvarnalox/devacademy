import asyncio

STOP = object()


async def serve(queue, handler, workers, stop):
    """Вернуть (processed, remaining) после graceful shutdown."""
    # TODO
    return None
