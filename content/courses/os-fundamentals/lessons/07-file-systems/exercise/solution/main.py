import sys

MAX_SYMLINKS = 8


class FsError(Exception):
    def __init__(self, reason, path):
        self.reason = reason
        self.path = path


class Fs:
    def __init__(self):
        # inode number -> {"type": "dir"|"file"|"symlink", "nlink": int, "entries": {...} | "target": str}
        self.inodes = {1: {"type": "dir", "nlink": 2, "entries": {}}}

    def alloc(self, node):
        ino = 2
        while ino in self.inodes:
            ino += 1
        self.inodes[ino] = node
        return ino

    def lookup(self, path, follow_last, shown=None):
        """Return (parent_ino, name, ino); ino is None when only the last component is missing."""
        shown = path if shown is None else shown
        parts = [p for p in path.split("/") if p]
        if not parts:
            return None, "/", 1
        cur = 1
        expansions = 0
        while parts:
            name = parts.pop(0)
            last = not parts
            if self.inodes[cur]["type"] != "dir":
                raise FsError("not a directory", shown)
            child = self.inodes[cur]["entries"].get(name)
            if child is None:
                if last:
                    return cur, name, None
                raise FsError("no such file or directory", shown)
            node = self.inodes[child]
            if node["type"] == "symlink" and (not last or follow_last):
                expansions += 1
                if expansions > MAX_SYMLINKS:
                    raise FsError("too many levels of symbolic links", shown)
                parts = [p for p in node["target"].split("/") if p] + parts
                cur = 1
                if not parts:
                    return None, "/", 1
                continue
            if last:
                return cur, name, child
            cur = child
        raise AssertionError

    def release(self, ino):
        node = self.inodes[ino]
        node["nlink"] -= 1
        if node["nlink"] == 0:
            del self.inodes[ino]

    def mkdir(self, path):
        parent, name, ino = self.lookup(path, False)
        if ino is not None:
            raise FsError("file exists", path)
        new = self.alloc({"type": "dir", "nlink": 2, "entries": {}})
        self.inodes[parent]["entries"][name] = new
        self.inodes[parent]["nlink"] += 1

    def touch(self, path):
        parent, name, ino = self.lookup(path, False)
        if ino is None:
            new = self.alloc({"type": "file", "nlink": 1})
            self.inodes[parent]["entries"][name] = new

    def link(self, target, link):
        _, _, ino = self.lookup(target, False)
        if ino is None:
            raise FsError("no such file or directory", target)
        if self.inodes[ino]["type"] == "dir":
            raise FsError("hard link not allowed for directory", target)
        parent, name, existing = self.lookup(link, False)
        if existing is not None:
            raise FsError("file exists", link)
        self.inodes[parent]["entries"][name] = ino
        self.inodes[ino]["nlink"] += 1

    def symlink(self, target, link):
        parent, name, existing = self.lookup(link, False)
        if existing is not None:
            raise FsError("file exists", link)
        new = self.alloc({"type": "symlink", "nlink": 1, "target": target})
        self.inodes[parent]["entries"][name] = new

    def rm(self, path):
        parent, name, ino = self.lookup(path, False)
        if ino is None:
            raise FsError("no such file or directory", path)
        if self.inodes[ino]["type"] == "dir":
            raise FsError("is a directory", path)
        del self.inodes[parent]["entries"][name]
        self.release(ino)

    def rmdir(self, path):
        parent, name, ino = self.lookup(path, False)
        if ino is None:
            raise FsError("no such file or directory", path)
        node = self.inodes[ino]
        if node["type"] != "dir":
            raise FsError("not a directory", path)
        if parent is None:
            raise FsError("device or resource busy", path)
        if node["entries"]:
            raise FsError("directory not empty", path)
        del self.inodes[parent]["entries"][name]
        del self.inodes[ino]
        self.inodes[parent]["nlink"] -= 1

    def describe(self, ino, name):
        node = self.inodes[ino]
        if node["type"] == "dir":
            label = name.rstrip("/") + "/" if name != "/" else "/"
        elif node["type"] == "symlink":
            label = f"{name} -> {node['target']}"
        else:
            label = name
        return f"{ino} {node['nlink']} {label}"

    def ls(self, path):
        _, _, ino = self.lookup(path, True)
        if ino is None:
            raise FsError("no such file or directory", path)
        node = self.inodes[ino]
        if node["type"] != "dir":
            return [self.describe(ino, path)]
        return [self.describe(child, name) for name, child in sorted(node["entries"].items())]


def execute(fs, words):
    cmd = words[0]
    args = words[1:]
    try:
        if cmd == "mkdir":
            fs.mkdir(args[0])
        elif cmd == "touch":
            fs.touch(args[0])
        elif cmd == "ln":
            if args[0] == "-s":
                fs.symlink(args[1], args[2])
            else:
                fs.link(args[0], args[1])
        elif cmd == "rm":
            fs.rm(args[0])
        elif cmd == "rmdir":
            fs.rmdir(args[0])
        elif cmd == "ls":
            return fs.ls(args[0])
        elif cmd == "df":
            return [f"inodes used: {len(fs.inodes)}"]
        else:
            return [f"error: unknown command: {cmd}"]
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
