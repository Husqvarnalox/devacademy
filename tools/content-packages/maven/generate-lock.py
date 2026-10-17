#!/usr/bin/env python3
"""Developer-only: regenerate tools/content-packages/maven/<set>/lock.json from <set>/pom.xml.

The app never runs Maven. It downloads exactly the jars listed in lock.json from Maven Central and verifies each sha256, so this script is the only
place that resolves versions. It needs Apache Maven (`mvn` on PATH, or MVN=/path/to/mvn) and the network (Maven Central).

    python3 tools/content-packages/maven/generate-lock.py spring-boot-web-test

Review the resulting diff of lock.json like any other dependency bump. Only jars without a classifier of the runtime scope are accepted.
"""
import hashlib
import json
import os
import subprocess
import sys
import tempfile

SOURCE = "https://repo1.maven.org/maven2"


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    here = os.path.dirname(os.path.abspath(__file__))
    set_id = sys.argv[1]
    pom = os.path.join(here, set_id, "pom.xml")
    if not os.path.isfile(pom):
        print(f"error: {pom} not found")
        return 2
    mvn = os.environ.get("MVN", "mvn")
    with tempfile.TemporaryDirectory() as tmp:
        listing = os.path.join(tmp, "list.txt")
        cmd = [mvn, "-B", "-q", "-f", pom, "dependency:list", "-DincludeScope=runtime", f"-DoutputFile={listing}",
               "-DoutputAbsoluteArtifactFilename=true", "-DappendOutput=false", "-Dsort=true"]
        subprocess.run(cmd, check=True)
        jars = []
        for raw in open(listing, encoding="utf-8"):
            line = raw.strip().split(" -- ")[0]  # "...jar -- module x (auto)"
            if not line or line.endswith(":") or ":" not in line or line.startswith("The following"):
                continue
            parts = line.split(":")
            if len(parts) != 6 or parts[2] != "jar":
                sys.exit(f"error: unsupported dependency line (classifier or non-jar): {line}")
            group, artifact, _, version, _scope, path = parts
            file_name = f"{artifact}-{version}.jar"
            if os.path.basename(path) != file_name:
                sys.exit(f"error: unexpected file name {os.path.basename(path)} for {group}:{artifact}:{version}")
            data = open(path, "rb").read()
            jars.append({"groupId": group, "artifactId": artifact, "version": version, "file": file_name,
                         "sha256": hashlib.sha256(data).hexdigest(), "size": len(data)})
    jars.sort(key=lambda j: (j["groupId"], j["artifactId"], j["version"]))
    lock = {"set": set_id, "source": SOURCE, "jars": jars}
    out = os.path.join(here, set_id, "lock.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(lock, f, indent=2)
        f.write("\n")
    total = sum(j["size"] for j in jars)
    print(f"{out}: {len(jars)} jars, {total} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
