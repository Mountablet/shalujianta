#!/usr/bin/env python3
"""Legal / proprietary-binary scanner for shalujianta (PR-000).

Scans tracked files (or a given --root) and fails if game binaries,
decompiled trees, oversized blobs, or in-repo sts2.dll references appear.

Usage:
    python tools/legal_scan.py
    python tools/legal_scan.py --root .
    python tools/legal_scan.py --self-test
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import tempfile
from pathlib import Path

MAX_FILE_BYTES = 8 * 1024 * 1024

# Path rules apply to posix-normalized relative paths.
BANNED_PATH_PATTERNS: list[tuple[str, re.Pattern[str]]] = [
    ("sts2.dll", re.compile(r"(^|/)sts2\.dll$", re.IGNORECASE)),
    ("decompiled/", re.compile(r"(^|/)decompiled(/|$)")),
    ("third_party/", re.compile(r"(^|/)third_party(/|$)")),
    ("*.pck", re.compile(r"\.pck$", re.IGNORECASE)),
    ("MegaCrit path", re.compile(r"(^|/)MegaCrit(/|$)", re.IGNORECASE)),
]

CODE_SUFFIXES = {
    ".cs",
    ".csproj",
    ".fsproj",
    ".vbproj",
    ".props",
    ".targets",
    ".sln",
    ".json",
    ".xml",
}

# Content scan is limited to source / project files so design docs may
# discuss MegaCrit and sts2.dll without failing CI.
BANNED_CONTENT: list[tuple[str, re.Pattern[str]]] = [
    ("MegaCrit namespace", re.compile(r"\bMegaCrit\b")),
    ("repo-relative sts2 HintPath", re.compile(r"<HintPath>\s*(?!\$\()[^<]*sts2\.dll", re.IGNORECASE)),
    ("sts2 PackageReference", re.compile(r'<PackageReference[^>]*Include\s*=\s*"sts2"', re.IGNORECASE)),
    ("sts2 Reference Include", re.compile(r'<Reference[^>]*Include\s*=\s*"sts2"', re.IGNORECASE)),
]


def posix_rel(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def iter_scan_files(root: Path) -> list[Path]:
    """Prefer git-tracked files so local gitignored game copies are ignored."""
    git_dir = root / ".git"
    if git_dir.exists():
        import subprocess

        result = subprocess.run(
            ["git", "-C", str(root), "ls-files", "-z"],
            check=True,
            capture_output=True,
        )
        files: list[Path] = []
        for raw in result.stdout.split(b"\0"):
            if not raw:
                continue
            rel = raw.decode("utf-8", errors="surrogateescape")
            candidate = root / rel
            if candidate.is_file():
                files.append(candidate)
        return files

    skip_dirs = {".git", ".venv", "venv", "bin", "obj", "__pycache__"}
    files = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in skip_dirs]
        for name in filenames:
            files.append(Path(dirpath) / name)
    return files


def scan(root: Path) -> list[str]:
    violations: list[str] = []
    for path in iter_scan_files(root):
        rel = posix_rel(path, root)
        for label, pattern in BANNED_PATH_PATTERNS:
            if pattern.search(rel):
                violations.append(f"banned path ({label}): {rel}")

        try:
            size = path.stat().st_size
        except OSError as exc:
            violations.append(f"stat failed: {rel}: {exc}")
            continue
        if size > MAX_FILE_BYTES:
            violations.append(
                f"file larger than 8 MiB ({size} bytes): {rel}"
            )

        if path.suffix.lower() not in CODE_SUFFIXES:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for label, pattern in BANNED_CONTENT:
            if pattern.search(text):
                violations.append(f"banned content ({label}): {rel}")
    return violations


def write(path: Path, content: str | bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(content, bytes):
        path.write_bytes(content)
    else:
        path.write_text(content, encoding="utf-8")


def self_test() -> int:
    """Plant violations in a temp tree and require the scanner to catch all of them."""
    expected_substrings = [
        "banned path (sts2.dll)",
        "banned path (decompiled/)",
        "banned path (third_party/)",
        "banned path (*.pck)",
        "banned path (MegaCrit path)",
        "banned content (MegaCrit namespace)",
        "banned content (repo-relative sts2 HintPath)",
        "banned content (sts2 PackageReference)",
        "file larger than 8 MiB",
    ]
    with tempfile.TemporaryDirectory(prefix="shalujianta-legal-") as tmp:
        root = Path(tmp)
        write(root / "sts2.dll", b"not a real dll")
        write(root / "src" / "decompiled" / "Foo.cs", "class Foo {}\n")
        write(root / "third_party" / "note.txt", "local only\n")
        write(root / "mod.pck", b"pck")
        write(root / "MegaCrit" / "leak.txt", "nope\n")
        write(
            root / "src" / "Leak.cs",
            "using MegaCrit.Sts2.Core;\nnamespace Demo;\n",
        )
        write(
            root / "src" / "Bad.csproj",
            """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Reference Include="sts2">
      <HintPath>..\\third_party\\sts2.dll</HintPath>
    </Reference>
    <PackageReference Include="sts2" Version="1.0.0" />
  </ItemGroup>
</Project>
""",
        )
        write(root / "huge.bin", b"\0" * (MAX_FILE_BYTES + 1))

        found = scan(root)
        joined = "\n".join(found)
        missing = [item for item in expected_substrings if item not in joined]
        if missing:
            print("SELF-TEST FAILED: scanner missed planted violations:", file=sys.stderr)
            for item in missing:
                print(f"  missing: {item}", file=sys.stderr)
            print("scanner output:", file=sys.stderr)
            print(joined or "(empty)", file=sys.stderr)
            return 1

        print("self-test passed: scanner detected all planted fixtures")
        for line in found:
            print(f"  caught: {line}")
        return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=None, help="directory to scan")
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="prove the scanner fails on planted illegal fixtures",
    )
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()

    root = (args.root or Path.cwd()).resolve()
    violations = scan(root)
    if violations:
        print("LEGAL SCAN FAILED", file=sys.stderr)
        for item in violations:
            print(f"  {item}", file=sys.stderr)
        print(
            "\nDo not commit sts2.dll, decompiled sources, PCK assets, "
            "or repo-relative HintPath references. "
            "Use STS2_DLL for local Adapter builds.",
            file=sys.stderr,
        )
        return 1
    print(f"legal scan clean: {root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
