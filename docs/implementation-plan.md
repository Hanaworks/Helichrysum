# Helichrysum 实施计划（Implementation Plan）

| 项目 | 内容 |
|---|---|
| 文档状态 | Draft v0.1 |
| 最后更新 | 2026-08-13 |
| 关联文档 | [REQUIREMENTS.md](./requirements.md) · [TECHNICAL_DESIGN.md](./technical-design.md) |

> 本文档定义**如何一步步把 Helichrysum 写出来**。里程碑由垂直切片序列构成，每个切片的内部细节（功能清单 / TDD 红线 / 验收命令）构成实现步骤。已与需求文档和技术方案同步。

---

## 1. 实施策略

### 1.1 垂直切片（Walking Skeleton）

不按层水平铺开（先做完全部扫描再做完全部分析），而是**每条切片都是端到端可运行的最小单元**：

```text
切片 0 → 切片 1 → 切片 2 → ... → 切片 9
         │
         │ 每个切片都是：
         │   Scope 配置 → 扫描 → 分析 → 存储 → 输出/报告
         │   一条完整链路，可运行、可验收
```

每条切片在已有链路上**加深一层能力**，而不是重新造一条链。接口错误在切片内部暴露，不会攒到后面返工。

### 1.2 双层验收

| 层 | 使用时机 | 验证方式 |
|---|---|---|
| **单元层（TDD）** | 每个功能点实现时 | 红-绿-重构（先写测试 → 实现 → 重构），保证核心逻辑正确 |
| **切片层（集成）** | 每个切片收尾时 | 集成测试 + **手工可运行验证**（用户亲手跑一遍 CLI / 界面） |

**切片完成 = 双重标准同时满足**：
1. 该切片涉及的所有功能点 TDD 测试全绿
2. 端到端可运行（真实跑一遍，看到真实输出）

### 1.3 fixture 目录树（测试实物）

所有核心逻辑对着**人工构造的 fixture 目录树**断言。

```text
tests/fixtures/
├── backup1/                 # 与 backup2 有完全重复文件
├── backup2/
├── links/                   # symlink/hardlink/junction/mount 场景
│   ├── in_scope_link/       # 指向 scope 内
│   ├── out_scope_link/      # 指向 scope 外
│   ├── broken_link/         # 指向不存在
│   └── circular/            # 循环链接
├── archives/
│   ├── project.zip          # 与解压目录 FullyExtracted 配对
│   └── project/             # 解压目录
└── sibling_a/  sibling_b/   # 结构高度相似的目录对
```

fixture 以脚本或 `git` 方式维护（目录结构、mtime 可控制），测试不得依赖机器上已有真实文件。

---

## 2. 切片地图（里程碑总览）

| 切片 | 名称 | 对应 Phase | 交付物 | 大致工期 |
|---|---|---|---|---|
| **0** | 项目骨架 | Phase 0 | 可构建 sln + 测试基础设施 + CI 三平台绿 | 2 周 |
| **1** | 扫描→manifest→重复→报告 | Phase 1 | **CLI 端到端跑通**（找完全重复 + HTML/JSON 报告） | 3 周 |
| **2** | Link 完整处理 | Phase 2a | 全部 link 类型识别 + 分流 + 不扩大 scope | 1.5 周 |
| **3** | 分层 hash | Phase 2b | SampledHash + 单调升级 + hash 缓存 | 0.5 周 |
| **4** | 关系分析扩展 | Phase 3a | StructuralSibling / Renamed / Moved / Versioned | 2 周 |
| **5** | ArchivePair | Phase 3b | 压缩包↔解压目录配对 + mtime 高可信标记 | 1 周 |
| **6** | Plan + 冲突检测 + Executor | Phase 4 | 标记→计划→dry-run→执行→日志/回滚 | 2 周 |
| **7** | WebUI | Phase 5 | 操作界面 + 报告界面（浏览器） | 3 周 |
| **8** | WPF 桌面壳 | Phase 6 | 操作界面 + 报告界面 + 系统集成 | 2 周 |
| **9** | 增量与性能 | Phase 7 | 增量扫描 / hash 缓存 / 大规模调优 | 持续 |

**v1.0 = 切片 0–8 完成；~17 周。** 切片 9 持续演进。

---

## 3. 切片明细

> 每个切片：目标 → 功能清单 → TDD 红线（关键测试要点）→ 验收命令。
> 实现顺序严格按切片序号，不跳片。

### 切片 0：项目骨架

**目标：** 能构建、能测试、三平台 CI 绿，fixture 就位。

**功能清单：**
- `Helichrysum.sln`（src/ + tests/ 结构，见技术方案 §1.2）
- `Helichrysum.Core` / `Helichrysum.Fs` / `Helichrysum.Cli` 三个空壳项目 + xUnit 测试项目
- Serilog + DI + 配置（`Microsoft.Extensions.Configuration`）基础设施
- GitHub Actions：Windows / Linux / macOS 三平台 build + test
- `tests/fixtures/` 目录树建立（脚本生成，含 mtime 控制）

**TDD 红线：** 一个冒烟测试跑通（如 `SampleTest`），验证测试基础设施可用。

**验收命令：**
```bash
dotnet build Helichrysum.sln
dotnet test
git push  # CI 三平台绿
```

---

### 切片 1：扫描 → manifest → 重复 → 报告（Walking Skeleton）

**目标：** CLI 能对指定两个目录完成"扫描 → 找完全重复 → 输出报告"的端到端流程。

**功能清单：**
- **Scope**：`ScopeConfig`（根路径集合、排除规则 glob）、canonical path、`Contains()` 判定
- **Scanner 基础版**：递归遍历 regular file / directory / symlink（识别但不跟随）；排除规则；进度上报
- **Manifest**：SQLite schema（scopes / objects / hashes / relations 表）、迁移、批量写入、断点续扫占位
- **ExactDuplicate**：size 索引 → 元数据碰撞 → FullHash（SHA256）→ 去重分组
- **报告**：HTML 单文件（目录树 + 重复组标记）+ JSON

**TDD 红线：**
- Scope.Contains：包含 / 排除 / 路径边界（不包含父级前缀误匹配）
- Scanner：扫 fixture `backup1/backup2` → 断言对象数量、类型、路径相对化正确
- ExactDuplicate：fixture 中构造的重复组 → 断言分组正确；`(size, mtime)` 碰撞才触发 hash
- 报告 JSON：断言重复组成员、路径、hash 字段

**验收命令：**
```bash
helichrysum scope add tests/fixtures/backup1 --name b1
helichrysum scope add tests/fixtures/backup2 --name b2
helichrysum scan --scope b1,b2
helichrysum analyze --tier full
helichrysum report --format html --out report.html
# 打开 report.html：应看到 backup1/backup2 的重复组
```

---

### 切片 2：Link 完整处理

**目标：** 正确处理所有 link 类型，不重复统计、不扩大 Scope。

**功能清单：**
- symlink：不跟随；scope 内 → 建立 LinkReference；scope 外 → OutOfScope；目标缺失 → Broken；循环 → Circular
- hardlink：inode group 分组，同一组只统计一次内容
- Windows Junction / Reparse Point（P/Invoke，见技术方案 §4.3）
- Mount Point 边界（Linux `/proc/self/mountinfo`）
- canonical path 判定（防路径绕过 Scope）

**TDD 红线：**
- fixture `links/`：断言四种 link 的 `ScopeRelation` 全部正确分类
- hardlink：同一 inode 两个路径 → 只产生一个内容统计
- 循环 link：不无限递归，标 Circular
- scope 外 link：扫描结果不含目标内容

**验收命令：**
```bash
helichrysum scan --scope tests/fixtures/links
helichrysum report --format json
# 检查: out_scope_link → OutOfScope；broken_link → Broken；circular → Circular
```

---

### 切片 3：分层 hash

**目标：** 从"全量 hash 所有文件"优化为"分层升级，碰撞才加深"。

**功能清单：**
- `HashTier` 三态（Metadata / SampledHash / FullHash）落地
- SampledHash：头 16KB + 中段 32KB + 尾 16KB（xxhash），<64KB 全读
- 单调升级（永不回退）
- hash 结果缓存（manifest 持久化，二次扫描命中）
- **摘要分层（F-Layered-6）**：文件相等判定走两级摘要——快速摘要（CRC32）预筛 + 强摘要（MD5/SHA256）确认；快速摘要不等 → 直接判不等

**TDD 红线：**
- 升级路径：`(size,mtime)` 不碰撞 → 停在 Metadata；碰撞 → Sampled；采样仍碰撞 → Full
- 采样可靠性：对 fixture 中"仅改动中段"的一对文件，Sampled 升级到 Full 后被识别
- 缓存：二次扫描相同文件 → 不重算（读取 hash 记录）
- 摘要分层：CRC32 相同 → MD5 确认；CRC32 不同 → 直接判不等（不触发 MD5）

**验收命令：** 在切片 1 的链路上加 `--tier sampled`，观察 hash 时间下降、重复组结果不变。

---

### 切片 4：关系分析扩展

**目标：** 识别超出字节级重复的语义关系。

**功能清单：**
- **StructuralSibling**：目录子路径集合 Jaccard ≥ 阈值(默认 0.7) → 推断"同一套数据的演进"
- **Renamed / Moved**：依据（Filesystem Identity + hash + 路径差异）
- **Versioned**：同名文件、size 接近、内容部分相似
- （可选）**NearDuplicate**：文本类文件 EOL/BOM/尾部空白归一化后比对
- **处理决策模型（F-Resolve-1~10）**：对每个关系组输出处理意图 `Equality` / `Compatibility` / `Conflict`
  - 文件级兼容：旧内容逐字包含于新内容（文本类精确；二进制降级低置信建议）
  - 目录级兼容：旧目录文件集合 ⊆ 新目录文件集合（新增=合并即可；减少→人工；同名不同内容→文件级判定）

**TDD 红线：**
- fixture `sibling_a/sibling_b`：Jaccard > 阈值 → StructuralSibling，置信度+依据正确
- 同 inode/同 hash 不同路径 → Moved；仅名字不同 → Renamed
- Versioned：同名不同 mtime、hash 不同 → Versioned 组
- **决策模型**：
  - fixture 构造"旧目录文件列表 ⊆ 新目录"（仅新增）→ `Compatibility`（目录级）
  - fixture 构造"旧有文件在新目录消失" → 非兼容，标人工
  - 文本文件"旧内容逐字包含于新内容" → `Compatibility`（文件级）
  - 内容互不包含 → `Conflict`
  - `Equality` 组（CRC32+MD5 双确认相等）→ 不进入人工

**验收命令：** 报告 JSON 中出现 `StructuralSibling` / `Renamed` / `Moved` / `Versioned` 组，字段含 confidence + evidence；每组附 `resolution` 字段（equality/compatibility/conflict）。

---

### 切片 5：ArchivePair

**目标：** 识别压缩包与解压目录的对应关系。

**功能清单：**
- 压缩包清单提取：zip（`System.IO.Compression`）、tar/tgz、7z/rar（`SharpCompress`）；加密包 → EncryptedArchive
- 候选解压目录查找：同名兄弟目录、`-1` / `_extracted` 后缀
- 匹配度判定：FullyExtracted / PartialExtraction / ModifiedAfterExtraction / Unrelated
- mtime 容差（默认 1h）判定"解压后未改动" → 高可信度"建议清理压缩包"

**TDD 红线：**
- fixture `archives/project.zip + project/`：清单完全一致 → FullyExtracted + 建议清理
- 修改 fixture 中解压目录一个文件（mtime 更新）→ ModifiedAfterExtraction，不标记清理
- 改名/增量文件 → PartialExtraction / Unrelated 正确分类

**验收命令：** 报告 JSON 出现 `ArchivePair` 组及 `FullyExtracted` 标记。

---

### 切片 6：Plan + 冲突检测 + Executor

**目标：** 从标记到执行的完整闭环，安全第一。

**功能清单：**
- **标记**：Keep / MoveToTrash / MoveTo / Rename / ReplaceWithLink / Merge
- **Plan**：生成、持久化、冲突检测（目标已存在 / 对象被多 action 引用 / 跨卷风险）
- **决策落地（F-Resolve）**：基于关系组的 `resolution`（equality/compatibility/conflict）自动生成计划项——Equality → 自动去重；Compatibility → 自动以新为准；Conflict → 进人工队列。**自动项同样列出到报告，标注判据/置信度，可被用户否决**
- **dry-run**：模拟执行结果预览
- **Executor**：二次确认；**Trash 优先**（Windows 回收站 / macOS Finder / Linux gio）；跨卷两阶段（先 Copy 后 Delete）；执行日志；中断恢复

**TDD 红线：**
- 冲突检测：目标路径已存在 → Conflict；同对象两个 action → Conflict
- Equality 组 → 自动产生去重计划项（保留一份，其余 MoveToTrash）
- Compatibility 组 → 自动产生"以新为准"计划项（旧版标清理），且**出现在报告中可被否决**
- Conflict 组 → 不进自动执行，进人工队列
- 用户否决自动项 → 该决定持久化，不重复生成
- Trash：对 fixture 文件执行 MoveToTrash → 断言进入对应平台回收站/Trash（测试注入 mock 断言调用）
- 两阶段：Copy 失败 → 不执行 Delete（异常路径测试）
- 执行日志：每条动作写日志，幂等恢复

**验收命令：**
```bash
helichrysum plan new --from-marks plan.json
helichrysum plan show <id>        # 冲突列表
helichrysum plan dry-run <id>     # 模拟执行结果
helichrysum exec <id>             # 确认后执行
# 验证: 文件进入回收站、目标目录结构正确、manifest 已更新
```

---

### 切片 7：WebUI

**目标：** 浏览器可用：操作界面 + 报告界面。

**功能清单：**
- ASP.NET Core API（操作接口 + 报告接口两套端点，见技术方案 §5.3）
- **操作界面**：Scope 配置、扫描进度、问题列表（默认视图，类型分组、批量标记）、计划管理、执行、全局筛选条
- **报告界面**：目录树逐层展开 + 问题标记 + **预览/差异面板**（文本并排 Diff、图片对比、ArchivePair 清单差异、exe 元数据）、导出
- 前端 Vite + Svelte（待定框架），构建产物入 `wwwroot/`
- 本地服务默认 `127.0.0.1`

**验收方式：**
- fixture 扫描 → 浏览器完整流程走一遍：配置 → 扫描 → 问题列表 → 标记 → 计划 → dry-run → 执行
- 报告界面：目录树展开、预览文本 Diff、导出 HTML

**注意：** 此切片是"交互实验室"——UI 布局与交互细节在此迭代定稿，然后才进入 WPF。

---

### 切片 8：WPF 桌面壳

**目标：** Windows 桌面一等公民体验。

**功能清单：**
- MVVM（`CommunityToolkit.Mvvm`），复刻切片 7 定稿的交互
- 操作界面：配置面板、扫描进度、问题列表（默认）、标记、计划、执行、文件行动（资源管理器定位/默认程序打开）
- 报告界面：目录树 + 预览/差异面板 + 导出
- 系统托盘：后台扫描通知、双击展开
- 从切片 1 起的所有 Core 能力直连（进程内）

**验收方式：** Windows 上手动完整流程；托盘、定位、拖拽等原生能力逐项验证。

---

### 切片 9：增量与性能（持续）

**目标：** 规模化与长期维护。

**功能清单：**
- **增量扫描**：对比上次 manifest，只扫新增/修改
- hash 缓存命中率优化
- 100 万+ 文件调优（内存预算、批量事务、索引）

**验收命令：** 对真实大规模目录（用户数据）执行，报告时间/内存指标。

---

## 4. 风险与回退点

| 风险 | 出现时机 | 应对 |
|---|---|---|
| 采样 hash 误判（中段碰撞漏检） | 切片 3 | 采样阈值可调；`MetaOnly` 回退层保留 |
| ArchivePair 误配（命名相似但无关） | 切片 5 | 多重证据：清单匹配 + mtime + size 分布；低于阈值标 Unrelated |
| 跨卷移动中断 | 切片 6 | 两阶段 + 执行日志；中断恢复向幂等 |
| WebUI 交互不好用 | 切片 7 | 这正是"交互实验室"——迭代到满意再进 WPF，不返工 |
| Trash 在无桌面环境失效 | 切片 6 | 检测失败即报错，绝不静默永久删除 |
| 平台 P/Invoke 差异（junction/reparse） | 切片 2 | 未知 reparse 类型标记 Unknown 保留原始数据；三平台 CI 覆盖 |

---

## 4.5 TDD 验收标准（每条测试必须满足的全部条件）

> 每个切片的每条单元测试、集成测试都必须通过以下全部门槛。**不满足 = 测试不合格 = 切片不算完成。**

### 4.5.1 单元测试（TDD 核心）—— 必须全部满足

| # | 标准 | 失败后果 |
|---|---|---|
| U-1 | **红-绿-重构闭环**：测试先写并失败（红），实现后通过（绿），再重构保持绿 | 一上来就"写实现再补测试"视为不合格 |
| U-2 | **独立可运行**：`dotnet test --filter <测试名>` 单测可独立跑，不依赖其他测试顺序 | 测试间存在隐式依赖 = 不合格 |
| U-3 | **fixture 驱动**：断言对象来自 `tests/fixtures/` 目录树，不碰机器上真实数据 | 依赖真实文件 = 不合格 |
| U-4 | **确定性**：同一 fixture 跑任意次结果一致（无随机、无时间戳漂移、无并发竞态） | 偶发性失败 = 不合格 |
| U-5 | **单断言组**：一个测试聚焦一个行为断言（可多 assert 但必须同主题） | 一个测试验证多个无关行为 = 不合格 |
| U-6 | **异常路径覆盖**：涉及 IO/解析的逻辑必须同时测成功与失败路径 | 只测 happy path = 不合格 |
| U-7 | **平台抽象 mock 化**：`Helichrysum.Fs` 的 P/Invoke 用接口 mock 注入，测试不真调 Windows API | 测试跑真实系统调用 = 不合格 |

### 4.5.2 集成测试（切片层）—— 必须满足

| # | 标准 | 失败后果 |
|---|---|---|
| I-1 | **链路串通**：切片验收命令能用 fixture 完整跑通（扫描→分析→输出） | 命令中途报错 = 不合格 |
| I-2 | **输出断言**：`--json` 输出结构稳定，与报告 schema 契约一致（可被机器解析） | 输出格式漂移 = 不合格 |
| I-3 | **幂等性**：同一 fixture 执行两次结果一致（增量扫描场景除外） | 二次运行结果变 = 不合格 |
| I-4 | **跨平台**：链路至少在目标平台（Win/Linux/macOS）之一 + CI 全部绿 | 任一平台挂 = 不合格 |

### 4.5.3 手工验收（用户视角）—— 必须满足

| # | 标准 | 说明 |
|---|---|---|
| M-1 | 用户按切片验收命令亲手跑一遍，看到**真实输出**和预期一致 | 机器通过 ≠ 用户认可 |
| M-2 | 用户对"自动处理项"（Equality/Compatibility）在报告中**可见且可否决** | F-Resolve-7 的人工确认闭环 |
| M-3 | 冲突项（Conflict）确实进入人工队列，未被自动误处理 | F-Resolve-5/6 |

### 4.5.4 度量门槛（覆盖率硬指标）

| 指标 | 门槛 |
|---|---|
| 核心逻辑（Scope / Relation / Resolve / Plan）行覆盖率 | ≥ 80% |
| 平台抽象层（Helichrysum.Fs） | ≥ 60%（P/Invoke 部分尽力） |
| 集成测试对每个切片的验收命令 | 100% 跑通 |

---

## 5. 完成定义（Definition of Done）

每个切片合入前检查：

- [ ] 切片清单内所有功能点实现
- [ ] **§4.5 TDD 验收标准全部满足**（U-1~U-7 + I-1~I-4 + M-1~M-3 + 覆盖率门槛）
- [ ] 该切片所有 TDD 红线测试全绿（红-绿-重构闭环过）
- [ ] 集成测试通过（链接路 + 幂等 + 输出契约）
- [ ] 手工按验收命令跑通过，看到真实输出（M-1）
- [ ] CLI `--json` 输出机器可读且稳定
- [ ] 代码符合技术方案（§2.4 接口隔离：IScannerDriver / IHashProvider / IPreviewProvider 不违背）
- [ ] 不引入 `as` 强转 / 吞异常 / 未处理空引用（.NET 分析器开启）

---

## 6. 实施顺序快速指引

```text
先: 切片 0（骨架）→ 切片 1（端到端握手）   —— 5 周内拿到可运行工具
再: 切片 2-6（加深核心能力）                 —— 逐层加 Link/hash/关系/归档/执行
后: 切片 7（WebUI 交互定稿）→ 切片 8（WPF 落地）
终: 切片 9（规模性能）
```

**每个切片完成后，用户亲手验收一次：** 这是流程硬约束。