from fastapi import FastAPI, HTTPException

app = FastAPI()

JOBS: dict[int, dict] = {}
_next_id = 1


def reset_state() -> None:
    """Очистить хранилище и начать нумерацию заново с 1."""
    raise NotImplementedError


@app.get("/health")
def health():
    return {"status": "ok"}

# TODO: GET /hello/{name}
# TODO: POST /jobs (201)
# TODO: GET /jobs/{job_id} (404 при отсутствии)
# TODO: DELETE /jobs/{job_id} (204 без тела, 404 при отсутствии)
