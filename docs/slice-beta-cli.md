# 切片 Beta CLI：命令补全 & 检测器集成

**状态：✅ 已完成**

---

## 一、实现目标

CLI 可独立完成**扫描 → 分析 → 计划 → 执行**全流程，无需 UI 介入。Beta 可发布版本。

**验收命令：**
```bash
helichrysum --version
helichrysum scan --scope tests/fixtures/backup1,tests/fixtures/backup2
helichrysum analyze --tier full
helichrysum plan show
helichrysum plan dry-run
helichrysum exec --confirm
```

---

## 二、任务计划与功能点

### 任务 1：检测器集成到 analyze 命令
**依赖：** 已有 4 个检测器（ExactDuplicate / StructuralSibling / MovedRenamed / ArchivePair）
**功能点：**
- 1a. analyze 命令跑完所有 4 个检测器，合并输出
- 1b. 分析完成后自动生成 Plan 并保存到 manifest 目录

### 任务 2：plan 命令
**依赖：** 任务 1
**功能点：**
- 2a. `plan list` — 列出所有已生成的计划
- 2b. `plan show <id>` — 展示计划详情（动作数、冲突数、回滚步骤）
- 2c. `plan dry-run <id>` — 模拟执行，展示执行后状态（不改动文件）

### 任务 3：exec 命令
**依赖：** 任务 2
**功能点：**
- 3a. `exec <plan-id>` — 执行指定计划，**必须显式 `--confirm` 才执行**
- 3b. 执行前对象身份重校验（F-Exec-11：路径存在 + hash 一致）
- 3c. 执行日志输出到控制台 + manifest 记录
- 3d. 执行完成后生成新 manifest

### 任务 4：--version 与 CI 验证
**依赖：** 无
**功能点：**
- 4a. `helichrysum --version` 输出双轨版本号（SemVer + git hash）
- 4b. GitHub Actions CI 验证跑绿

---

## 三、TDD 测试用例

### 测试 1：PlanCommandTests（任务 2）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Plan_Show_DisplaysPlanDetails` | 从 JSON 加载 plan → 显示动作数、冲突数 | F-Plan-3 |
| `Plan_DryRun_DoesNotModifyFiles` | dry-run 后文件未被修改（hash 不变） | F-Plan-3 |
| `Plan_List_Empty_WhenNoPlans` | 无 plan 时 → 显示空列表 | F-Plan-4 |

### 测试 2：ExecCommandTests（任务 3）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Exec_RequiresConfirmFlag` | 无 `--confirm` → 不执行，返回 1 | F-Exec-1 |
| `Exec_WithConfirm_ExecutesPlan` | 有 `--confirm` → 执行计划，文件被 moved | F-Exec-1 |
| `Exec_VerifyObjectBeforeAction` | 文件被修改后 → 中止动作，标人工 | F-Exec-11 |
| `Exec_LogsEachAction` | 执行后日志有每条动作的记录 | F-Exec-3 |

### 测试 3：VersionTests（任务 4）

| 测试名 | 断言 | 涉及 F-xxx |
|---|---|---|
| `Version_OutputsSemver` | `--version` 输出包含 SemVer 格式 | §10.3 |
| `Version_OutputsGitHash` | `--version` 输出包含 git hash | §10.3 |

---

## 四、模块依赖关系

```
任务 4（--version + CI）← 独立
   ↓
任务 1（检测器集成 → analyze 自动生成 Plan）
   ↓
任务 2（plan 命令：list / show / dry-run）
   ↓
任务 3（exec 命令：confirm 执行 + 校验 + 日志）
   ↓
端到端验收：CLI 全流程跑通
```

---

## 五、设计决策

| 决策 | 结论 |
|---|---|
| Plan 存储位置 | `~/.helichrysum/plans/` 目录，JSON 格式 |
| Exec 确认方式 | 必须 `--confirm` 参数（交互式确认在 WebUI 中做） |
| 版本号格式 | `v0.1.0 (git abc1234)` 双轨展示 |
| 检测器输出 | 各检测器结果合并写入 manifest relations 表 |