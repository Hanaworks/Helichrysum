# Helichrysum 技术实现方案

| 项目 | 内容 |
|---|---|
| 文档状态 | Draft v0.1 |
| 最后更新 | 2026-08-13 |
| 适用版本 | 目标 v1.0 |
| 关联文档 | [requirements.md](./requirements.md) |

> 本文档定义 Helichrysum 的工程实现方案。所有需求条目（F-xxx / N-xxx）请参阅需求文档。

---

## 1. 总体架构

### 1.1 设计原则

1. **核心引擎单一**：所有扫描、分析、报告、计划、执行逻辑集中在一个 Rust crate（`helichrysum-core`），其他形态（CLI、WebUI、桌面 GUI）都是它的"壳"。
2. **形态可分离**：CLI 必须可在无 GUI / 无浏览器环境下独立完成全部流程；WebUI 与 GUI 仅作为人机交互的便利层。
3. **只读扫描与写入执行严格分离**：扫描阶段绝对零写动作；所有写动作必须经过 Plan → Exec 流水线。
4. **Manifest 是事实来源**：所有阶段产物都基于或生成 manifest；manifest 可独立审计、独立复用。
5. **分层渐进**：从目录层到 hash 层逐级加深，上层未命中冲突时下层不启动。

### 1.2 分层架构

```text
┌──────────────────────────────────────────────────────────────────┐
│  形态层（Shells）                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │  CLI (clap)  │  │ WebUI (axum) │  │ Desktop GUI (Tauri)    │  │
│  │  helichrysum │  │ helichrysum │  │ helichrysum-desktop    │  │
│  │              │  │   -web      │  │                        │  │
│  └──────┬───────┘  └──────┬───────┘  └───────────┬────────────┘  │
│         │                  │                       │               │
│         └──────────────────┴───────────────────────┘               │
│                            │                                       │
│                            ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  SDK 层（public Rust API）       helichrysum-core            │  │
│  │  Scope / Scanner / Analyzer / Reporter / Planner / Executor │  │
│  └─────────────────────────────┬───────────────────────────────┘  │
│                                │                                   │
│  ┌─────────────────────────────┴───────────────────────────────┐  │
│  │  平台抽象层 (helichrysum-fs)                                │  │
│  │  Windows / Linux / macOS 各自的 fs 操作、link 语义、路径规范化│  │
│  └─────────────────────────────┬───────────────────────────────┘  │
│                                │                                   │
│  ┌─────────────────────────────┴───────────────────────────────┐  │
│  │  存储层 (helichrysum-store)                                 │  │
│  │  SQLite (rusqlite) + 迁移 + manifest 仓库                   │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 1.3 Cargo Workspace 结构

```text
helichrysum/
├── Cargo.toml                    # workspace 根
├── crates/
│   ├── core/                     # SDK + 业务逻辑（lib + integration tests）
│   │   ├── src/
│   │   │   ├── scope.rs          # Scope 定义、规范化、判定
│   │   │   ├── scan/             # 扫描器（并发、断点续扫、增量）
│   │   │   ├── link/             # Link 处理（按平台分流）
│   │   │   ├── analyze/          # 关系分析（duplicate/sibling/archive）
│   │   │   ├── hash/             # 分层 hash 策略
│   │   │   ├── report/           # 报告生成（HTML / JSON / SQLite）
│   │   │   ├── plan/             # 处理计划生成与冲突检测
│   │   │   ├── exec/             # 执行器（Trash、Move、Link 替换）
│   │   │   ├── manifest/         # manifest schema + 迁移
│   │   │   └── lib.rs
│   │   └── tests/
│   ├── fs/                       # 平台抽象（feature-gated per platform）
│   │   ├── src/
│   │   │   ├── lib.rs
│   │   │   ├── unix/             # symlink/hardlink/mount detection
│   │   │   ├── windows/          # reparse point/junction/file id
│   │   │   └── macos/            # firmlink、APFS 特性
│   ├── store/                    # SQLite 封装、迁移、查询
│   │   └── src/
│   ├── cli/                      # 二进制：helichrysum（clap）
│   │   ├── src/
│   │   │   ├── commands/         # scan / analyze / report / plan / exec / ...
│   │   │   └── main.rs
│   │   └── Cargo.toml
│   ├── web/                      # 二进制：helichrysum-web（axum + 静态资源）
│   │   ├── src/
│   │   └── frontend/             # 前端项目（Vite + React/Svelte）
│   └── desktop/                  # Tauri 应用
│       ├── src-tauri/
│       └── src/                  # 与 web 共享前端
├── docs/
│   ├── requirements.md
│   ├── technical-design.md       # 本文档
│   ├── cli-reference.md          # (待补) CLI 命令详细文档
│   ├── manifest-schema.md        # (待补) Manifest schema
│   └── plugin-api.md             # (待补) 插件接口
└── examples/
```

---

## 2. 技术栈选型

### 2.1 核心语言：Rust

**理由：**

| 维度 | Rust 的契合度 |
|---|---|
| 跨平台三端等价 | 编译产物为各平台原生二进制；同一份代码三端跑 |
| 多盘海量文件扫描 | 性能与 C 等价；零成本抽象 + 所有权模型让并发安全 |
| Hash 性能 | `blake3` SIMD、`xxh3`、`sha2` 均为业内最快实现 |
| CLI / SDK 分离 | Cargo workspace 天然支持 lib + 多 bin 模式 |
| 桌面 GUI | Tauri 即 Rust 写的，与核心引擎无成本复用 |
| Web 服务 | `axum` 性能顶尖，类型安全 |
| 类型安全 | 借用检查 + 强类型，避免 `as any` / null 错误 |
| 参考实现 | `ripgrep` / `fd` / `dua` / `fclones` / `czkawka` 等同类工具均选 Rust |

**已考虑的替代方案与不选的理由：**

- **Go**：开发速度快，但 hardlink / junction / reparse point 的跨平台抽象库不如 Rust 成熟；桌面 GUI 生态薄弱（Wails / Fyne 不及 Tauri）。
- **Python**：开发速度快，但 hash 百万级文件性能瓶颈明显；分发依赖运行时，不利于"个人归档工具"的轻量部署。
- **TypeScript (Node/Bun)**：前后端统一，但大文件 hash 与目录遍历 IO 受 V8 限制；native 模块引入反而复杂化分发。

### 2.2 关键依赖

| 用途 | Crate | 选型理由 |
|---|---|---|
| CLI 框架 | `clap` v4 | 事实标准，derive 风格，子命令支持完善 |
| 目录遍历 | `ignore` / `jwalk` | `ignore` 来自 ripgrep，支持 gitignore 风格过滤；`jwalk` 提供并行遍历 |
| Hash | `blake3` / `xxhash-rust` / `sha2` | blake3 SIMD 极快且支持 rayon；xxh3 用于快速预筛；sha2 用于兼容场景 |
| 数据库 | `rusqlite` (bundled) | 嵌入式 SQLite，零外部依赖；`bundled` feature 静态链接 |
| 异步运行时 | `tokio` | axum 依赖；用于 WebUI 与并发 IO |
| Web 框架 | `axum` | 类型安全、与 tower 生态融合 |
| 序列化 | `serde` / `serde_json` | 标配 |
| 错误处理 | `thiserror` (库) + `anyhow` (bin) | 库精确错误，二进制简单聚合 |
| 日志 | `tracing` | 结构化日志 + span，远胜 `log` |
| 路径规范化 | `dunce` (Windows) + `std::fs::canonicalize` | `dunce` 解决 Windows UNC 路径问题 |
| Trash | `trash` crate | 跨平台回收站操作 |
| 压缩包读取 | `zip` / `sevenz-rust` / `tar` / `rar` (unrar) | 覆盖主流格式 |
| 桌面 GUI | `tauri` | Rust 写、复用核心、产物小 |

### 2.3 平台特有依赖

| 平台 | 用途 | 接入方式 |
|---|---|---|
| Windows | Reparse Point / Junction / File ID | `windows-sys` 调用 `GetFileInformationByHandleEx`、`FSCTL_GET_REPARSE_POINT` |
| Windows | 最终路径解析 | `GetFinalPathNameByHandleW` |
| Linux | Mount detection | 解析 `/proc/self/mountinfo` |
| Linux | inode / nlink | `lstat` / `stat` 字段 |
| macOS | firmlink / APFS | `libc::stat` + `getattrlist` |

---

## 3. 核心数据模型

### 3.1 Filesystem Object

```rust
/// 文件系统对象的唯一逻辑标识（在单次 manifest 内稳定）
pub struct ObjectId(pub u64);  // manifest 内自增 ID

/// 对象类型
pub enum ObjectKind {
    RegularFile,
    Directory,
    Symlink { target: PathBuf, broken: bool, circular: bool },
    HardlinkGroup { group_id: InodeGroupId },  // 引用 inode group
    WindowsJunction { target: PathBuf },
    ReparsePoint { tag: u32, target: Option<PathBuf> },
    MountPoint,
    Other,
}

/// 一次扫描得到的一个对象
pub struct FsObject {
    pub id: ObjectId,
    pub scope_id: ScopeId,
    pub path: PathBuf,            // 相对 scope 根的相对路径
    pub canonical_path: PathBuf,  // 规范化绝对路径
    pub kind: ObjectKind,
    pub size: Option<u64>,
    pub mtime: Option<SystemTime>,
    pub ctime: Option<SystemTime>,
    pub inode_group: Option<InodeGroupId>,  // hardlink 共享标识
    pub device_id: u64,
    pub scope_relation: ScopeRelation,       // InScope / OutOfScope / Broken / Circular
}

pub enum ScopeRelation {
    InScope,
    OutOfScope { target: PathBuf },
    Broken,
    Circular { cycle_path: Vec<ObjectId> },
}
```

### 3.2 Hash 分层

```rust
pub enum HashTier {
    /// 仅元数据（size + mtime + size class），不读内容
    Metadata,
    /// 快速 hash：仅文件头尾 + 中段采样 + size + mtime
    SampledHash(u64),  // xxh3
    /// 全量内容 hash
    FullHash(Blake3Hash),
}

pub struct HashRecord {
    pub object_id: ObjectId,
    pub tier: HashTier,
    pub computed_at: SystemTime,
    pub bytes_read: u64,  // 用于性能统计
}
```

### 3.3 Relation

```rust
pub struct Relation {
    pub id: RelationId,
    pub kind: RelationKind,
    pub members: Vec<ObjectId>,     // 参与关系的对象
    pub confidence: f32,             // 0.0 ~ 1.0
    pub evidence: Vec<Evidence>,     // 判定依据
}

pub enum RelationKind {
    ExactDuplicate,
    NearDuplicate,
    Renamed,
    Moved,
    StructuralSibling,
    ArchivePair(ArchivePairKind),
    LinkReference,
    PartialOverlap,
    Versioned,
}

pub enum ArchivePairKind {
    FullyExtracted,
    ModifiedAfterExtraction,
    PartialExtraction,
    Unrelated,
}

pub enum Evidence {
    HashMatch { algorithm: &'static str },
    SizeMatch,
    MtimeMatch { tolerance_secs: u64 },
    StructureSimilarity { score: f32 },
    ArchiveListing { entries_matched: usize, entries_total: usize },
}
```

### 3.4 Manifest Schema（SQLite）

```sql
-- 元数据表
CREATE TABLE manifest_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);  -- schema_version, created_at, scope_hash, helichrysum_version ...

-- Scope 定义
CREATE TABLE scopes (
    id          INTEGER PRIMARY KEY,
    root_path   TEXT NOT NULL,
    canonical   TEXT NOT NULL,
    added_at    INTEGER NOT NULL
);

-- 文件系统对象
CREATE TABLE objects (
    id              INTEGER PRIMARY KEY,
    scope_id        INTEGER NOT NULL REFERENCES scopes(id),
    path            TEXT NOT NULL,
    canonical_path  TEXT NOT NULL,
    kind            TEXT NOT NULL,        -- JSON-serialized ObjectKind
    size            INTEGER,
    mtime           INTEGER,
    ctime           INTEGER,
    inode_group     INTEGER,
    device_id       INTEGER NOT NULL,
    scope_relation  TEXT NOT NULL,
    UNIQUE (scope_id, path)
);
CREATE INDEX idx_objects_size ON objects(size) WHERE size IS NOT NULL;
CREATE INDEX idx_objects_inode ON objects(inode_group) WHERE inode_group IS NOT NULL;

-- Hash 记录
CREATE TABLE hashes (
    object_id    INTEGER PRIMARY KEY REFERENCES objects(id),
    tier         TEXT NOT NULL,
    hash_value   BLOB,
    bytes_read   INTEGER NOT NULL,
    computed_at  INTEGER NOT NULL
);

-- 关系
CREATE TABLE relations (
    id          INTEGER PRIMARY KEY,
    kind        TEXT NOT NULL,
    confidence  REAL NOT NULL,
    evidence    TEXT NOT NULL             -- JSON array
);
CREATE TABLE relation_members (
    relation_id INTEGER NOT NULL REFERENCES relations(id),
    object_id   INTEGER NOT NULL REFERENCES objects(id),
    role        TEXT,                     -- 在关系中的角色（如 archive / extracted_dir）
    PRIMARY KEY (relation_id, object_id)
);
CREATE INDEX idx_relation_members_obj ON relation_members(object_id);

-- 扫描任务状态（断点续扫）
CREATE TABLE scan_state (
    scope_id    INTEGER PRIMARY KEY,
    last_path   TEXT,
    status      TEXT,                     -- pending / running / paused / done / failed
    updated_at  INTEGER
);
```

**索引策略：**

- `size` 上的部分索引用于快速找"大小相同的候选"
- `inode_group` 上的部分索引用于 hardlink 去重
- `relation_members.object_id` 反向索引支持"这个对象参与了哪些关系"

---

## 4. 关键模块设计

### 4.1 Scope 模块

**职责：**

- 接受用户输入的根路径集合
- 对每条路径做 Canonical Path 解析（解析所有 symlink / 相对引用 / Windows 短名）
- 提供 `contains(path) -> ScopeCheck` 用于扫描时快速判定
- 处理 Scope 嵌套（`D:\` 包含 `D:\Sub`，应警告）
- 持久化 Scope 配置（命名、保存、加载）

**关键算法：** Scope 判定基于 Canonical Path 的前缀匹配，配合设备 ID 防止"同路径不同卷"绕过。

### 4.2 Scanner 模块

**职责：**

- 接收 Scope + 排除规则，输出 `FsObject` 流
- 并发遍历（基于 `jwalk` 或自实现的 work-stealing 队列）
- 处理 Link：见 4.3
- 检测 Mount Point 与设备边界（默认跨设备不递归）
- 检测循环引用（维护"路径栈"哈希集合）
- 断点续扫：定期 flush `scan_state`
- 增量追加：对比上次 manifest，仅扫描新增/修改对象

**并发模型：** 单生产者（目录遍历）多消费者（worker pool 处理文件元数据获取），输出 channel 写入 SQLite 批量事务。

**背压：** SQLite 写入跟不上扫描时，worker 必须阻塞而非丢失。

### 4.3 Link Handler

按平台分流，统一接口：

```rust
pub trait LinkInspector {
    /// 读取对象元数据，不跟随
    fn inspect(&self, path: &Path) -> Result<LinkInfo>;
}

pub struct LinkInfo {
    pub is_link: bool,
    pub link_kind: LinkKind,           // Symlink / Hardlink / Junction / ReparsePoint / None
    pub target: Option<PathBuf>,       // 不跟随得到的原始 target
    pub resolved_target: Option<PathBuf>,  // Canonical Path
    pub scope_relation: ScopeRelation,
    pub inode_group: Option<InodeGroupId>,
}
```

| 平台 | 实现 |
|---|---|
| Linux | `libc::lstat` + `readlink` + `/proc/self/mountinfo` |
| macOS | `libc::lstat` + `readlink` + `getattrlist`（识别 firmlink） |
| Windows | `GetFileAttributesEx` + `FSCTL_GET_REPARSE_POINT` + `FileIdInfo`（128-bit file id 作为 inode group） |

**关键策略：**

- Link 默认不跟随，仅记录 `(source, target, scope_relation)`
- Scope 内 Link：`scope_relation = InScope`，建立 `LinkReference` 关系，不重复扫描目标
- Scope 外 Link：`scope_relation = OutOfScope`，标记后跳过
- Broken Link：`scope_relation = Broken`
- 循环：维护"已访问 Canonical Path 集合"，命中即 `Circular`

### 4.4 分层 Hash 模块

**策略：**

```text
对每个 FsObject（仅 RegularFile，size > 0）：
  1. (Metadata tier) 入库 size + mtime + ctime
  2. 若与其他对象在 (size, mtime) 上碰撞 → 升级到 SampledHash
  3. 若 SampledHash 仍碰撞 → 升级到 FullHash
  4. 升级路径单调，永不回退
```

**SampledHash 设计：**

- 文件 < 64 KB：全量 xxh3
- 文件 ≥ 64 KB：头 16 KB + 中段 32 KB + 尾 16 KB 共 64 KB 采样

**FullHash：** blake3，支持 rayon 多线程，流式读取避免大文件内存爆炸。

### 4.5 Relation Analyzer

按关系类型组织为独立的 Detector，可并行调度：

```rust
pub trait RelationDetector {
    fn name(&self) -> &'static str;
    fn detect(
        &self,
        ctx: &AnalysisContext,
        emit: &mut dyn FnMut(Relation),
    ) -> Result<()>;
}
```

**核心 Detector：**

| Detector | 输入 | 算法 |
|---|---|---|
| `ExactDuplicateDetector` | 所有 FullHash | hash 相同 → 一组 |
| `HardlinkGroupDetector` | inode_group | 同一 inode group → 一组 |
| `RenamedDetector` | 同 size 同 hash 但不同路径 | hash + size 一致，路径不同 → 标 Renamed 或 Moved |
| `StructuralSiblingDetector` | 目录对象 | 目录结构相似度（Jaccard on 子路径集合）> 阈值 → 一组 |
| `ArchivePairDetector` | 压缩包 + 同名兄弟目录 | 见 4.6 |
| `NearDuplicateDetector`（可选） | 文本类文件 | 容忍 EOL/BOM 差异的归一化后 hash |
| `VersionedDetector`（可选） | 同名文件 | size 接近 + 内容部分相似 |

**StructuralSibling 相似度：**

- 对两个目录 A、B：取各自直接子项的相对路径集合
- 计算 Jaccard：`|A ∩ B| / |A ∪ B|`
- 阈值默认 0.7（可配置）
- 高相似度 → 推断为"同一套数据的演进"

### 4.6 Archive Pair Detector

**流程：**

```text
对于每个 .zip / .7z / .tar.gz / .rar 对象 X：
  1. 提取 X 内部文件清单（path, size, mtime）
  2. 在 X 所在目录寻找兄弟目录候选：
     - 同名（去扩展名）
     - 同名 + "-1" / "_extracted" / "副本" 等后缀
  3. 对每个候选目录 C：
     a. 取 C 内所有文件清单
     b. 计算 (X 清单, C 清单) 的匹配度
        - 完全匹配 → FullyExtracted
        - C 是 X 的子集 → PartialExtraction
        - C 有 X 之外的文件 → ModifiedAfterExtraction
        - 几乎不匹配 → Unrelated
     c. 若 FullyExtracted：
        - 比较目录内文件的 mtime 与 X 的 mtime
        - 若目录文件 mtime ≤ X mtime + 容差（默认 1 小时），高可信度
        - 标记 "建议清理 X（压缩包）"
```

**注意：**

- 提取压缩包清单只读取中央目录（zip central directory、7z header、tar 顺序读），不真正解压
- 大压缩包（>1 GB）的清单提取应异步、可取消
- 加密压缩包无法提取清单 → 标记为 `EncryptedArchive`，不参与配对分析

### 4.7 Report Builder

**输入：** manifest + 视图参数（筛选、排序、展开状态）

**输出形态：**

1. **HTML（可交互）**：内嵌 JSON 数据 + 轻量 JS（无外部依赖，可离线打开），目录树虚拟滚动，按问题类型筛选
2. **JSON（机器可读）**：与 manifest 同构，方便脚本二次处理
3. **SQLite 视图**：直接在 manifest 数据库上创建视图，可用任意 SQL 客户端查询

**报告聚合规则：**

- 目录节点的"问题数"= 子树内所有 `ExactDuplicate` 成员数 + `ArchivePair` 数 + OutOfScope Link 数 + Broken Link 数
- 目录节点的"状态"= 子树状态的并集（如子树存在重复，本目录标记为 `HasDuplicates`）

### 4.8 Plan Generator

**输入：** 用户在报告中的标记（Keep / Delete / MoveTo / ReplaceWithLink / Merge 等）+ manifest

**输出：** Plan 对象（可序列化）

```rust
pub struct Plan {
    pub id: PlanId,
    pub created_at: SystemTime,
    pub based_on_manifest: ManifestRef,  // 哈希指向 manifest
    pub actions: Vec<PlannedAction>,
    pub conflicts: Vec<Conflict>,
    pub rollback_info: Vec<RollbackEntry>,
}

pub enum PlannedAction {
    Keep { object_id: ObjectId },
    MoveToTrash { object_id: ObjectId },
    MoveTo { object_id: ObjectId, dest: PathBuf },
    Rename { object_id: ObjectId, new_name: String },
    ReplaceWithLink { object_id: ObjectId, link_to: ObjectId, link_kind: LinkKind },
    Merge { source_ids: Vec<ObjectId>, dest_dir: PathBuf, conflict_policy: MergeConflictPolicy },
}
```

**冲突检测：**

- 目标路径已存在（非计划中删除）
- 同一对象被多个 action 引用
- Hardlink 删除一个路径不影响其他路径（提示用户）
- 跨卷 Move 实际是 Copy + Delete（耗时与失败风险都更高）

### 4.9 Executor

**执行原则：**

1. **再次确认**：执行前必须弹出最终确认（CLI 强制交互 yes，WebUI/GUI 必须显式按钮）
2. **Trash 优先**：所有 `MoveToTrash` 走 `trash` crate；跨平台语义一致
3. **两阶段**（跨卷操作）：先 Copy 后 Delete，Copy 失败则不 Delete
4. **执行日志**：每步记 `(action, target, result, timestamp)`，写入 manifest
5. **可恢复**：中断后基于执行日志判断哪些已完成、哪些待重做

---

## 5. 三端形态

### 5.1 CLI

参考 `git` 的子命令风格，目标是让无 GUI 环境也能完成全流程：

```text
helichrysum scope add <path> [--name <name>]
helichrysum scope list
helichrysum scope remove <name>

helichrysum scan [--scope <name>] [--exclude <glob>]... [--resume]
helichrysum analyze [--tier metadata|sampled|full] [--detectors <name>]...

helichrysum report [--format html|json|sqlite] [--filter <expr>]... [--out <path>]
helichrysum report serve --port 8080   # 启动 WebUI

helichrysum plan new [--from-marks <file>]
helichrysum plan show <plan-id>
helichrysum plan edit <plan-id>
helichrysum plan validate <plan-id>
helichrysum plan dry-run <plan-id>

helichrysum exec <plan-id> [--yes]

helichrysum manifest list
helichrysum manifest inspect <manifest-id>
helichrysum manifest diff <m1> <m2>
```

**关键设计：**

- 所有命令接受 `--manifest <path>`（默认 `~/.helichrysum/current.sqlite`）
- 输出优先机器可读（`--json`），人类可读为默认
- 退出码语义明确（0 成功 / 1 用户错误 / 2 系统错误 / 130 中断）

### 5.2 SDK

`helichrysum-core` 的 public API：

```rust
use helichrysum::{Scope, Scanner, Analyzer, Reporter, Planner, Executor};

let scope = Scope::new()
    .with_root("/data/backup1")
    .with_root("/data/backup2")
    .with_exclude("**/.git/**")?;

let manifest = Scanner::new(scope)?.run()?;
let manifest = Analyzer::new(manifest)?.run_to_tier(HashTier::FullHash)?;

Reporter::new(&manifest)
    .with_filter(Filter::HasDuplicates.or(Filter::ArchivePair))
    .render_html("/tmp/report.html")?;

let plan = Planner::new(&manifest)
    .with_mark(...)
    .build()?;

Executor::new(&plan).confirm_interactive()?.run()?;
```

### 5.3 WebUI

**架构：** axum 起本地 HTTP 服务，前端用 Vite + 一个轻量框架（候选：Svelte / SolidJS / React），构建产物嵌入二进制（`rust-embed`）。

**职责：**

- 浏览 manifest（目录树虚拟滚动）
- 按问题类型筛选
- 在重复组、ArchivePair 内做标记（Keep / Delete / Move / ReplaceWithLink）
- 编辑 Plan、查看 dry-run 结果
- 文件预览（文本、图片、PDF）—— 通过 axum 路由转发到本地路径
- 配置管理（Scope、排除规则、并发度等）

**安全：**

- 默认仅监听 `127.0.0.1`，不暴露到网络
- 所有写操作（标记、执行 Plan）必须经过显式 API，不接受路径穿越

### 5.4 桌面 GUI（Tauri）

**复用 WebUI 的前端**，外壳换为 Tauri：

- 文件预览体验更好（可直接调用系统默认程序）
- 提供"在资源管理器中定位"按钮（Tauri 提供 shell 集成）
- 系统托盘常驻、通知进度
- 自动更新（Tauri Updater）

**Tauri vs 纯 Web 的取舍：**

| 维度 | Tauri | 纯 WebUI |
|---|---|---|
| 集成度 | 高（直接调系统 API） | 中（需后端转发） |
| 分发 | 一份安装包 | 跑 `helichrysum web serve` + 浏览器 |
| 启动 | 双击即用 | 需要打开浏览器 |
| 系统集成 | 强（托盘、通知） | 弱 |

**目标：** Windows 一等公民，macOS/Linux 作为 Tauri 自然延伸。

---

## 6. 性能策略

### 6.1 扫描并发

- 目录遍历用 `jwalk`（基于 `crossbeam` 的 work-stealing）
- 文件元数据获取的 worker pool 大小默认 = CPU 核数
- SQLite 写入用单 writer + 批量事务（每 N=1000 条提交一次）

### 6.2 Hash 优化

- 仅在元数据碰撞时升级 hash 层级（避免对全盘文件无差别 hash）
- blake3 启用 rayon 多线程
- 大文件流式读取（8MB buffer），避免内存占用
- Hash 结果缓存到 manifest，二次扫描直接命中

### 6.3 SQLite 调优

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA mmap_size = 268435456;  -- 256MB
PRAGMA cache_size = -200000;    -- 200MB
PRAGMA temp_store = MEMORY;
```

### 6.4 内存预算

- FsObject 流式产生，进 DB 后即丢弃
- Analyzer 阶段按 chunk 加载对象（如每次 10 万条），避免全量 in-memory
- Report HTML 用虚拟滚动，DOM 节点数上限 5 万

---

## 7. 安全性设计

### 7.1 只读 / 写入分离

- `Scanner`、`Analyzer`、`Reporter` 阶段：核心代码无任何写入文件系统的能力（仅写 manifest SQLite）
- `Executor` 是唯一可以修改用户文件系统的模块
- 平台抽象层的所有写操作集中在 `fs::write` 子模块，便于审计

### 7.2 Trash 优先

```rust
pub enum DeletionStrategy {
    Trash,        // 默认：走 trash crate
    Permanent,    // 必须显式指定，跳过 Trash
}
```

### 7.3 路径安全

- 所有用户输入路径先 Canonical Path 化
- `MoveTo` 目标必须在 Scope 或用户指定的归档目录内（防 path traversal）
- 跨设备移动必须显式确认

### 7.4 隐私

- 零网络请求（除可选的更新检查，需用户开启）
- Manifest 中可以选择不存储 hash（牺牲再次扫描速度）
- 提供 `helichrysum manifest purge` 清理所有元数据

---

## 8. 配置与默认值

### 8.1 配置文件位置

| 平台 | 路径 |
|---|---|
| Linux | `~/.config/helichrysum/config.toml` |
| macOS | `~/Library/Application Support/helichrysum/config.toml` |
| Windows | `%APPDATA%\helichrysum\config.toml` |

### 8.2 关键默认值

| 项 | 默认 | 说明 |
|---|---|---|
| `scan.follow_symlinks` | `false` | 默认不跟随 |
| `scan.cross_device` | `false` | 默认不跨设备 |
| `scan.worker_count` | CPU 核数 | 并发数 |
| `scan.batch_size` | 1000 | SQLite 批量提交大小 |
| `hash.default_tier` | `full` | 默认分析到 FullHash |
| `hash.sampled_threshold_bytes` | 65536 | SampledHash 启用阈值 |
| `relation.structural_sibling_threshold` | 0.7 | Jaccard 阈值 |
| `relation.archive_mtime_tolerance_secs` | 3600 | 压缩包配对 mtime 容差 |
| `exec.deletion_strategy` | `trash` | 默认走回收站 |
| `exec.cross_volume_confirm` | `true` | 跨卷操作必须确认 |
| `web.listen` | `127.0.0.1:0` | 仅本地，随机端口 |
| `ignore.builtin_dirs` | 见下 | 内置忽略列表 |

**内置忽略目录默认值：**

```text
$RECYCLE.BIN
System Volume Information
.git/objects
.svn
.hg
node_modules
__pycache__
.DS_Store
Thumbs.db
```

---

## 9. 阶段路线图

### Phase 0：基础设施（2 周）

- Cargo workspace 骨架
- `helichrysum-fs` 平台抽象（Linux + macOS + Windows 基础元数据）
- `helichrysum-store` SQLite 迁移系统
- CI（Windows / Linux / macOS 三平台构建）

### Phase 1：扫描 + 报告 MVP（3 周）

- Scope 管理（CLI）
- Scanner 基础版（单线程，识别 regular file + directory + symlink）
- Manifest 持久化
- ExactDuplicateDetector（仅元数据 + FullHash）
- CLI: `scope` / `scan` / `analyze` / `report`
- HTML 报告基础版（目录树 + 重复组高亮）

**交付能力：** 命令行完成一次"找出所有完全重复文件"的端到端流程。

### Phase 2：Link 完整支持 + 分层 hash（2 周）

- Hardlink / Junction / Reparse Point 识别
- Mount Point 边界检测
- SampledHash 层
- 循环引用检测
- OutOfScope / Broken / Circular 标记

### Phase 3：关系分析扩展（3 周）

- StructuralSiblingDetector
- ArchivePairDetector（zip / 7z / tar.gz / rar）
- Renamed / Moved detector
- NearDuplicate（文本类，可选）

### Phase 4：Plan + Executor（2 周）

- Plan 数据模型
- Plan 生成（从标记）
- 冲突检测
- Executor（Trash / Move / ReplaceWithLink）
- 执行日志 + 恢复
- 两阶段跨卷操作

### Phase 5：WebUI（3 周）

- axum 后端
- 目录树虚拟滚动
- 标记管理
- Plan 编辑器
- 文件预览（文本 / 图片 / PDF）
- 配置 UI

### Phase 6：桌面 GUI（2 周）

- Tauri 外壳
- 系统集成（资源管理器定位、托盘）
- 自动更新

### Phase 7：增量与性能（持续）

- 增量扫描
- Hash 缓存
- Profile 驱动优化
- 大规模数据集（100 万+ 文件）调优

**总计目标：** v1.0 在 ~17 周内交付完整三端能力；Phase 1 MVP 在 5 周内可用。

---

## 10. 风险与未决问题

| 风险 | 影响 | 缓解 |
|---|---|---|
| Windows Reparse Point 种类繁多 | 误识别为 Symlink / Junction | 只处理已知类型，未知类型标记 `UnknownReparsePoint` 并保留原始数据 |
| 压缩包格式（特别是 RAR）闭源 | 解析依赖有限 | 用 `unrar` 系统 binary 或 `rar` crate，失败时降级为"无法读取清单" |
| 大文件 hash 的 IO 瓶颈 | 全盘 hash 缓慢 | 分层 hash + 仅对碰撞项升级 |
| Tauri 在 Linux 的集成体验 | 桌面 GUI 体验下降 | Linux 上首推 WebUI，Tauri 作为可选 |
| 同一份数据在不同时期有真实差异 | 整合困难 | `Versioned` 关系标记后必须人工确认，绝不自动合并 |
| 跨卷 Move 的原子性 | 中断后状态不一致 | 两阶段 + 执行日志，可恢复 |

---

## 11. 未决问题（需要后续讨论）

1. **配置是否使用 TOML 还是 YAML？** 倾向 TOML（与 Cargo 一致），但 YAML 在配置嵌套结构上更灵活。
2. **Manifest 是否需要分布式 / 多机协作？** v1.0 单机即可，但 schema 设计要预留。
3. **WebUI 前端框架选型**：Svelte / SolidJS / React？倾向 Svelte（包体积小、虚拟滚动生态成熟）。
4. **插件系统是否需要 v1.0 就支持？** 倾向 Phase 7+ 再考虑，先用 trait 接口预留扩展点。
5. **是否提供 `helichrysum verify` 命令**用于事后验证归档完整性（hash 重算 + manifest 对比）？强烈建议有。
6. **Canonical Archive 的目标结构是否需要工具辅助规划？** 还是仅作为 Plan 输出物？
