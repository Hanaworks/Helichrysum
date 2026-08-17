# 切片 1 实施计划：扫描 → manifest → 重复 → 报告

**状态：✅ 已完成（回溯补写）**

## 一、实现目标

CLI 能对指定两个目录完成"扫描 → 找完全重复 → 输出报告"的端到端流程，且所有核心逻辑均通过 TDD 验证。

**验收命令：**
```bash
helichrysum scope add tests/fixtures/backup1 --name backup1
helichrysum scope add tests/fixtures/backup2 --name backup2
helichrysum scan --scope backup1,backup2
helichrysum analyze --tier full
helichrysum report --format html --out report.html
# 打开 report.html 应看到 backup1/backup2 的重复组
```

---

## 二、任务计划与功能点

### 任务 1：Manifest 仓库（SQLite 持久化）
**依赖：** 无（基础）
**功能点：**
- 1a. 数据模型：FilesystemObject / HashRecord / Relation / ManifestMeta
- 1b. ManifestRepository：打开/创建 SQLite 库，建表（scopes / objects / hashes / relations / relation_members / scan_state / _schema_version / _manifest_meta）
- 1c. 版本管理：启动时校验版本号，旧版本执行硬编码 if 升级（v1 初始建表）
- 1d. 批量写入：BatchInsertObjects / BatchInsertHashes / InsertRelation
- 1e. 查询：QueryObjectsBySize / QueryObjectsByHash / GetDuplicateGroups
- 1f. 快照元数据：created_at / scanned_at / scope_snapshot / tool_version
- 1g. 断点续扫：SaveScanState / GetScanState

### 任务 2：Scope 模型（扫描范围）
**依赖：** 无
**功能点：**
- 2a. ScopeConfiguration：根路径集合 + 排除规则（glob）
- 2b. CanonicalPath：解析路径为规范绝对路径
- 2c. Contains：判定路径是否在 Scope 内（前缀匹配）
- 2d. IsExcluded：判定路径是否匹配排除规则

### 任务 3：Scanner 基础版（文件遍历）
**依赖：** 任务 2
**功能点：**
- 3a. 递归遍历目录，输出 FilesystemObject 流
- 3b. 识别 RegularFile / Directory / Symlink（不跟随）
- 3c. 排除规则过滤
- 3d. 韧性隔离：权限拒绝/坏文件跳过不崩（F-Scan-11）
- 3e. 进度回调（IProgress<ScanProgress>）
- 3f. 并行度控制（Parallel.ForEachAsync）

### 任务 4：分层 Hash 服务
**依赖：** 无（工具类）
**功能点：**
- 4a. CRC32 快速摘要（预筛，System.IO.Hashing）
- 4b. SHA256 强摘要（确认，内置）
- 4c. 升级策略：size+mtime 不碰撞 → 不 hash；碰撞 → CRC32；CRC32 相同 → SHA256 确认
- 4d. HashRecord 产出

### 任务 5：ExactDuplicate 检测器
**依赖：** 任务 1 + 任务 4
**功能点：**
- 5a. Size 索引 → 找 size 相同的候选组
- 5b. 候选组内 hash 比对 → 分成 ExactDuplicate 组
- 5c. 输出 Relation（kind = ExactDuplicate，confidence = 1.0，evidence = 判定依据）

### 任务 6：JSON 报告生成
**依赖：** 任务 1
**功能点：**
- 6a. 从 ManifestRepository 读取重复组数据
- 6b. 输出 JSON（含目录结构摘要 + 重复组清单 + 标签统计）

### 任务 7：HTML 报告生成
**依赖：** 任务 6
**功能点：**
- 7a. 单文件自包含 HTML
- 7b. 目录树（聚合统计，仅首层展开）
- 7c. 重复组清单（路径 / size / hash / 置信度）
- 7d. 精简投影（≤20MB 目标）

### 任务 8：CLI 命令
**依赖：** 任务 1-7
**功能点：**
- 8a. `scope add <path> [--name <name>]` / `scope list`
- 8b. `scan --scope <name>`（扫描 + 自动写入 manifest）
- 8c. `analyze --tier full|sampled|metadata`（执行 hash + 重复检测）
- 8d. `report --format html|json --out <path>`（输出报告）

---

## 三、TDD 测试用例

### 测试 1：ScopeTests（任务 2）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Scope_Contains_AcceptsPathWithinRoot` | 构造 Scope 含 `/data/backup1`，传 `/data/backup1/report.docx` → `Contains() == true` | F-Scope-1 |
| `Scope_Contains_RejectsPathOutsideRoot` | 传 `/other/path/file.txt` → `Contains() == false` | F-Scope-1 |
| `Scope_ExcludePattern_ExcludesMatchingPath` | 排除 `*.tmp`，传 `file.tmp` → `IsExcluded() == true` | F-Scope-2 |
| `Scope_CanonicalPath_ResolvesCorrectly` | 传相对路径 → 返回 `Path.GetFullPath` 后的规范绝对路径 | F-Scope-4 |
| `Scope_MultipleRoots_AllPathsAccepted` | 两个根路径，各自子路径均 `Contains() == true` | F-Scope-1 |

### 测试 2：ScanningTests（任务 3）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Scanner_CountsFiles_Correctly` | 扫 fixture backup1（2 文件）→ 产出 2 个 FilesystemObject | F-Scan-1 |
| `Scanner_DetectsSymlink_WithoutFollowing` | fixture 中有 symlink → 产出 Link 类型对象，LinkTarget 正确 | F-Scan-1 / F-Link-1 |
| `Scanner_HandlesAccessDenied_Gracefully` | 构造无权限子目录 → 扫描不崩，跳过该目录，剩余文件完整 | F-Scan-11 |
| `Scanner_RespectsExcludePattern` | 排除 `*.txt` → 扫 backup1 不产出 .txt 文件 | F-Scan-1 / F-Scope-2 |
| `Scanner_ReportsProgress` | 扫描过程中 Progress 回调被调用至少一次 | F-Scan-8 |

### 测试 3：HashTests（任务 4）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Hash_Crc32_TwoIdenticalFiles_Matches` | backup1/readme.txt = backup2/readme.txt → CRC32 一致 | F-Layered-6 |
| `Hash_Crc32_TwoDifferentFiles_Differs` | backup1/notes.txt ≠ backup2/notes.txt → CRC32 不同 | F-Layered-6 |
| `Hash_Sha256_ConfirmsCrc32Match` | CRC32 相同 → SHA256 也相同 | F-Layered-6 |
| `Hash_Upgrade_MetadataCollisionTriggersCrc32` | size+mtime 相同才触发 hash，不同不触发 | F-Layered-1 |

### 测试 4：DuplicateDetectionTests（任务 5）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Duplicate_IdenticalFiles_Grouped` | backup1/readme.txt = backup2/readme.txt → 同一 ExactDuplicate 组 | F-Relation-1 |
| `Duplicate_DifferentFiles_NotGrouped` | backup1/notes.txt ≠ backup2/notes.txt → 不在同一组 | F-Relation-1 |
| `Duplicate_Group_HasCorrectEvidence` | 重复组 Confidence = 1.0，Evidence 含 HashMatch + SizeMatch | F-Relation-7 |

### 测试 5：ReportTests（任务 6/7）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Report_Json_ContainsDuplicateGroups` | JSON 报告含 `"relations"` 数组，每组有 members、confidence | F-Report-6 |
| `Report_Json_ScopeSummary_Exists` | JSON 报告含 scope 摘要（总文件数、总大小、问题数） | F-Report-2 |
| `Report_Html_Generated_And_Valid` | HTML 报告是有效 HTML，含 `<html>` 标签，且文件大小 < 5MB | F-Report-6 |

---

## 四、模块依赖关系图

```
任务 2（Scope）    任务 4（Hash）
      ↘               ↙
任务 3（Scanner）→  任务 1（Manifest）← 任务 5（Duplicate）
                          ↙               ↘
                   任务 6（JSON 报告）      任务 7（HTML 报告）
                          ↙               ↘
                         任务 8（CLI 命令）
                          ↓
                  端到端验收（fixture 跑通）
```

---

## 五、设计决策（已定）

| 决策 | 结论 |
|---|---|
| Manifest 路径 | `~/.helichrysum/manifests/` 自动管理，CLI 可 `--manifest` 覆盖 |
| 报告 HTML 模板 | 内嵌在代码中（字符串渲染，零外部依赖） |
| CLI 子命令风格 | `helichrysum <verb> <noun>` 风格（`scope add` / `scan` / `analyze` / `report`） |
| 测试框架 | xUnit + 应断言 |