# Helichrysum 需求规格说明书

| 项目 | 内容 |
|---|---|
| 文档状态 | Draft v0.1 |
| 最后更新 | 2026-08-13 |
| 适用版本 | 目标 v1.0 |
| 关键词 | 个人数字资产、归档、多备份整合、重复识别、Scope 感知、压缩包配对 |

> 本文档采用 RFC 2119 风格的需求分级：**必须（MUST）**、**应当（SHOULD）**、**可以（MAY）**。
> 凡未标级别的语句视为背景说明，不构成可验证需求。

---

## 1. 背景与目标

### 1.1 背景

用户在长期使用计算机的过程中，普遍积累了大量零散备份数据：

- 多块硬盘上的不同时期备份（`Backup2020` / `Backup2022` / `Backup2024`）
- 同一套数据被手工复制、移动、重命名多次
- 下载文件、解压归档（`.zip` / `.7z` / `.tar.gz`）后未清理原压缩包
- 增量备份、镜像备份与日常使用目录混杂
- 系统迁移、设备更换遗留下的半同步目录

这些数据之间存在丰富的语义关系（完全重复、版本演进、重命名、目录结构相似、压缩包与解压目录配对等），但现有工具（重复文件查找器、磁盘分析工具、文件管理器）普遍只能解决"内容 hash 相同 → 列表 → 删除"这一最浅层问题，无法帮助用户完成**真正的整理与归档**。

### 1.2 目标

构建一款**面向个人数字资产的整理与归档工具**，能够：

1. 在用户指定的范围内**识别**数据之间的语义关系（不只是字节级重复）
2. 生成**可逐层展开、可筛选**的分析报告
3. 帮助用户基于报告形成**处理计划**
4. 在用户确认后**安全执行**整理动作
5. 最终将分散在多盘多备份中的数据整合为一套**完整、干净、有序、可长期维护的标准归档（Canonical Archive）**

### 1.3 非目标（明确不做什么）

| 类型 | 说明 |
|---|---|
| **不是云同步工具** | 不与 OneDrive / Dropbox / iCloud 等交互 |
| **不是版本控制系统** | 不维护文件历史版本链，只识别"同一份数据的多个副本" |
| **不是磁盘清理工具** | 不主动清理系统缓存、临时文件、回收站 |
| **不是企业级 DAM** | 不面向团队协作、权限管理、审计 |
| **不是无脑删除工具** | 任何删除动作必须经过用户确认；不提供"自动清理"模式 |
| **不主动扩大扫描范围** | 严格在用户指定 Scope 内工作 |

---

## 2. 核心概念

### 2.1 Scope（扫描范围）

用户在启动一次分析任务时显式指定的**根路径集合**。Scope 是分析世界的边界，扫描器、分析器、报告器、执行器的所有行为都**必须**基于 Scope 推理。

**示例：**

```text
Scope = {
  D:\Backup1
  D:\Backup2
  E:\Archive\Photos
}
```

### 2.2 Filesystem Object（文件系统对象）

将文件系统统一建模为**对象图**而非目录树。对象类型包括：

| 类型 | 说明 |
|---|---|
| Regular File | 普通文件 |
| Directory | 目录 |
| Symbolic Link | 符号链接（POSIX symlink、Windows symlink） |
| Hard Link | 硬链接（同一 inode / file record 多个路径） |
| Windows Junction | NTFS 目录联结 |
| Windows Reparse Point | 包括上述 Junction 及其他属性点 |
| Mount Point / Bind Mount | POSIX 挂载点 / Linux bind mount / macOS firmlink |
| Other FS Objects | 设备文件、套接字、管道等 |

### 2.3 Object Identity（对象身份）

对象身份分**三层**，分析必须能在三层间自由切换：

1. **Path Identity**：路径（最廉价，不可靠）
2. **Filesystem Identity**：inode / file record / file id（中等，识别 hard link 与重命名）
3. **Content Identity**：内容 hash（最昂贵，最终判据）

### 2.4 Relation（数据关系）

工具需要识别的关系类型：

| 关系 | 定义 |
|---|---|
| `ExactDuplicate` | 内容完全一致（hash 相同） |
| `NearDuplicate` | 内容几乎一致（如仅 EOL、BOM、元数据差异），需要内容级比对 |
| `Versioned` | 同一基础文件的多次修改版本 |
| `Renamed` | 同一对象在不同路径以不同名字出现 |
| `Moved` | 同一对象在不同目录出现（路径不同、内容相同、size+mtime 一致） |
| `StructuralSibling` | 两个目录结构高度相似，推断为同一套数据的演进 |
| `ArchivePair` | 一个压缩包与一个解压目录之间的对应关系 |
| `LinkReference` | Link 与其目标之间的引用关系 |
| `PartialOverlap` | 两个目录内容部分重叠 |

### 2.5 Canonical Archive（标准归档）

整理完成后的目标状态：**每个语义上的"数据单元"在归档中只存在一份**，配有 manifest 描述来源、合并历史、保留决策。

---

## 3. 典型用户场景

### 场景 A：多盘多年备份整合

```text
用户环境：
  D:\Backup2019   （老硬盘拷出）
  E:\Backup2021   （换机时拷出）
  F:\Current      （当前工作盘）

期望：
  识别三者是同一套个人数据的演进
  把"只有旧备份独有"的文件挑出来保留
  把"完全重复"的文件做合并标记
  最终形成 F:\Archive 单一归档
```

### 场景 B：压缩包与解压目录

```text
用户环境：
  D:\Downloads\Project.zip     mtime: 2023-07-01 13:20
  D:\Work\Project\             目录内文件 mtime 集中在 2023-07-01 13:19~13:20

期望：
  识别 Project.zip 与 Project/ 为 ArchivePair
  内容一致 + 解压后无后续修改 → 高可信度标记为"建议删除压缩包"
  不强制自动删除，进入处理计划等待确认
```

### 场景 C：Scope 边界处理

```text
用户环境：
  Scope = { D:\Backup }
  D:\Backup\archive → 指向 E:\External\Archive （Scope 外）

期望：
  扫描到 archive 时识别为 Symbolic Link
  目标 E:\External\Archive 不在 Scope 内
  → 不递归、不统计、不参与重复分析
  → 报告中标记 OutOfScope
```

### 场景 D：增量式渐进分析

```text
用户环境：
  拿到一个 8TB 数据盘，不确定里面有什么

期望：
  先快速建立目录树索引（不读内容）
  在目录层面看到大致结构与可疑相似目录
  圈定可疑子集后，对该子集做文件级 + hash 级分析
  整个过程可以中断、恢复、增量追加
```

### 场景 E：报告驱动的批量决策

```text
用户环境：
  完成一次扫描后，几十万文件、数千组重复

期望：
  报告按目录树逐层展开
  能筛选"仅重复""仅压缩包配对""仅 Scope 外 Link"等问题子集
  在重复组内可一次标记"保留最新""保留路径最长""全部纳入归档候选"
  批量决策汇总为处理计划，预览后再执行
```

---

## 4. 功能需求

### 4.1 Scope 管理（F-Scope）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Scope-1 | 必须 | 用户可以指定一个或多个根路径作为 Scope |
| F-Scope-2 | 必须 | 用户可以指定排除规则（glob / regex / 文件名 / 目录名） |
| F-Scope-3 | 必须 | 用户可以指定排除挂载点 / 设备边界（默认跨设备不递归） |
| F-Scope-4 | 必须 | Scope 判断必须使用 **Canonical Path**（realpath / `GetFinalPathNameByHandle`），避免通过相对路径 / 符号链接绕过 |
| F-Scope-5 | 必须 | 扫描器不得主动把 Scope 外路径纳入扫描世界 |
| F-Scope-6 | 应当 | 支持 Scope 配置的保存、加载、命名 |
| F-Scope-7 | 应当 | 支持从 manifest 反向重建历史 Scope |

### 4.2 文件系统扫描（F-Scan）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Scan-1 | 必须 | 支持 Regular File / Directory / Symbolic Link 的扫描 |
| F-Scan-2 | 必须 | 支持 Hard Link 识别（基于 inode / file id），同一对象不重复统计 |
| F-Scan-3 | 必须 | 在 Windows 上支持 Junction 与 Reparse Point 识别 |
| F-Scan-4 | 必须 | 在 Linux 上支持 Mount Point / Bind Mount 边界识别 |
| F-Scan-5 | 必须 | 在 macOS 上支持 firmlink 与 firmlink 跨卷识别 |
| F-Scan-6 | 必须 | 支持循环引用检测（A→B→A 的 symlink 环） |
| F-Scan-7 | 必须 | 扫描过程必须可中断、可恢复、可增量追加 |
| F-Scan-8 | 必须 | 扫描进度可观测（当前路径、已扫描数、速率、ETA） |
| F-Scan-9 | 应当 | 默认跳过常见虚拟目录（`$RECYCLE.BIN`、`System Volume Information`、`.git/objects` 等可配置） |
| F-Scan-10 | 应当 | 支持并行扫描，CPU 与 IO 调度可配置 |

### 4.3 Link 处理（F-Link）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Link-1 | 必须 | Symbolic Link 默认**不跟随递归**，只记录 (source, target) |
| F-Link-2 | 必须 | Link 目标按 Scope 分流处理：<br>• 目标在 Scope 内 → Resolve 到对象，建立 LinkReference，不重复扫描<br>• 目标在 Scope 外 → 标记 `OutOfScope`，不递归、不统计、不参与分析<br>• 目标不存在 → 标记 `Broken`<br>• 目标形成环 → 标记 `Circular` |
| F-Link-3 | 必须 | Hard Link 仅作为同一对象的多路径引用，不重复统计内容 |
| F-Link-4 | 必须 | Link 信息必须保留在报告中，不因"识别为 Link"而消失 |
| F-Link-5 | 应当 | 支持"跟随 / 不跟随"开关（默认不跟随，可显式启用） |
| F-Link-6 | 应当 | 对 Junction 提供与 Symbolic Link 一致的处理语义 |

### 4.4 分层渐进分析（F-Layered）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Layered-1 | 必须 | 提供**至少四层**分析能力，按代价从低到高：<br>1. 目录结构层（仅元数据）<br>2. 文件元数据层（路径、size、mtime、ctime）<br>3. 快速 hash 层（size+mtime 命中后再算 hash，可选 xxh3 / blake3）<br>4. 全量内容 hash 层（最终判据） |
| F-Layered-2 | 必须 | 上层未命中冲突，下层不再启动 |
| F-Layered-3 | 必须 | 每层结果可独立持久化，支持后续增量补充 |
| F-Layered-4 | 必须 | 用户可以显式指定分析深度（"只跑目录层"、"跑完 hash 层"） |
| F-Layered-5 | 应当 | 提供"快速预览"模式：在数秒内给出目录层概览 |

### 4.5 关系分析（F-Relation）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Relation-1 | 必须 | 识别 `ExactDuplicate`（hash 相同） |
| F-Relation-2 | 应当 | 识别 `NearDuplicate`（文本类文件，容忍 EOL / BOM / 末尾空白差异） |
| F-Relation-3 | 应当 | 识别 `Renamed` / `Moved`（基于 Filesystem Identity + 内容 hash） |
| F-Relation-4 | 必须 | 识别 `StructuralSibling`（目录结构相似度高于阈值时推断） |
| F-Relation-5 | 必须 | 识别 `ArchivePair`（见 F-Archive） |
| F-Relation-6 | 应当 | 识别 `Versioned`（同名文件、size 接近、内容部分相似） |
| F-Relation-7 | 必须 | 每个识别出的关系必须附带**置信度**与**判定依据**（哪些层命中、阈值是多少） |
| F-Relation-8 | 必须 | 每个关系必须**可追溯到原始对象**（不可只给"重复组 ID"） |

### 4.6 压缩包识别（F-Archive）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Archive-1 | 必须 | 支持识别 `.zip` / `.7z` / `.tar` / `.tar.gz` / `.tgz` / `.tar.bz2` / `.tar.xz` / `.rar` 至少 8 种主流格式 |
| F-Archive-2 | 必须 | 提取压缩包内部**文件清单**（路径、size、mtime），不强制解压全部内容 |
| F-Archive-3 | 必须 | 在 Scope 内查找候选解压目录（命名相似的兄弟目录） |
| F-Archive-4 | 必须 | 比较 ArchivePair：内部清单 vs 目录实际内容，判定关系：<br>• `FullyExtracted`（清单完全一致）<br>• `ModifiedAfterExtraction`（目录有新增 / 修改 / 删除）<br>• `PartialExtraction`（目录只解压了部分）<br>• `Unrelated`（仅命名相似） |
| F-Archive-5 | 必须 | 对 `FullyExtracted` + 目录 mtime 接近压缩包 mtime + 无后续修改的情况，赋予"建议清理压缩包"的高可信度标记 |
| F-Archive-6 | 应当 | 支持按需计算压缩包内文件的 hash（用于和目录文件做内容级比对） |
| F-Archive-7 | 应当 | 支持嵌套压缩包识别（压缩包内的压缩包） |

### 4.7 报告（F-Report）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Report-1 | 必须 | 报告以**目录树**形式呈现，从 Scope 根逐层展开 |
| F-Report-2 | 必须 | 每个目录节点显示聚合状态（总大小、文件数、问题数：重复 / 配对 / OutOfScope Link / Broken Link 等） |
| F-Report-3 | 必须 | 每个文件节点显示元数据、所属关系组、关系类型、置信度 |
| F-Report-4 | 必须 | 支持按**问题类型**筛选视图（"仅看 ArchivePair"、"仅看 ExactDuplicate"、"仅看 OutOfScope"） |
| F-Report-5 | 必须 | 支持按**路径 / glob / 大小 / mtime 范围**筛选 |
| F-Report-6 | 必须 | 报告支持导出为：可交互 HTML、JSON（机器可读）、SQLite（查询友好） |
| F-Report-7 | 应当 | 提供 Diff 视图：在同一重复组或 ArchivePair 内并排查看差异 |
| F-Report-8 | 必须 | 报告必须可在**未启动处理计划**时独立查看 |
| F-Report-9 | 必须 | 报告数据必须可序列化为 manifest，供后续增量分析与审计 |

### 4.8 预览与打开（F-Preview）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Preview-1 | 必须 | 支持文本类文件预览（`.txt` / `.md` / `.log` / `.json` / `.xml` / `.yaml` / `.csv` / 源代码） |
| F-Preview-2 | 必须 | 支持图片预览（`.jpg` / `.png` / `.gif` / `.webp` / `.heic` 至少） |
| F-Preview-3 | 应当 | 支持 PDF 预览 |
| F-Preview-4 | 应当 | 支持 Office 文档（`.docx` / `.xlsx` / `.pptx`）的内容提取预览 |
| F-Preview-5 | 应当 | 支持音视频文件的元数据预览（时长、编码、分辨率） |
| F-Preview-6 | 必须 | 可执行文件**不预览内容**，但显示 size / hash / 版本资源 / 数字签名等元数据 |
| F-Preview-7 | 必须 | 提供"使用系统默认程序打开" |
| F-Preview-8 | 必须 | 提供"在文件管理器中定位"（Explorer / Finder / `xdg-open` 父目录） |
| F-Preview-9 | 应当 | 支持调用第三方比较工具（如 Beyond Compare / Meld / WinMerge）做内容 Diff |
| F-Preview-10 | 应当 | 对 NearDuplicate 提供**内置文本 Diff**视图 |

### 4.9 处理计划（F-Plan）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Plan-1 | 必须 | 处理计划是显式对象，由用户从报告中的标记汇总生成 |
| F-Plan-2 | 必须 | 计划项类型至少包括：`Keep` / `Delete` / `MoveToTrash` / `MoveTo` / `Rename` / `ReplaceWithLink` / `Merge` |
| F-Plan-3 | 必须 | 计划必须支持**预览（dry-run）**，展示执行后状态 |
| F-Plan-4 | 必须 | 计划必须支持**保存 / 加载 / 编辑**，允许用户多次迭代 |
| F-Plan-5 | 必须 | 计划必须包含**冲突检测**（如目标路径已存在、移动后产生歧义） |
| F-Plan-6 | 必须 | 计划生成必须基于已持久化的 manifest，可脱离原扫描结果独立验证 |
| F-Plan-7 | 必须 | 计划必须包含**回滚信息**（每个动作的逆操作所需信息） |

### 4.10 执行处理（F-Exec）

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Exec-1 | 必须 | 执行前必须再次确认（**不提供"自动执行"模式**） |
| F-Exec-2 | 必须 | 默认删除动作走**回收站 / Trash**而非直接 unlink |
| F-Exec-3 | 必须 | 每一步执行必须**记录日志**（动作、对象、结果、时间） |
| F-Exec-4 | 必须 | 执行中断后可恢复（基于执行日志 + 原计划） |
| F-Exec-5 | 必须 | 执行完成后生成**新 manifest**，反映整理后的归档状态 |
| F-Exec-6 | 应当 | 支持"先复制后删除"两阶段执行，降低跨卷操作风险 |

### 4.11 形态（F-Form）

工具必须支持以下三种形态，共用同一核心引擎：

| 编号 | 级别 | 需求 |
|---|---|---|
| F-Form-1 | 必须 | **CLI**：完整的命令行接口，覆盖扫描、分析、报告导出、计划生成、执行所有能力 |
| F-Form-2 | 必须 | **SDK**：核心引擎以库形式提供，可被其他程序嵌入 |
| F-Form-3 | 必须 | **WebUI**：本地 HTTP 服务，浏览器访问，承担报告查看、标记管理、计划编辑、配置管理 |
| F-Form-4 | 应当 | **桌面 GUI**：原生 Windows 桌面应用（覆盖核心场景） |
| F-Form-5 | 必须 | 三种形态共享同一份 manifest、配置、报告格式 |
| F-Form-6 | 必须 | CLI 必须可在无 GUI / 无浏览器环境下独立完成全部流程 |

---

## 5. 非功能需求

### 5.1 跨平台（N-Platform）

| 编号 | 级别 | 需求 |
|---|---|---|
| N-Platform-1 | 必须 | 支持 Windows 10/11、主流 Linux 发行版、macOS 三端 |
| N-Platform-2 | 必须 | 三端功能等价（除平台特有的 link 语义） |
| N-Platform-3 | 必须 | 平台特有概念（Junction、Mount Point、firmlink）按平台分别处理，不互相模拟 |

### 5.2 性能与规模（N-Perf）

| 编号 | 级别 | 需求 |
|---|---|---|
| N-Perf-1 | 必须 | 单盘 100 万级文件扫描 + 元数据索引应在合理时间（小时级）内完成 |
| N-Perf-2 | 必须 | 内存占用可控，支持流式扫描（不全量加载到内存） |
| N-Perf-3 | 必须 | Hash 计算支持流式读取 + 多线程并发 |
| N-Perf-4 | 必须 | 支持扫描断点续传，意外中断不丢失已完成结果 |
| N-Perf-5 | 应当 | 增量扫描（针对已有 manifest 追加新文件）显著快于全量扫描 |

### 5.3 安全与可信（N-Safe）

| 编号 | 级别 | 需求 |
|---|---|---|
| N-Safe-1 | 必须 | 任何修改文件系统的动作必须经过用户显式确认 |
| N-Safe-2 | 必须 | 默认走 Trash / 回收站，不直接物理删除 |
| N-Safe-3 | 必须 | 全程只读 → 写入操作严格分离，扫描阶段不得有任何写动作 |
| N-Safe-4 | 必须 | 所有处理动作可审计、可回滚（在 Trash 未清空范围内） |
| N-Safe-5 | 必须 | 不收集、不上传任何用户数据，纯本地工具 |
| N-Safe-6 | 应当 | 对路径操作进行规范化校验，防止 path traversal 等问题 |

### 5.4 可扩展性（N-Ext）

| 编号 | 级别 | 需求 |
|---|---|---|
| N-Ext-1 | 应当 | 关系识别器（Relation Detector）插件化，允许第三方扩展新关系类型 |
| N-Ext-2 | 应当 | 压缩包格式识别器插件化 |
| N-Ext-3 | 应当 | 预览器插件化（按文件类型注册） |
| N-Ext-4 | 应当 | 报告渲染器插件化（自定义导出格式） |

### 5.5 可观测性（N-Obs）

| 编号 | 级别 | 需求 |
|---|---|---|
| N-Obs-1 | 必须 | 提供结构化日志（含扫描进度、分析决策、执行动作） |
| N-Obs-2 | 必须 | 提供 metrics（速率、ETA、各阶段耗时） |
| N-Obs-3 | 应当 | 支持输出 profiling 数据用于性能分析 |

---

## 6. 输入与输出

### 6.1 输入

- 用户指定的 Scope（一个或多个根路径）
- 排除规则（glob / regex）
- 配置文件（链接跟随策略、并发度、忽略目录列表等）
- 历史 manifest（用于增量分析）

### 6.2 输出

| 类型 | 用途 |
|---|---|
| Manifest（SQLite） | 持久化扫描结果与关系，是后续所有操作的事实来源 |
| 报告（HTML） | 可交互浏览，主要给人看 |
| 报告（JSON） | 机器可读，可被脚本二次处理 |
| Plan（JSON / SQLite） | 处理计划，可保存、加载、编辑、审计 |
| Execution Log | 执行日志，可审计、可回滚 |
| Canonical Archive | 整理后的目标目录（用户指定位置） |

---

## 7. 约束与边界

1. **个人规模**：目标用户是个人，不是企业；不处理权限、协作、审计合规问题
2. **本地优先**：所有计算与存储在本地完成；不依赖网络
3. **不破坏原始数据**：扫描阶段只读；执行阶段默认走 Trash；任何破坏性操作都需要显式确认
4. **不做版本控制**：不维护文件历史链；只识别"同一份数据的多个副本"
5. **不替代备份工具**：本身不备份数据；只是整理已有备份

---

## 8. 术语表

| 术语 | 定义 |
|---|---|
| Scope | 用户指定的扫描范围 |
| Filesystem Object | 文件系统中可被识别的对象（文件、目录、链接等） |
| Object Identity | 对象身份，分 Path / Filesystem / Content 三层 |
| Relation | 对象之间的语义关系（重复、版本、配对等） |
| Manifest | 持久化的扫描结果与关系数据 |
| Canonical Archive | 整理后的标准归档 |
| Canonical Path | 路径的规范化形式，解析所有 symlink 与相对引用后的真实路径 |
| OutOfScope | 链接目标不在 Scope 内的状态标记 |
| ArchivePair | 一个压缩包与一个解压目录之间的对应关系 |
| Plan | 处理计划，由用户在报告中标记后汇总生成 |

---

## 9. 后续工作

- [ ] 技术实现方案：见 [`technical-design.md`](./technical-design.md)
- [ ] CLI 命令设计文档
- [ ] 数据模型 / Manifest Schema 详细规范
- [ ] 关系识别器的判定阈值与算法细节
- [ ] UI 原型设计（WebUI 与桌面 GUI）
