## 摘要

<!-- 这个 PR 改了什么，对应设计文档里的哪一个 PR 编号。 -->

## 法律检查

- [ ] 未提交 `sts2.dll`、PCK、反编译源、游戏贴图或 `third_party/`
- [ ] 未使用仓库相对路径的 `HintPath` 指向 `sts2.dll`（本机请用环境变量 `STS2_DLL`）
- [ ] 未提交权重大文件（`checkpoints/**/*.pt`）；单文件不超过 8 MB
- [ ] 未把 `MegaCrit` 命名空间抄进模拟器源码

## 测试

- [ ] `python tools/legal_scan.py`
- [ ] `python tools/legal_scan.py --self-test`
- [ ] 其它：
