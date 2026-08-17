# 切片 Exec-Safety：执行安全兜底

**状态：✅ 已完成（回溯补写计划文档）**

---

## 一、实现目标

Executor 增加 F-Exec-7~12 安全兜底：清理前 Staging 保底、清理后完整性校验与自动回滚、执行前 TOCTOU 防替换。

**验收命令：**
```bash
dotnet test  # 71 测试全绿
# 行为验证：
#   ① 执行 MoveToTrash → 原文件消失，staging 有保底副本
#   ② 传入错误 hash（模拟 TOCTOU）→ 动作中止，文件保留
#   ③ 传入错误 hash（模拟 staging 损坏）→ 回滚，原文件不动
```

---

## 二、任务计划与功能点

### 任务 1：TOCTOU 执行前校验（F-Exec-11）
- `ExecuteAction` 增加 `expectedHash` 参数
- 执行前重算 hash，与期望值不符 → Aborted，文件保留

### 任务 2：Staging 两阶段保底（F-Exec-7）
- MoveToTrash 时先复制到 staging 目录
- staging 校验通过后才移入 trash

### 任务 3：完整性校验与回滚（F-Exec-8）
- staging 副本 hash 与原始 hash 比对
- 不符 → 删除 staging、回滚、标 RolledBack

---

## 三、TDD 测试用例

| 测试名 | 断言 | 涉及 |
|---|---|---|
| `MoveToTrash_WithStagingBackup` | 原文件消失 + staging 有副本 + Completed | F-Exec-7 |
| `TOCTOU_HashMismatch_Aborts` | hash 变 → Aborted + 文件保留 | F-Exec-11 |
| `IntegrityCheck_Fails_Rollback` | staging 损坏 → RolledBack + 原文件保留 | F-Exec-8 |

---

## 四、验收确认

- 71 测试全绿 ✅
- 已合并 master 并推送 ✅