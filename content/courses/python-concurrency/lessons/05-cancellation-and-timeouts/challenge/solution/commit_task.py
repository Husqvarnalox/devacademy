import asyncio


async def shielded_commit(commit, log):
    """Довести commit() до конца, даже если нас отменили."""
    inner = asyncio.ensure_future(commit())
    try:
        return await asyncio.shield(inner)
    except asyncio.CancelledError:
        await inner
        log.append("cancelled after commit")
        raise
