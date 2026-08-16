# 切片 Resolve：处理决策模型落地

**状态：⏳ 计划中**

---

## 一、实现目标

将 F-Resolve-1~17 的决策模型从设计落地为代码：对每组重复/关系输出 **Equality / Compatibility / Conflict** 三态标签，并写入 manifest（relations 表），供报告展示与 Plan 消费。

**验收命令：**
```bash
helichrysum scan --scope tests/fixtures/backup1,tests/fixtures/backup2
helichrysum analyze --tier full
# JSON 报告中的 relations 应带 resolution 字段（equality/compatibility/conflict）
# fixture 中 readme.txt（内容相同）→ equality；加入"旧包含新"场景 → compatibility
```

---

## 二、任务计划与功能点

### 任务 1：Resolution 模型
**依赖：** 无
**功能点：**
- 1a. `ResolutionKind` 枚举：Equality / Compatibility / Conflict / Unknown
- 1b. `ResolutionResult` 记录：Kind + Confidence + Evidence（判定依据）
- 1c. 模型与 manifest `relations` 表对齐（kind 字段可存 resolution）

### 任务 2：Resolution 判定器（核心）
**依赖：** 任务 1
**功能点：**
- 2a. **Equality 判定**：同 hash 文件组 → Equality（内容完全一致，自动去重候选）
- 2b. **Compatibility 判定**（文件级）：旧内容 ⊆ 新内容（逐字包含）→ 以新为准
- 2c. **Conflict 判定**：内容不同且互不包含 → 人工
- 2d. **证据链采集**：HashMatch / SizeMatch / ContentContainment / Confidence

### 任务 3：目录级 Compatibility
**依赖：** 任务 2
**功能点：**
- 3a. 目录文件集合 ⊆ 检查（旧目录文件全在新目录中且内容一致）
- 3b. 输出目录级 Resolution（MergedProvider / ContainsConflict）

### 任务 4：Resolution 写入 manifest + 报告展示
**依赖：** 任务 1-3
**功能点：**
- 4a. Resolution 结果写入 `relations` 表（kind + confidence + evidence）
- 4b. ReportBuilder 输出 resolution 字段（JSON + HTML）

---

## 三、TDD 测试用例

### 测试 1：ResolutionModelTests（任务 1）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Resolution_Equality_ForIdenticalHashes` | 同 hash → Equality | F-Resolve-1 |
| `Resolution_Assertion` | 模型可序列化/反序列化 | F-Resolve-1 |

### 测试 2：EqualityDetectionTests（任务 2a）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Equality_IdenticalFiles_Grouped` | 相同内容 → Equality 组 | F-Resolve-2 |
| `Equality_Evidence_ContainsHashMatch` | Equality 有 HashMatch 证据 | F-Resolve-2 |

### 测试 3：CompatibilityDetectionTests（任务 2b/2c）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Compatibility_OldContent_ContainedInNew` | 旧内容逐字 ⊆ 新内容 → Compatibility | F-Resolve-3 |
| `Compatibility_Reverse_NotDetected` | 新 ⊆ 旧 → 不判 Compatibility（方向反了） | F-Resolve-3 |
| `Conflict_DifferentContent_NotCompatible` | 内容互不包含 → Conflict | F-Resolve-5 |

### 测试 4：DirectoryCompatibilityTests（任务 3）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Directory_OldFilesSubsetOfNew_Compatible` | 旧目录文件 ⊆ 新目录 → 目录级 Compatibility | F-Resolve-3 |
| `Directory_OldHasUniqueFile_NotCompatible` | 旧目录有独有文件 → 非兼容（人工） | F-Resolve-5 |

### 测试 5：ResolutionPersistenceTests（任务 4）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Resolution_WrittenToManifest` | resolution 写进 relations 表，可查询 | F-Resolve-1 |
| `Report_ContainsResolution` | JSON 报告含 resolution 字段 | F-Resolve-7 |

---

## 四、模块依赖

```
任务 1（Resolution 模型）
   ↓
任务 2（Equality / Compatibility / Conflict 判定）
   ↓
任务 3（目录级 Compatibility）
   ↓
任务 4（manifest 持久化 + 报告展示）
   ↓
端到端验收：analyze 后报告带 resolution
```

---

## 五、设计决策

| 决策 | 结论 |
|---|---|
| Equality 判定 | 已存的 SHA256 hash 分组即 Equality 候选（最廉价） |
| Compatibility 文件级 | 文本类逐字包含（旧 ⊆ 新）；二进制降级 Unknown |
| 输出位置 | 复用 `relations` 表，不加新表（kind 字段存 resolution 值） |
| 报告展示 | relations 输出带 `resolution` 字段 |