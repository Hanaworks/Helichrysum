# 切片 3 实施计划：分层 Hash

**状态：⏳ 计划中**

---

## 一、实现目标

实现分层 hash 升级策略：从最轻量开始，仅在必要时升级到更昂贵的 hash 层级。避免无差别全盘 hash。

**验收命令：**
```bash
# 全量测试通过
dotnet test
# 手动验证：对 fixture 文件分别测试各层级 hash 结果一致
```

---

## 二、任务计划与功能点

### 任务 1：SampledHash（采样 hash）
**依赖：** 已有的 HashService（CRC32、SHA256）
**功能点：**
- 1a. `SampledHash` 方法：读取头 16KB + 中段 32KB + 尾 16KB，用 xxhash 合并
- 1b. 文件 < 64KB 时全量读取（不采样）
- 1c. 采样结果作为升级路径的中间层

### 任务 2：HashTier 升级策略
**依赖：** 任务 1
**功能点：**
- 2a. `HashTier` 三态：`Metadata`（仅 size+mtime，不读内容）、`SampledHash`（部分读取）、`FullHash`（SHA256）
- 2b. 升级路径：`(size,mtime)` 不碰撞 → 停在 Metadata；碰撞 → SampledHash；采样仍碰撞 → FullHash
- 2c. 单调升级：永不回退（一旦升级到 FullHash，后续不再降级）
- 2d. hash 缓存：manifest 持久化，二次扫描命中直接读取

### 任务 3：摘要分层判定
**依赖：** 任务 2
**功能点：**
- 3a. 文件相等判定：CRC32 快速预筛 → MD5/SHA256 确认（F-Layered-6）
- 3b. CIDR32 不同 → 直接判不等（不触发 SHA256）

---

## 三、TDD 测试用例

### 测试 1：SampledHashTests

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `SampledHash_SmallFile_ReadsAll` | 文件 < 64KB → 实际读取字节数 == 文件大小 | F-Layered-1 |
| `SampledHash_LargeFile_ReadsPartial` | 文件 ≥ 64KB → 实际读取字节数 ≈ 64KB（头+中+尾） | F-Layered-1 |
| `SampledHash_TwoIdenticalFiles_Matches` | 相同文件 → SampledHash 一致 | F-Layered-1 |
| `SampledHash_TwoDifferentFiles_Differs` | 不同文件 → SampledHash 不同 | F-Layered-1 |

### 测试 2：HashTierUpgradeTests

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Upgrade_MetadataMatch_TriggersSampled` | size+mtime 相同 → 触发 SampledHash | F-Layered-2 |
| `Upgrade_MetadataMismatch_StopsAtMetadata` | size/mtime 不同 → 停在 Metadata，不触发 hash | F-Layered-2 |
| `Upgrade_SampledMatch_TriggersFull` | SampledHash 相同 → 触发 SHA256 | F-Layered-2 |

### 测试 3：HashCacheTests

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Cache_SameHash_ReturnsCached` | 同一文件二次查询 → 返回缓存 hash | F-Layered-3 |
| `Cache_DifferentHash_ComputesNew` | 不同文件 → 计算新 hash 并缓存 | F-Layered-3 |

---

## 四、模块依赖

```
HashService（已有：CRC32 + SHA256）
    ↓
SampledHash（新增：头/中/尾采样）
    ↓
HashTier 升级策略（新增：Metadata → Sampled → Full）
    ↓
摘要分层判定（新增：CRC32 预筛 → SHA256 确认）
    ↓
测试全部通过
```