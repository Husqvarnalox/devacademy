import logging
import subprocess
import sys


def run_python(code, timeout=5):
    """Запустить [sys.executable, '-c', code]; вернуть (returncode, stdout, stderr)."""
    # TODO
    return None


def run_checked(code, timeout=5):
    """stdout.strip(); при ненулевом коде - RuntimeError с кодом и stderr."""
    # TODO
    return None


def run_with_timeout(code, timeout):
    """stdout.strip() или строка 'timeout', если процесс не уложился."""
    # TODO
    return None


def safe_echo(arg):
    """Дочерний Python печатает sys.argv[1]; аргумент передаётся списком."""
    # TODO
    return None


def make_logger(name, handler, level=logging.INFO):
    """Логгер name с уровнем level, handler (без дублей) и propagate=False."""
    # TODO
    return None


def log_operation(logger, user, action, ok):
    """INFO 'user=%s action=%s' или ERROR 'user=%s action=%s failed' (лениво)."""
    # TODO


def safe_divide(logger, a, b):
    """a / b; при ZeroDivisionError - logger.exception и None."""
    # TODO
    return None
