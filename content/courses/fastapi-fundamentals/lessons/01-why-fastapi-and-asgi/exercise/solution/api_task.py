from fastapi import FastAPI, HTTPException

app = FastAPI()

JOBS: dict[int, dict] = {}
_next_id = 1


def reset_state() -> None:
    global _next_id
    JOBS.clear()
    _next_id = 1


@app.get("/health")
def health():
    return {"status": "ok"}


@app.get("/hello/{name}")
def hello(name: str):
    return {"message": f"Hello, {name}!"}


@app.post("/jobs", status_code=201)
def create_job():
    global _next_id
    job = {"id": _next_id, "status": "queued"}
    JOBS[_next_id] = job
    _next_id += 1
    return job


@app.get("/jobs/{job_id}")
def get_job(job_id: int):
    if job_id not in JOBS:
        raise HTTPException(status_code=404, detail="job not found")
    return JOBS[job_id]


@app.delete("/jobs/{job_id}", status_code=204)
def delete_job(job_id: int):
    if job_id not in JOBS:
        raise HTTPException(status_code=404, detail="job not found")
    del JOBS[job_id]
