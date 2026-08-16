# shalujianta

Slay the Spire 2 的打牌 AI：无头模拟器 + 局内策略 + 局外搜索。

设计文档：[docs/STS2-AI-系统设计计划.md](docs/STS2-AI-系统设计计划.md)

## 当前进度

官方 v0 = PR-000–PR-012。

- **PR-000**：仓库骨架与法律扫描
- **PR-001**：EntityId 命名空间 + `tools/dump_catalog.py`（默认只读合成目录）

已锁定：

- 第一角色：**Ironclad**
- 模拟器语言：**C# / .NET 9**
- 训练编排：Python 3.12 + PyTorch（后续 PR）
- 真实游戏客户端 **不是** 训练环境

## 法律红线

不要提交：

- `sts2.dll`、Godot `.pck`、游戏 `.run` 存档
- 反编译源（`decompiled/`、`MegaCrit` 命名空间）
- `third_party/` 下的游戏文件
- 超过 8 MB 的单文件 / 训练权重（`checkpoints/**/*.pt`）

本机接游戏时，把 dll 路径放到环境变量 `STS2_DLL`，不要写进仓库相对 `HintPath`。

```powershell
python tools/legal_scan.py
python tools/legal_scan.py --self-test
python -m pip install -r requirements-dev.txt
python tools/dump_catalog.py
python -m pytest python/tests/test_catalog.py
```

GitHub Actions 工作流源文件在 `tools/ci/legal.yml`。要启用线上 CI，需要给 `gh` 补 `workflow` 权限后再把该文件复制到 `.github/workflows/legal.yml`。

## 仓库布局

```
docs/       设计文档
catalog/    实体目录（CI 只用 catalog/ci/synthetic/）
proto/      Observation / Action schema（后续 PR）
src/        C# 模拟器 / Adapter / Bindings
python/     训练、搜索、评估
tools/      扫描与后续 dump / parity 脚本
traces/     本机录音，不提交 traces/real/
```

## 许可

源码为 MIT（见 [LICENSE](LICENSE)）。游戏本体与 `sts2.dll` 仍归 Mega Crit 所有，本仓库不重新分发。
