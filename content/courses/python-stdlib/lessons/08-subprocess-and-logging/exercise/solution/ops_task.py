import logging
import subprocess
import sys


def _run(code, timeout, check=False):
    return subprocess.run(
        [sys.executable, "-c", code],
        capture_output=True,
        text=True,
        timeout=timeout,
        check=check,
    )


def run_python(code, timeout=5):
    r = _run(code, timeout)
    return r.returncode, r.stdout, r.stderr


def run_checked(code, timeout=5):
    try:
        return _run(code, timeout, check=True).stdout.strip()
    except subprocess.CalledProcessError as exc:
        raise RuntimeError(f"процесс завершился с кодом {exc.returncode}: {exc.stderr.strip()}") from exc


def run_with_timeout(code, timeout):
    try:
        return _run(code, timeout).stdout.strip()
    except subprocess.TimeoutExpired:
        return "timeout"


def safe_echo(arg):
    r = subprocess.run(
        [sys.executable, "-c", "import sys; print(sys.argv[1])", arg],
        capture_output=True,
        text=True,
        timeout=10,
        check=True,
    )
    return r.stdout[:-1] if r.stdout.endswith("\n") else r.stdout


def make_logger(name, handler, level=logging.INFO):
    logger = logging.getLogger(name)
    logger.setLevel(level)
    if handler not in logger.handlers:
        logger.addHandler(handler)
    logger.propagate = False
    return logger


def log_operation(logger, user, action, ok):
    if ok:
        logger.info("user=%s action=%s", user, action)
    else:
        logger.error("user=%s action=%s failed", user, action)


def safe_divide(logger, a, b):
    try:
        return a / b
    except ZeroDivisionError:
        logger.exception("division failed a=%s b=%s", a, b)
        return None
