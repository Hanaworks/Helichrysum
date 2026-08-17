# Helichrysum 实施计划（Implementation Plan）

| 项目 | 内容 |
|---|---|
| 文档状态 | Draft v0.2 |
| 最后更新 | 2026-08-14 |
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

### 1.1a 切片实施流程（强制）

每个切片/里程碑开工前，必须按以下流程执行，不可跳过：

```text
① 编写切片计划（docs/slice-<n>-plan.md）
   ├─ 实现目标（验收命令）
   ├─ 任务计划与功能点（按依赖排序）
   └─ TDD 测试用例（测试名 + 断言 + 涉及 F-xxx）
② 确认计划（用户审阅）
③ 按任务顺序逐个实现（TDD：红 → 绿 → 重构）
④ 端到端验收（跑验收命令，用户亲手确认）
⑤ 切片完成，计划文档标注状态
```

每个切片的计划文档存入 `docs/slice-<n>-plan.md`，完成后头部标注 `✅ done` 状态。

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
| **0** | 项目骨架 | Phase 0 | 可构建 sln + 测试基础设施 + CI 三平台绿 | 2 周 | ✅ |
| **1** | 扫描→manifest→重复→报告 | Phase 1 | **CLI 端到端跑通**（找完全重复 + HTML/JSON 报告） | 3 周 | ✅ |
| **2** | Link 完整处理 | Phase 2a | 全部 link 类型识别 + 分流 + 不扩大 scope | 1.5 周 | ✅ |
| **3** | 分层 hash | Phase 2b | SampledHash + 单调升级 + hash 缓存 | 0.5 周 | ✅ |
| **4** | 关系分析扩展 | Phase 3a | StructuralSibling / Renamed / Moved / Versioned | 2 周 | ✅ |
| **5** | ArchivePair | Phase 3b | 压缩包↔解压目录配对 + mtime 高可信标记 | 1 周 | ✅ |
| **6** | Plan + 冲突检测 + Executor | Phase 4 | 标记→计划→dry-run→执行→日志/回滚 | 2 周 | ✅ |
| **7** | WebUI | Phase 5 | 操作界面 + 报告界面（浏览器） | 3 周 | ⏳ |
| **8** | WPF 桌面壳 | Phase 6 | 操作界面 + 报告界面 + 系统集成 | 2 周 | ⏳ |
| **9** | 扫描优化与性能 | Phase 7 | 全量快照重扫优化 / hash 缓存 / 压力基准 | 持续 | ⏳ |

**v1.0 = 切片 0–8 完成；切片 0-6 已交付（64 测试全绿）。** 切片 9 持续演进。

---

## 3. 切片明细

> 每个切片：目标 → 功能清单 → TDD 红线（关键测试要点）→ 验收命令。
> 实现顺序严格按切片序号，不跳片。

### 切片 0：项目骨架

**目标：** 能构建、能测试、三平台 CI 绿，fixture 就位。

**功能清单：**
- `Helichrysum.sln`（src/ + tests/ 结构，见技术方案 §1.2）
- `Helichrysum.Core` / `Helichrysum.Filesystem` / `Helichrysum.Cli` 三个空壳项目 + xUnit 测试项目
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
- **Manifest**：SQLite schema（scopes / objects / hashes / relations / tags 表 + `_schema_version` 版本号占位）、批量写入、断点续扫占位；版本转换函数**发布前补齐**（开发期删库重扫，F-Report-11）
- **ExactDuplicate**：size 索引 → 元数据碰撞 → FullHash（SHA256）→ 去重分组
- **报告**：只读精简投影——目录聚合 + 重复组 + 标签统计；分级渲染（摘要内嵌 + 详情惰性）+ 单文件 HTML（默认，≤阈值）+ JSON

**TDD 红线：**
- Scope.Contains：包含 / 排除 / 路径边界（不包含父级前缀误匹配）
- Scanner：扫 fixture `backup1/backup2` → 断言对象数量、类型、路径相对化正确
- ExactDuplicate：fixture 中构造的重复组 → 断言分组正确；`(size, mtime)` 碰撞才触发 hash
- 报告 JSON：断言重复组成员、路径、hash 字段
- **韧性隔离（F-Scan-11）**：fixture 中放置截断 zip / 坏 JPEG 头 / 超长文件名 → 扫描不崩，正确跳过并标记（断言 scan 完成、坏对象有记录、其余对象完整）

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
- **摘要分层（F-Layered-6/7）**：文件相等判定走两级摘要——快速摘要（CRC32）预筛 + 强摘要（MD5/SHA256）确认；快速摘要不等 → 直接判不等；**指纹持久化能力（CRC32+MD5+时间戳，强度可配）作为执行验证统一基线**

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
- **处理决策模型（F-Resolve-1~17）**：对每个关系组输出处理意图 `Equality` / `Compatibility` / `Conflict`
  - 文件级兼容：旧内容逐字包含于新内容（文本类精确；二进制降级低置信建议）
  - 目录级兼容：旧目录文件集合 ⊆ 新目录文件集合（新增=合并即可；减少→人工；同名不同内容→文件级判定）
  - **处理链（F-Resolve-11，最高约束）**：文件级 → 目录级 → 结构级固定顺序，不可跳级；交织特例通过"下钻→回溯"嵌入链中（F-Resolve-12）
  - **同层仲裁**：仅同层意图矛盾时触发（Moved > StructuralSibling > ArchivePair > Duplicate），意图一致直接合并（F-Resolve-13）
  - **按需下钻 = 暴露机制**：界面沿链自上而下查看，展开依据不下钻处理链本身（F-Resolve-14）
  - **依赖链可视化**：报告标注处理链顺序 + 下钻依据（F-Resolve-15）
- **新旧多证据综合（F-Resolve-4/4a/16/17）**：内容包含 > 命名序列/演化链 > 可信时间 > size > 压缩包锚点；时间经受 ctime 聚集检验；证据不足全部交给人工（不武断猜测）

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
  - **处理链（F-Resolve-11/12）**：
    - 主路径：处理链按固定顺序推进（文件→目录→结构），不可跳级
    - 交织场景：NewDir 整体更新，但 OldDir 有唯一新文件 → 处理链在目录环发现存疑 → 下钻文件级先保留该文件 → 回溯目录环结果为"合并"而非"清理 OldDir"
    - 依赖链：回溯后的目录级决策结果 != 用原始快照计算的结果（证明使用了文件级解决状态）
    - 同层冲突：Moved KEEP vs Duplicate CLEAN → 仲裁判 KEEP，无需人工
    - 意图一致：同文件全组 CLEAN → 直接合并，不触发仲裁
  - **新旧证据链（F-Resolve-4/4a/16/17）**：
    - 内容包含 → 判定方向（不依赖时间）
    - 目录命名序列（backup0505/0620）+ 演化链 → 方向证据
    - ctime 聚集检验：fixture 中"整目录同一 ctime" → 时间降权，不参与投票
    - ctime+mtime 成对：ctime 老 + mtime 新 = 可信演化；双同时 = 拷贝痕迹
    - 父目录时间佐证：父目录 mtime 晚于全部子文件 → 佐证活跃目录；父子全聚集 → 整链拷贝痕迹（F-Resolve-4a 补充）
    - 一致性投票：多数派一致 + 孤立者 → 孤立者标 Integrity_Suspected（F-Resolve-18）
    - 格式内建校验：zip CRC/JPEG 结构解析失败但 hash 自洽 → 仍标 Suspected（F-Resolve-19）
    - 压缩包锚点：受污染的 A 目录关联旧 zip（内部时间戳早）vs B 目录 ctime 分散 → 判 B 新
    - 证据耗尽 → 提升人工，断言不产生自动猜测决策

**验收命令：** 报告 JSON 中出现 `StructuralSibling` / `Renamed` / `Moved` / `Versioned` 组，字段含 confidence + evidence；每组附 `resolution` 字段（equality/compatibility/conflict）。

---

### 切片 5：ArchivePair

**目标：** 识别压缩包与解压目录的对应关系。

**功能清单：**
- 压缩包清单提取：zip（`System.IO.Compression`）、tar/tgz、7z/rar（`SharpCompress`）；加密包 → EncryptedArchive
- 候选解压目录查找：同名兄弟目录、`-1` / `_extracted` 后缀
- 匹配度判定：FullyExtracted / PartialExtraction / ModifiedAfterExtraction / Unrelated
- mtime 容差（默认 1h）判定"解压后未改动" → 高可信度"建议清理压缩包"
- **解压锚点产出（F-Resolve-16 联动）**：提取压缩包内部条目时间戳与压缩包文件 mtime，供新旧判定的证据链使用

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
- **安全兜底（F-Exec-7~12）**：清理前复制到 Staging 保底区；清理后重算保留副本 hash 校验、失败自动回滚；保底三策略可配；执行前重校验对象身份（防 TOCTOU）；Suspected 对象不进自动清理
- **Executor**：二次确认；**Trash 优先**（Windows 回收站 / macOS Finder / Linux gio）；跨卷两阶段（先 Copy 后 Delete）；执行日志；中断恢复

**TDD 红线：**
- 冲突检测：目标路径已存在 → Conflict；同对象两个 action → Conflict
- Equality 组 → 自动产生去重计划项（保留一份，其余 MoveToTrash）
- Compatibility 组 → 自动产生"以新为准"计划项（旧版标清理），且**出现在报告中可被否决**
- Conflict 组 → 不进自动执行，进人工队列
- 用户否决自动项 → 该决定持久化，不重复生成
- **Staging（F-Exec-7/8）**：清理动作 → 断言被清理对象先复制进 staging（含 manifest 记录）；清理后 mock 保留副本 hash 校验失败 → 断言自动回滚恢复 + 报告标"已回滚"
- **TOCTOU（F-Exec-11）**：
    - 执行前替换 fixture 目标对象身份 → 断言动作中止 + 标人工
    - 修改 fixture 文件 mtime（内容不变）→ 时间校验触发中止
    - 修改 fixture 文件内容（mtime 不变）→ CRC32 触发中止（不走到 MD5）
    - symlink 替换目标（路径字符串相同）→ realpath 后身份不符 → 中止
    - 指纹基线缺失的对象 → 默认中止该动作
- **Suspected（F-Exec-12）**：fixture 中标记 Suspected 的文件 → 断言不进自动清理
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
- **报告界面**：目录树逐层展开 + 问题标记 + **预览/差异面板**（文本并排 Diff、图片对比、ArchivePair 清单差异、exe 元数据）、导出（只读，无决策操作）
- 前端 Vite + Vue（已定），构建产物入 `wwwroot/`
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

### 切片 9：扫描优化与性能（持续）

**目标：** 规模化与长期维护。

**功能清单：**
- **全量快照重扫优化**：每次扫描都是全新快照（不做跨快照增量追踪），优化并行遍历、批量写入、索引
- hash 缓存命中率优化
- 100 万+ 文件调优（内存预算、批量事务、索引）
- **快照间差异分析**：两个历史 manifest 之间做只读 diff（新增/移除/变更），用于"上次归档 vs 现在"对比，不做增量建档
- **压力/并发对抗基准**（§4.6.5）：50 万文件合成数据扫描基线、扫描时干扰器、多线程遇文件锁、双进程同开 manifest——建立时间/内存基准曲线

**验收命令：** 对真实大规模目录（用户数据）执行，报告时间/内存指标；压力基准对比基线无退化。

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
| U-7 | **平台抽象 mock 化**：`Helichrysum.Filesystem` 的 P/Invoke 用接口 mock 注入，测试不真调 Windows API | 测试跑真实系统调用 = 不合格 |

### 4.5.2 集成测试（切片层）—— 必须满足

| # | 标准 | 失败后果 |
|---|---|---|
| I-1 | **链路串通**：切片验收命令能用 fixture 完整跑通（扫描→分析→输出） | 命令中途报错 = 不合格 |
| I-2 | **输出断言**：`--json` 输出结构稳定，与报告 schema 契约一致（可被机器解析） | 输出格式漂移 = 不合格 |
| I-3 | **幂等性**：同一 fixture 执行两次结果一致 | 二次运行结果变 = 不合格 |
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
| 平台抽象层（Helichrysum.Filesystem） | ≥ 60%（P/Invoke 部分尽力） |
| 集成测试对每个切片的验收命令 | 100% 跑通 |

---

## 4.6 测试策略：需求全覆盖（F-xxx 全部可追溯）

> 每条需求（当前 116 条，持续演化中）都必须有可断言的测试证明"已实现且正确"。
> 不存在"无测试覆盖的需求"——这是硬门禁，不是建议。

### 4.6.1 测试组织架构（与需求模块对齐）

```text
tests/
├── Helichrysum.Core.Tests/          # 核心逻辑单元测试
│   ├── ScopeTests/                  # → F-Scope (7条)
│   ├── ScanningTests/               # → F-Scan (11条)
│   ├── LinkTests/                   # → F-Link (6条)
│   ├── LayeredHashTests/            # → F-Layered (6条)
│   ├── RelationTests/               # → F-Relation (8条)
│   ├── ResolutionTests/             # → F-Resolve (10条)
│   ├── ArchiveTests/                # → F-Archive (7条)
│   ├── ReportTests/                 # → F-Report (12条)
│   ├── PreviewTests/                # → F-Preview (10条)
│   ├── PlanTests/                   # → F-Plan (7条)
│   ├── ExecTests/                   # → F-Exec (6条)
│   └── FormTests/                   # → F-Form (6条)
├── Helichrysum.Integration.Tests/   # 端到端链路（每切片验收命令）
└── Helichrysum.Cli.Tests/           # CLI 行为测试
```

### 4.6.2 命名规范（追溯自动化）

每条需求在对应测试类中至少一个 `[Fact]`，测试名带需求号，构建时可自动核对：

```csharp
// xUnit 的 Theory/属性或命名惯例：
public class ScanTests
{
    [Fact]
    public void F_Scan_1_RegularFile_IsScanned() { ... }   // → F-Scan-1
    [Fact]
    public void F_Scan_6_CircularReference_IsDetected() { ... } // → F-Scan-6
}
```

> 工具侧：CI 步骤用 `grep` 或测试清单核对"每条 F-xxx 都有同名测试"；缺失 → 构建告警/失败。

### 4.6.3 需求 → 测试覆盖矩阵（简化摘录，完整实现时逐条展开）

| 模块 | 需求数 | 测试套件 | 关键覆盖点（示例） |
|---|---|---|---|
| F-Scope | 7 | `ScopeTests` | 多根路径；排除规则；canonical 防绕过；配置持久化 |
| F-Scan | 11 | `ScanningTests` | 类型识别；hardlink 不重复；循环；可中断；进度；**韧性隔离（坏文件跳过不崩 + 解析上限）** |
| F-Link | 6 | `LinkTests` | 不跟随；InScope/OutOfScope/Broken/Circular 分流 |
| F-Layered | 7 | `LayeredHashTests` | 四层分析；单调升级；摘要分层 CRC32→MD5；**指纹持久化能力（强度可配，基线 CRC32+MD5+时间戳）** |
| F-Relation | 8 | `RelationTests` | 九种关系识别；置信度；可追溯 |
| F-Resolve | 10 | `ResolutionTests` | 三态决策；目录级兼容；自动项可见可否决 |
| F-Archive | 7 | `ArchiveTests` | 8 格式清单；配对判定；mtime 容差；加密标记 |
| F-Report | 12 | `ReportTests` | 目录树展开；筛选；**只读边界（无写能力）**；**分级渲染（摘要内嵌+详情惰性）**；**精简投影（≤20MB）**；**单文件唯一形态 + 超限截断**；未启动计划可查看；**schema 版本校验 + 一次性转换 + 降级报错**；**快照元数据（时间/Scope 快照/工具版本）+ 快照年龄展示** |
| F-Preview | 10 | `PreviewTests` | 文本/图片/PDF/Office 预览；exe 不预览；打开/定位 |
| F-Plan | 7 | `PlanTests` | 动作类型；dry-run；保存加载；冲突检测；回滚信息 |
| F-Exec | 6 | `ExecTests` | 二次确认；Trash 优先；执行日志；中断恢复 |
| F-Form | 6 | `FormTests` | CLI/SDK/WebUI/GUI 共享 manifest；CLI 独立跑全流程 |

### 4.6.4 "生活化场景"测试数据集

除模块单元测试外，另建**贴近真实世界**的集成测试数据集（见场景表），用于验证"面对真实数据形态时结果合理"——不只是"逻辑正确"。

| 场景 | 用户故事 | 覆盖需求 | 预期结果（golden） |
|---|---|---|---|
| S1 多盘多年演进 | 三套备份(B2019/B2021/Current) | StructuralSibling + F-Resolve | 兼容组；冲突组列出 |
| S2 下载解压残留 | zip+解压目录(改过/没改过) | F-Archive-4/5 | FullyExtracted vs ModifiedAfter |
| S3 零散复制 | 同文件散落桌面/下载/文档 | F-Scan-6 + F-Resolve | Equality 自动去重 |
| S4 批量重命名移动 | 整个文件夹改名挪位 | F-Relation-3 | Renamed/Moved |
| S5 链接与外部盘 | 链接到移动硬盘 | F-Link-2 | OutOfScope 分流正确 |
| S6 真实文件类型 | docx/pdf/jpg/zip/源码混合 | F-Preview 系列 | 预览能力按类型分流 |
| S7 mtime 分布 | 文件时间跨年纪(b2019~2024) | F-Resolve-4 | 新旧判定正确 |
| S8 深层嵌套+混乱命名 | 1000 层嵌套、`新建文件夹(2)` | F-Scan-1 + 报告 | 扫描不崩、报告可读 |

**Golden file 机制**：每个场景固定一个断言 JSON，运行结果与之逐字段 diff；人为更新须显式 `--update-golden` 并 review diff。

### 4.6.5 自动化门禁（CI 强制）

```text
1. dotnet test --collect:"XPlat Code Coverage"    # 所有 F-xxx 需求的对应测试运行
2. 覆盖率报告：核心 ≥80% 失败则 CI 红
3. 需求追溯核对：脚本检查每条 F-xxx 有对应测试名（缺失 → CI 红）
4. lifelike 场景 golden diff 失败 → CI 红
5. 三平台（Win/Linux/macOS）全绿
```

**脏输入冒烟**（CI 必跑，轻量）：fixture 内置"半损坏"样本集（截断 zip、坏 JPEG 头、超长路径、0 字节文件、声明与内容不符的文件）——每次 CI 跑一轮扫描冒烟：不得崩溃、坏样本被正确跳过并标记。**最低限度：即使无任何额外 fuzz 手段，扫描必须能完整跑完（F-Scan-11 韧性隔离）**；在此基础上专门 fuzz 压力测试作为可选增强（解析入口如压缩包/路径/配置加随机畸形输入，发现崩溃 → 固化为回归用例入库），不强求引入专用 fuzz 框架，可用内置随机生成器替代。

**压力/并发对抗**（切片 9 阶段，与性能调优同步）：合成大数据基准（50 万文件扫描耗时/内存基线）、扫描时干扰器（后台增删文件）、多线程 hash 遇文件锁、双进程同开 manifest——作为独立基准任务，不进常规 CI（CI 只跑脏输入冒烟）。

---

## 5. 完成定义（Definition of Done）

每个切片合入前检查：

- [ ] 切片清单内所有功能点实现
- [ ] **§4.5 TDD 验收标准全部满足**（U-1~U-7 + I-1~I-4 + M-1~M-3 + 覆盖率门槛）
- [ ] **§4.6 需求全覆盖检查通过**：该切片涉及的 F-xxx 需求全部有对应测试（追溯核对脚本绿）
- [ ] 该切片所有 TDD 红线测试全绿（红-绿-重构闭环过）
- [ ] 集成测试通过（链接路 + 幂等 + 输出契约）
- [ ] 手工按验收命令跑通过，看到真实输出（M-1）
- [ ] CLI `--json` 输出机器可读且稳定
- [ ] 代码符合技术方案（§2.4 接口隔离：IScannerDriver / IHashProvider / IPreviewProvider 不违背）
- [ ] 不引入 `as` 强转 / 吞异常 / 未处理空引用（.NET 分析器开启）
- [ ] **命名不使用缩写**（技术方案 §1.1 原则 6）：类型/成员/变量/文件名/配置键/表名列名用完整自描述命名，仅限业界标准术语例外
- [ ] **复杂度控制**（技术方案 §1.1 原则 7）：未引入多余抽象层/中间层；未重复实现框架已有功能；新增复杂度均有"降低整体复杂度"的正当理由
- [ ] **代码美学与换行**（技术方案 §1.1 原则 8）：无 120 字符强行折行；无意义拆分已避免；链式调用符合垂直对齐规范

---

## 6. 实施顺序快速指引

```text
✅ 切片 0-1（骨架 + 扫描/重复/报告）
✅ 切片 2-6（Link 处理 / 分层 hash / 关系分析 / ArchivePair / Plan+Executor）
⏳ 切片 7（WebUI 交互定稿）→ 切片 8（WPF 落地）
  每次交付后执行：dotnet test 验证 64 测试全绿
```

**每个切片完成后，用户亲手验收一次：** 这是流程硬约束。

---

## 7. 待实现功能项（切片 0-10 完成后的补充）

### 7.1 高优（7 项）

| # | 名称 | 状态 | 说明 | 关联 F-xxx | 工作量 |
|---|---|---|---|---|---|
| v1 | verify 命令 | ✅ 已完成 | 归档完整性重新 hash 对比 manifest | F-Exec-5 | 0.5 天 |
| v2 | CI 实跑 | ⏳ | GitHub Actions 验证三平台构建+测试绿 | — | 0.5 天 |
| v3 | 报告目录树渲染 | ⏳ | F-Report-1 HTML 目录树可展开 | F-Report-1 | 1-2 天 |
| v4 | 快照年龄展示 | ⏳ | F-Report-12 报告头部显示创建时间 | F-Report-12 | 0.5 天 |
| v5 | CONFIG 配置化 | ⏳ | 保底策略/报告阈值/分析深度做成配置读取 | 配置哲学 | 1 天 |
| v6 | CLI 端到端集成测试 | ⏳ | Scan→Analyze→Plan→Exec 全流程自动化测试 | — | 1 天 |
| v7 | AOT 单文件发布验证 | ⏳ | dotnet publish NativeAOT | — | 0.5-1 天 |

### 7.2 中优（14 项）

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

### 7.3 低优（3 项 + 2 UI）

| # | 名称 | 状态 | 说明 | 关联 F-xxx | 前提 |
|---|---|---|---|---|---|
| v22 | Hardlink inode 检测 | ⏳ | P/Invoke 跨平台 | F-Link-3 | 有真实 hardlink 需求 |
| v23 | Mount Point 边界 | ⏳ | Linux 挂载点检测不跨设备 | F-Link-4 | 非阻塞 |
| v24 | 交互式确认流 | ⏳ | 交互式确认替换 --confirm 参数 | F-Exec-1 | WebUI 阶段 |
| UI-7 | WebUI（切片 7） | ⏳ | 操作界面+报告界面 | F-Preview, F-Form | 你单独线 |
| UI-8 | WPF 桌面壳（切片 8） | ⏳ | Windows 优先 | F-Form | 等 WebUI 定稿 |

---

## 8. F-xxx 模块覆盖进度

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

## 9. 里程碑

| 里程碑 | 条件 | 预计 |
|---|---|---|
| Beta CLI 可用 | v1-v7 完成（71→78 测试） | ~1 周 |
| 报告完善 | v3/v4/v9/v10/v11 完成（78→85 测试） | ~2 周 |
| 决策模型完整 | v16-v21 完成（85→95 测试） | ~2 周 |
| 安全层完整 | v12/v13/v14 完成（95→100 测试） | ~1 周 |
| v1.0 Release | 全部非 UI 完成 + UI 线合流 | 待定 |