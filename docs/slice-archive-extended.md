# 切片 Archive-Extended：压缩包格式扩展

**状态：✅ 已完成（回溯补写计划文档）**

---

## 一、实现目标

ArchivePairDetector 从仅支持 zip 扩展为支持 **zip / tar / tar.gz / tgz / 7z / rar** 六种主流格式（F-Archive-1）。

**验收命令：**
```bash
dotnet test  # 71 测试全绿
```

---

## 二、任务计划与功能点

### 任务 1：SharpCompress 集成
- `GetArchiveEntryNames` 对 tar/7z/rar 使用 `SharpCompress.Readers.ReaderFactory.OpenReader`
- 提取条目文件名集合（过滤目录条目）

### 任务 2：ExecCommand 适配
- 适配 Executor 新 API（`ExecutePlan` 返回 `(path, hash)?` 元组）

---

## 三、TDD 测试用例

| 测试名 | 断言 | 涉及 |
|---|---|---|
| 既有 `ZipWithSiblingDir_FullyExtracted` | zip 配对不回归 | F-Archive-4 |
| 既有 `ZipWithModifiedDir_ModifiedAfterExtraction` | 修改判定不回归 | F-Archive-4 |

---

## 四、验收确认

- 71 测试全绿 ✅
- 已合并 master 并推送 ✅