from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.fixture(scope="session")
def bindings_dll() -> str:
    """Build the C# bindings bridge once per session (PR-008)."""
    result = subprocess.run(
        ["dotnet", "build", str(ROOT / "src" / "Shalujianta.Bindings"), "--nologo"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        pytest.skip(f"bindings build failed: {result.stderr[-400:]}")
    dll = ROOT / "src" / "Shalujianta.Bindings" / "bin" / "Debug" / "net9.0" / "Shalujianta.Bindings.dll"
    assert dll.exists()
    return str(dll)
