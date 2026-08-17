# Helichrysum 完整开发计划（整合版）

> 整合已完成的切片与待实现的功能项，形成一份完整的项目追踪文档。
> 状态：✅ 已完成 / 🔄 进行中 / ⏳ 待开始

---

## 第一部分：已完成（11 项）

| # | 名称 | 状态 | 说明 | 关联 F-xxx |
|---|---|---|---|---|
| 0 | 项目骨架 | ✅ | .sln/Core/Filesystem/Cli/测试/CI/fixture/Directory.Build.props | — |
| 1 | 扫描→manifest→重复→报告 | ✅ | Scope/Scanner/Hash/Manifest/ExactDuplicate/Report/CLI 端到端 | F-Scope, F-Scan, F-Layered, F-Report |
| 2 | Link 完整处理 | ✅ | ILinkInspector/LinkResolver/InScope/OutOfScope/Broken/Circular | F-Link-1~6 |
| 3 | 分层 Hash | ✅ | SampledHash/HashTier 升级策略 | F-Layered-1~7 |
| 4 | 关系分析扩展 | ✅ | StructuralSibling/MovedRenamed/Versioned | F-Relation-1~8 |
| 5 | ArchivePair 压缩包配对 | ✅ | zip/7z/tar/rar FullyExtracted/ModifiedAfterExtraction | F-Archive-1~7 |
| 6 | Plan+Executor | ✅ | ProcessingPlan/PlanGenerator/Executor/Trash 两阶段 | F-Plan-1~7, F-Exec-1~6 |
| 7 | Beta CLI 命令补全 | ✅ | plan-list/show/dry-run/exec/--version/9 个 CLI 命令 | F-Form-1 |
| 8 | 决策模型 Resolve | ✅ | Equality/Compatibility/Conflict 三态 + 目录级兼容 | F-Resolve-1~6 |
| 9 | 执行安全 Exec Safety | ✅ | TOCTOU/Staging/完整性回滚 | F-Exec-7~12 |
| 10 | 压缩包格式扩展 | ✅ | 7z/tar/rar SharpCompress 集成 | F-Archive-1 |

> 当前测试：71 全绿 · 核心代码 3,370 行 · 33 个类 · 18 个测试文件

---

## 第二部分：待实现（24 项）

### 高优（7 项）

| # | 名称 | 状态 | 说明 | 关联 F-xxx | 工作量 |
|---|---|---|---|---|---|
| v1 | verify 命令 | ✅ 已完成 | 归档完整性重新 hash 对比 manifest | F-Exec-5 | 0.5 天 |
| v2 | CI 实跑 | ⏳ | GitHub Actions 验证三平台构建+测试绿 | — | 0.5 天 |
| v3 | 报告目录树渲染 | ⏳ | F-Report-1 HTML 目录树可展开 | F-Report-1 | 1-2 天 |
| v4 | 快照年龄展示 | ⏳ | F-Report-12 报告头部显示创建时间 | F-Report-12 | 0.5 天 |
| v5 | CONFIG 配置化 | ⏳ | 保底策略/报告阈值/分析深度做成配置读取 | 配置哲学 | 1 天 |
| v6 | CLI 端到端集成测试 | ⏳ | Scan→Analyze→Plan→Exec 全流程自动化测试 | — | 1 天 |
| v7 | AOT 单文件发布验证 | ⏳ | dotnet publish NativeAOT | — | 0.5-1 天 |

### 中优（14 项）

| # | 名称 | 状态 | 说明 | 关联 F-xxx | 工作量 |
|---|---|---|---|---|---|
| v8 | NearDuplicate | ⏳ | 文本 EOL/BOM 归一化比较 | F-Relation-2 | 1 天 |
| v9 | 报告筛选+Diff | ⏳ | 按问题类型/路径/size/mtime 筛选 + Diff 视图 | F-Report-4/5/7 | 2 天 |
| v10 | 报告超限截断 | ⏳ | 大报告详情截断保留摘要 | F-Report-6c | 1 天 |
| v11 | SQLite 导出 | ⏳ | 报告可导出 SQLite 格式 | F-Report-10 | 1 天 |
| v12 | 保底三策略 | ⏳ | 双保险/仅回收站/仅Staging 可配 | F-Exec-9 | 1 天 |
| v13 | 新 manifest 生成 | ⏳ | 执行完成后生成新 manifest 反映归档状态 | F-Exec-5 | 1 天 |
| v14 | 中断恢复 | ⏳ | 执行中断后可续跑 | F-Exec-4 | 1 天 |
| v15 | 回滚信息 | ⏳ | 每个动作的逆操作信息 | F-Plan-7 | 1 天 |
| v16 | 一致性投票 | ⏳ | 多数派检测孤立者标 Integrity_Suspected | F-Resolve-18 | 1 天 |
| v17 | 时间可信度检验 | ⏳ | ctime 聚集检测拷贝痕迹降权 | F-Resolve-4a | 1 天 |
| v18 | 压缩包锚点 | ⏳ | 包内时间戳作为解压锚点证据链 | F-Resolve-16 | 1 天 |
| v19 | 同层仲裁 | ⏳ | 意图冲突优先级裁决（Moved>StructuralSibling>ArchivePair>Duplicate） | F-Resolve-13 | 1 天 |
| v20 | 自动项可见可否决 | ⏳ | 自动处理项在报告中可查可被否决 | F-Resolve-7 | 1 天 |
| v21 | 依赖链可视化 | ⏳ | 报告标注处理链层级依赖关系 | F-Resolve-15 | 1 天 |

### 低优（3 项 + 2 UI）

| # | 名称 | 状态 | 说明 | 关联 F-xxx | 前提 |
|---|---|---|---|---|---|
| v22 | Hardlink inode 检测 | ⏳ | P/Invoke 跨平台 | F-Link-3 | 有真实 hardlink 需求 |
| v23 | Mount Point 边界 | ⏳ | Linux 挂载点检测不跨设备 | F-Link-4 | 非阻塞 |
| v24 | 交互式确认流 | ⏳ | 交互式确认替换 --confirm 参数 | F-Exec-1 | WebUI 阶段 |
| UI-7 | WebUI（切片 7） | ⏳ | 操作界面+报告界面 | F-Preview, F-Form | 你单独线 |
| UI-8 | WPF 桌面壳（切片 8） | ⏳ | Windows 优先 | F-Form | 等 WebUI 定稿 |

---

## 第三部分：依赖关系图

```
v2 CI 实跑 ─── 独立（随时可做）
     │
v1 verify ─── v6 CLI 集成测试 ─── v7 AOT 发布
     │
v3 报告目录树 ─── v4 快照年龄 ─── v9 筛选+Diff ─── v10 超限截断 ─── v11 SQLite 导出
     │
v5 CONFIG 配置化 ─── v12 保底三策略 ─── v13 新 manifest ─── v14 中断恢复
     │
v8 NearDuplicate ─── v16 一致性投票 ─── v17 时间可信度 ─── v18 压缩包锚点
     │
v19 同层仲裁 ─── v20 自动项可见 ─── v21 依赖链可视化 ─── v15 回滚信息
     │
v22 Hardlink ─── v23 Mount Point ─── v24 交互式确认
     │
UI-7 WebUI ─── UI-8 WPF（你单独线）
```

---

## 第四部分：F-xxx 模块覆盖进度

| 模块 | 总数 | 已完成 | 待完成 | 完成率 |
|---|---|---|---|---|
| F-Scope | 7 | 7 | 0 | 100% |
| F-Scan | 11 | 10 | 1（v6） | 91% |
| F-Link | 6 | 5 | 1（v22/v23） | 83% |
| F-Layered | 7 | 7 | 0 | 100% |
| F-Relation | 8 | 7 | 1（v8） | 88% |
| F-Resolve | 17 | 6 | 11（v16-v21 等） | 35% |
| F-Archive | 7 | 7 | 0 | 100% |
| F-Report | 12 | 3 | 9（v3/v4/v9/v10/v11 等） | 25% |
| F-Preview | 10 | 0 | 10 | 0%（依赖 UI） |
| F-Plan | 7 | 5 | 2（v14/v15） | 71% |
| F-Exec | 12 | 8 | 4（v12/v13/v14/v24） | 67% |
| F-Form | 6 | 1 | 5 | 17%（依赖 UI） |

---

## 第五部分：里程碑

| 里程碑 | 条件 | 预计 |
|---|---|---|
| Beta CLI 可用 | v1-v7 完成（71→78 测试） | ~1 周 |
| 报告完善 | v3/v4/v9/v10/v11 完成（78→85 测试） | ~2 周 |
| 决策模型完整 | v16-v21 完成（85→95 测试） | ~2 周 |
| 安全层完整 | v12/v13/v14 完成（95→100 测试） | ~1 周 |
| v1.0 Release | 全部非 UI 完成 + UI 线合流 | 待定 |

---

> 本文档由 `docs/implementation-plan.md` + 后续缺口分析整合而成，
> 覆盖从切片 0 到 v24 的完整开发路径。
> 最后更新：2026-08-17