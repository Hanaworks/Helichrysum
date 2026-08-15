# 切片 2 实施计划：Link 完整处理

**状态：⏳ 计划中**

---

## 一、实现目标

CLI 扫描时能正确识别所有主流 link 类型（symlink、hardlink、junction、reparse point），按 Scope 分流（InScope / OutOfScope / Broken / Circular），不重复统计 hardlink 内容，不扩大 Scope。

**验收命令：**
```bash
# 在 fixture 中构造各类 link 场景后验证
helichrysum scan --scope tests/fixtures/links
helichrysum report --format json
# 检查 JSON 报告中: out_of_scope_link → OutOfScope; broken_link → Broken; circular → Circular
```

---

## 二、任务计划与功能点

### 任务 1：Link 检测基础设施（Helichrysum.Filesystem）
**依赖：** 无
**功能点：**
- 1a. `ILinkInspector` 接口：`Inspect(string path) → LinkInfo`
- 1b. `LinkInfo` 记录：`IsLink`, `LinkKind` (Symlink/Hardlink/Junction/ReparsePoint/None), `Target`, `ResolvedTarget`, `ScopeRelation`, `InodeGroup`
- 1c. Linux 实现：`lstat` + `readlink` + `/proc/self/mountinfo`（P/Invoke）
- 1d. macOS 实现：`lstat` + `readlink` + `getattrlist`（P/Invoke）
- 1e. Windows 实现：`ReparsePoint` + `FileIdInfo`（P/Invoke，CsWin32 或手动）

### 任务 2：Link 分流逻辑（Helichrysum.Core）
**依赖：** 任务 1
**功能点：**
- 2a. Symlink 不跟随，仅记录 (source, target, scope_relation)
- 2b. Link 目标分流处理：
  - 目标在 Scope 内 → Resolve 到对象，建立 LinkReference，不重复扫描
  - 目标在 Scope 外 → 标记 `OutOfScope`，不递归、不统计
  - 目标不存在 → 标记 `Broken`
  - 目标形成环 → 标记 `Circular`（已访问 Canonical Path 集合检测）
- 2c. Hardlink：同一 inode group 只统计一次内容，不重复
- 2d. Canonical Path 防绕过（symlink 链解析后判 Scope）

### 任务 3：Scanner 集成（Helichrysum.Core.Scanning）
**依赖：** 任务 2
**功能点：**
- 3a. Scanner 集成 LinkInspector，扫描时同步检测 link 类型
- 3b. 产出对象时填充 `LinkTarget` / `ResolvedLinkTarget` / `ScopeRelation`
- 3c. 循环引用检测（已访问 Hash Set）
- 3d. Mount Point 边界（默认不跨设备扫描）

---

## 三、TDD 测试用例

### 测试 1：LinkInspectorTests（任务 1，跨平台）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `LinkInspector_Symlink_DetectsTarget` | fixture 中 symlink 指向正确 target | F-Link-1 |
| `LinkInspector_BrokenSymlink_MarkedBroken` | 目标不存在的 symlink → IsLink=true, ScopeRelation=Broken | F-Link-2 |
| `LinkInspector_Hardlink_SameInode` | 同一文件的 hardlink → 相同 inode group ID | F-Link-3 |
| `LinkInspector_RegularFile_NotLink` | 普通文件 → IsLink=false | F-Link-1 |

### 测试 2：LinkResolutionTests（任务 2）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Link_ScopeIn_NotDuplicated` | Scope 内 symlink → 目标被扫描一次，不重复 | F-Link-2 |
| `Link_ScopeOut_MarkedOutOfScope` | Scope 外 symlink → 标记 OutOfScope, 不递归 | F-Link-2 |
| `Link_Circular_Detected` | A→B→A 循环 symlink → 两者标记 Circular | F-Link-6 |
| `Link_Hardlink_Deduplicated` | 同一 inode 两个路径 → 只产生一个内容统计 | F-Link-3 |

### 测试 3：ScannerLinkTests（任务 3，集成）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Scanner_Links_ScopeRelationCorrect` | 扫 fixture links/ → 所有 link 的 ScopeRelation 正确 | F-Link-2 |
| `Scanner_Symlink_NotFollowed` | 扫 symlink 目录 → 不递归进入目标 | F-Link-1 |
| `Scanner_MountPoint_NotCrossed` | 挂载点边界 → 不跨设备扫描 | F-Link-4 |

---

## 四、模块依赖关系

```
任务 1（LinkInspector 接口 + 平台实现）
   ↓
任务 2（Link 分流逻辑 + Canonical Path）
   ↓
任务 3（Scanner 集成 + 循环检测 + Mount Point 边界）
   ↓
端到端验收（fixture links/ 扫全）
```

---

## 五、设计决策

| 决策 | 结论 |
|---|---|
| 平台抽象方式 | `Helichrysum.Filesystem` 中 `ILinkInspector` 接口，按平台条件编译 |
| Link 目标 Scope 判定 | 用 Canonical Path 前缀匹配 + DeviceId 双重校验 |
| Hardlink 分组 | 用 inode 或 NTFS FileId 作为 inode group key |
| 循环检测 | 维护已访问 Canonical Path 的 HashSet |
| Mount Point 默认策略 | 默认不跨设备（`--cross-device` 可覆盖） |