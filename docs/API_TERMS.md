# 外部数据条款

本仓库可以引用 [spire-codex](https://spire-codex.com) / `ptrlrd/spire-codex` 作为公开数值摘要的来源（卡牌 ID、费用、稀有度、HP 区间等）。

约束：

1. `tools/dump_catalog.py` 必须在文件头注明来源与本条款。
2. CI **禁止**访问 live Codex API。Catalog 测试只读 `catalog/ci/synthetic/`。
3. 允许提交：`entity_ids.json`、公开数值摘要、我们手写的招式机数值表。
4. 不允许提交：整站镜像、从 `sts2.dll` 反编译抄录的 C# 源、PCK / 贴图 / 音频。
5. Adapter 构建通过环境变量 `STS2_DLL` 指向本机游戏安装目录，不把游戏二进制放进仓库。

这些规则由 `tools/legal_scan.py` 与 `.github/workflows/legal.yml` 强制执行。
