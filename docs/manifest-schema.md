# Manifest Schema 规范

| 项目 | 内容 |
|---|---|
| 文档状态 | v0.1（Draft，与代码同步） |
| 实现位置 | `src/Helichrysum.Core/Manifest/ManifestRepository.cs` |
| 存储 | SQLite（Microsoft.Data.Sqlite） |
| 数据库文件 | `~/.helichrysum/manifests/<name>.sqlite`（默认 `default.sqlite`） |

> Manifest 是 Helichrysum 的**唯一事实来源**（Single Source of Truth）：所有扫描结果、关系、标签、执行状态都持久化于此。报告、计划、执行器均基于 manifest 工作。

---

## 1. 数据库配置（PRAGMA）

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -200000;      -- 200 MB
PRAGMA temp_store = MEMORY;
PRAGMA foreign_keys = OFF;        -- 应用层维护引用一致性，不使用数据库外键强制
```

连接串固定 `Pooling=False`（避免 Windows 上连接池持有文件句柄导致删除失败）。

---

## 2. 版本管理

```sql
CREATE TABLE IF NOT EXISTS _schema_version (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL
);
```

打开数据库时：

```text
读取 MAX(version) → currentVersion
  currentVersion < 1  → 执行 ApplyV1Schema() → SetSchemaVersion(1)
  currentVersion == 当前实现 → 正常使用
  currentVersion > 当前实现 → 报错"此 manifest 更新，请升级工具"
```

**一次性轻量化约定（F-Report-11）**：schema 演进采用硬编码 if 分支迁移（`if (v<2) upgradeToV2()`），不引入迁移框架。开发期 schema 变动直接删库重扫；迁移函数在发布前补齐。

---

## 3. 表的完整定义

### 3.1 `_manifest_meta` — 快照元数据

```sql
CREATE TABLE IF NOT EXISTS _manifest_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

已使用键：

| 键 | 写入时机 | 示例值 |
|---|---|---|
| `created_at` | scan 完成 | `2026-08-17T09:52:44.282Z` |
| `tool_version` | scan 完成 | `v0.1.0` |
| `snapshot_age` | 报告生成时计算（不持久化） | `"刚刚" / "2 天前"` |
| `last_plan_executed` | exec 完成 | `plan-id` |
| `last_execution_at` | exec 完成 | ISO 8601 |

`created_at` 用于报告快照年龄展示（F-Report-12）。

### 3.2 `scopes` — 扫描范围

```sql
CREATE TABLE IF NOT EXISTS scopes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    root_path TEXT NOT NULL,
    canonical TEXT NOT NULL,
    added_at TEXT NOT NULL
);
```

| 列 | 说明 |
|---|---|
| `root_path` | 用户输入的原路径 |
| `canonical` | 规范化绝对路径（解析 symlink 后） |

### 3.3 `objects` — 文件系统对象

```sql
CREATE TABLE IF NOT EXISTS objects (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scope_id INTEGER NOT NULL REFERENCES scopes(id),
    path TEXT NOT NULL,
    canonical_path TEXT NOT NULL,
    kind TEXT NOT NULL,
    size INTEGER,
    mtime TEXT,
    ctime TEXT,
    inode_group INTEGER,
    device_id INTEGER NOT NULL,
    scope_relation TEXT NOT NULL
);
```

| 列 | 说明 | 取值 |
|---|---|---|
| `kind` | 对象类型 | `RegularFile` / `Directory` / `Symlink` |
| `size` | 字节数（目录为 NULL） | |
| `mtime` / `ctime` | ISO 8601 字符串 | |
| `inode_group` | 硬链接分组的 inode（跨越设备时为 NULL） | |
| `device_id` | 所在设备 | |
| `scope_relation` | 与 Scope 的关系 | `InScope` / `OutOfScope` / `Broken` / `Circular` / `Removed`（F-Exec-5 执行后标记） |

**已知缺陷（2016-08-17）**：模型 `FilesystemObject` 含 `LinkTarget` / `ResolvedLinkTarget` 属性，但 `objects` 表**缺少对应列**——Symlink 的目标信息（source→target 映射）当前未持久化。影响：报告/计划的 Link 语义只保留 `scope_relation` 分类，无法追溯具体目标路径。修复需升级 schema（`v2` 增加 `link_target` / `resolved_link_target` 列）。

**索引：**

```sql
CREATE INDEX IF NOT EXISTS idx_objects_size
    ON objects(size) WHERE size IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_objects_inode
    ON objects(inode_group) WHERE inode_group IS NOT NULL;
```

### 3.4 `hashes` — 内容指纹

```sql
CREATE TABLE IF NOT EXISTS hashes (
    object_id INTEGER PRIMARY KEY REFERENCES objects(id),
    tier TEXT NOT NULL,
    hash_value TEXT,
    bytes_read INTEGER NOT NULL,
    computed_at TEXT NOT NULL
);
```

| 列 | 说明 |
|---|---|
| `tier` | 摘要层级：`FullHash`（当前仅实现此层） |
| `hash_value` | SHA256 十六进制（小写） |
| `bytes_read` | 计算时读取的字节数 |
| `computed_at` | 计算时间 |

对每个文件：扫描阶段仅存元数据；analyze 阶段计算 SHA256 存入。`hash_value IS NULL` 表示尚未计算（元数据层）。

### 3.5 `relations` + `relation_members` — 关系

```sql
CREATE TABLE IF NOT EXISTS relations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    kind TEXT NOT NULL,
    confidence REAL NOT NULL,
    evidence TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS relation_members (
    relation_id INTEGER NOT NULL REFERENCES relations(id),
    object_id INTEGER NOT NULL REFERENCES objects(id),
    role TEXT,
    PRIMARY KEY (relation_id, object_id)
);

CREATE INDEX IF NOT EXISTS idx_relation_members_obj
    ON relation_members(object_id);
```

| 列 | 说明 |
|---|---|
| `kind` | 关系类型：`ExactDuplicate`（当前唯一写入）；未来扩展 `ArchivePair` / `StructuralSibling` 等 |
| `confidence` | 置信度 0.0 ~ 1.0 |
| `evidence` | JSON 字符串（`EvidenceEntry` 数组序列化） |
| `role` | 成员在关系中的角色（保留字段） |

`evidence` 示例：

```json
[
  { "Type": "HashMatch", "Details": "SHA256" },
  { "Type": "SizeMatch", "Details": "1024" }
]
```

### 3.6 `scan_state` — 断点续扫

```sql
CREATE TABLE IF NOT EXISTS scan_state (
    scope_id INTEGER PRIMARY KEY,
    last_path TEXT,
    status TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
```

| 列 | 说明 |
|---|---|
| `status` | `pending` / `running` / `done`（实际实现以 running/done 为主） |

当前实现：`SaveScanState` / `GetScanState` 已具备，扫描器断点续扫为切片 9 的持续优化项。

---

## 4. 查询面（ManifestRepository 公开 API）

| 方法 | 用途 |
|---|---|
| `Open(path)` | 打开/创建数据库，应用 PRAGMA + 版本迁移 |
| `GetSchemaVersion()` | 当前 schema 版本 |
| `InsertObject(FilesystemObject)` | 单个对象插入，返回 ID |
| `BatchInsertObjects(IEnumerable<FilesystemObject>)` | 批量插入（单事务） |
| `InsertHash(HashRecord)` | 写入内容指纹 |
| `GetObjectById(long)` | 按 ID 查对象 |
| `GetAllFiles()` | 全部 RegularFile 对象 |
| `GetDirectoryTree()` | 目录 → 直接文件数聚合（报告目录树用） |
| `QueryObjectsBySize(long)` | 按大小查（重复检测候选） |
| `QueryObjectsByHash(string)` | 按 hash 查对象 |
| `GetDuplicateGroups()` | 重复组（同 hash ≥2 成员） |
| `GetHashByObjectId(long)` | 对象指纹 |
| `InsertRelation(Relation, List<long>)` | 关系 + 成员（事务） |
| `MarkObjectRemoved(long)` | F-Exec-5：执行后标记已归档 |
| `SaveScanState / GetScanState` | 断点续扫状态 |
| `SetManifestMeta / GetManifestMeta` | 快照元数据 KV |

---

## 5. 与代码的同步约定

- **本文档是 schema 的唯一规范参考**；修改 schema 时必须同步此文档
- schema 变更需更新 `_schema_version` + 在 `Initialize()` 加 `if (currentVersion < N) ApplyVnSchema()` 分支
- 已知缺陷（link_target 列缺失）修复时：升 v2 schema + 文档同步 + 一次性迁移（对旧库的 Symlink 对象重扫补 link 信息或标 NULL）

---

## 6. 待办

- [ ] **v2 schema**：`objects` 表增加 `link_target` / `resolved_link_target` 列（修复模型-schema 不一致）
- [ ] 断点续扫真正接入 Scanner（当前 scan_state 表已建，扫描器尚未消费）
- [ ] `uploaded_at` / 更多 meta 键（按需）