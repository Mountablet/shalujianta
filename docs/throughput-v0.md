# v0 吞吐报告(PR-008)

设计 4.1 的 Phase-1 DoD:「吞吐尽力达到 1e4 step/s;未达标只写报告,不挡 Phase 1 / 官方 v0」。本报告即该条目。

## 测量

- 命令:`PYTHONPATH=python python -m shalujianta.tools.random_rollout --slice v0 --n 10000`
- 环境:Windows 10 x64,.NET 9.0.317,Python 3.12,单进程桥接(bindings 子进程)
- 日期:2026-11-01

| 模式 | 步数 | 对局数 | 用时 | 吞吐 | 胜/负/stall |
| --- | --- | --- | --- | --- | --- |
| NOSL | 10000 | 705 | 1.72 s | ≈5.8k steps/s | 703/1/0 |
| SL | 10000 | 683 | 1.72 s | ≈5.8k steps/s | 682/0/0 |

## 结论

- **正确性门禁全过**:1 万步随机合法 rollout 双模式零崩溃、零非法动作、零 mask 违规。
- **吞吐 5.8k < 1e4**:瓶颈在 JSON 序列化 + stdio 管道往返(每步两次消息),不在模拟器内核。
- **不挡 v0**:v0 的 Heuristic40 是浅搜索,评估套件(`v0_first_fight` 100 种子)在这个速率下秒级完成。
- **post-v0 路线**(设计 5.9 / KD-20):训练热路径走 `Shalujianta.Bindings` 的进程内 C ABI 扁平缓冲,目标 1e5+ steps/s;本 JSONL 桥接退居调试与评估。

## 备注

- `run_instant` 级数据无意义——启动一次桥接进程约 0.3s,长跑摊薄后吞吐稳定在 5.8k。
- 若 post-v0 前需要临时提速:批量 step 命令(一次请求跑 K 步)可把管道开销摊薄一个数量级,无需动 ABI。
