# shalujianta

Slay the Spire 2 的打牌 AI：无头模拟器 + 局内策略 + 局外搜索。

设计文档：[docs/STS2-AI-系统设计计划.md](docs/STS2-AI-系统设计计划.md)

## 当前进度

官方 v0 = PR-000–PR-012。

- **PR-000**：仓库骨架与法律扫描
- **PR-001**：EntityId 命名空间 + `tools/dump_catalog.py`（默认只读合成目录）
- **PR-002**：`ObservationV1` / `ActionV1` + NoSL 投影 + 256 槽展开表
- **PR-003**：钉死的 `System.Random` 兼容实现、RNG 流哈希表、`SimCheckpoint`
- **PR-004**：战斗内核与回合机（`CombatWorld`：BattleStart 不抽牌、每回合 TurnStart 抽 5、END_TURN 内敌方、手牌上限 10、0 费硬切、SL Clone/Checkpoint/双哈希）
- **PR-005**：SliceV0 内容（Ironclad 起始套、Burning Blood、Two-Tailed Rats、黄金测试 `ironclad_twotailedrats_seed42`；数值取自 wiki.gg，PR-014 parity 复核）
- **PR-006**：药水槽 + FirePotion / BlockPotion
- **PR-007**：256 槽 ActionMask 收尾（X-cost / HAND_SELECT 恒 0、mask↔Step 全槽一致性）
- **PR-008**：`Shalujianta.Bindings` JSONL 桥接 + Python `CombatEnv` / `NoSlSearchEnv` + `clone_for_search`（PIMC 确定化）+ 随机 rollout（[吞吐报告](docs/throughput-v0.md)）
- **PR-009**：starter-only `CombatGenerator` + `v0_act1_hallway` HP 结转（procedural 关闭到 PR-005b）
- **PR-010**：RewardSpec `combat_v0.1`（C#/Python 逐常数一致，反拖延/反倒药/反囤药/反过量格挡）
- **PR-011**：Heuristic40 + 1-ply 浅搜索（只经 `clone_for_search`，KD-16 结构保证）
- **PR-012**：固定种子评估套件 + replay viewer；`v0_act1_hallway`：启发 100% 胜/74.7% HP vs 随机 88%/35.7%（[结果](python/shalujianta/eval/results/)）

**官方 v0（PR-000–PR-012）至此全部落地。** 学习策略（BC/PPO）、Adapter、第二角色均为 post-v0。

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
dotnet build src/Shalujianta.Bindings          # Python env 需要
python -m pytest
dotnet test src/Shalujianta.Sim.Tests/Shalujianta.Sim.Tests.csproj
# 随机 rollout(Phase-1 DoD 冒烟)
$env:PYTHONPATH="python"; python -m shalujianta.tools.random_rollout --slice v0 --n 10000

# 评估套件(数字入库到 python/shalujianta/eval/results/)
$env:PYTHONPATH="python"; python -m shalujianta.eval.run_suite --suite v0_act1_hallway --policy heuristic --n 100 --out my_run.json

# 回放查看器:浏览器打开 tools/replay_viewer/index.html,加载 results/trace_*.jsonl
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
