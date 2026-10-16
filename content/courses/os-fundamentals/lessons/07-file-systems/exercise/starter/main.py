import sys

MAX_SYMLINKS = 8


class FsError(Exception):
    """Raised with a reason like "file exists" and the path that caused it; the message is
    printed as "<cmd>: <reason>: <path>"."""

    def __init__(self, reason, path):
        self.reason = reason
        self.path = path


class Fs:
    def __init__(self):
        # inode number -> {"type": "dir"|"file"|"symlink", "nlink": int, "entries": {...} | "target": str}
        self.inodes = {1: {"type": "dir", "nlink": 2, "entries": {}}}

    # TODO: alloc (smallest free inode number >= 2), lookup(path, follow_last) with symlink expansion
    # (at most MAX_SYMLINKS expansions), mkdir, touch, link, symlink, rm, rmdir, ls.


def execute(fs, words):
    """Run one command, return the list of output lines."""
    cmd = words[0]
    args = words[1:]
    try:
        # TODO: dispatch mkdir, touch, ln, ln -s, rm, rmdir, ls, df; unknown -> "error: unknown command: <cmd>"
        pass
    except FsError as e:
        return [f"{cmd}: {e.reason}: {e.path}"]
    return []


def main():
    fs = Fs()
    out = []
    for line in sys.stdin.read().split("\n"):
        words = line.split()
        if words:
            out += execute(fs, words)
    print("\n".join(out))


main()
