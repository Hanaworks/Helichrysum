---
layout: home

hero:
  name: Helichrysum
  text: 个人数字资产整理与归档工具
  tagline: 多硬盘、多备份、多时期数据的统一分析、整理与归档
  actions:
    - theme: brand
      text: 阅读需求规格说明书
      link: /requirements
    - theme: alt
      text: 阅读技术实现方案
      link: /technical-design

features:
  - icon: 📋
    title: 需求规格说明书
    details: 背景与目标、核心概念、典型用户场景、12+ 功能模块共 116 条编号需求（RFC 2119 风格）、非功能需求、约束边界与术语表。
    link: /requirements
    linkText: 查看需求 →
  - icon: 🛠️
    title: 技术实现方案（.NET 主方案）
    details: 分层架构（Core SDK + CLI + WebUI + WPF）、.NET 8 / C# 核心引擎选型、分层择优（Polyglot）与可替换接口、核心数据模型与 SQLite Schema、关键模块设计、操作/报告双界面、阶段路线图。
    link: /technical-design
    linkText: 查看方案 →
  - icon: 🗺️
    title: 实施计划
    details: 垂直切片序列（切片 0-9）、每片功能清单与 TDD 红线、fixture 目录树、双层验收与完成定义（DoD），从骨架到 v1.0 的施工图。
    link: /implementation-plan
    linkText: 查看计划 →
  - icon: 🎯
    title: 不是重复文件查找器
    details: 面向"同一套数据在不同时期被多次备份"的真实场景，识别完全重复、版本演进、重命名、压缩包配对等语义关系，最终整合为标准归档。
  - icon: 🔒
    title: 只读扫描，安全执行
    details: 扫描阶段绝对零写动作；所有破坏性操作必须经过 Plan → Exec 流水线、二次确认，默认走回收站而非物理删除。
---

## 这是什么

Helichrysum（蜡菊 / 永久花）是一款**面向个人数字资产整理与归档**的命令行 + 图形界面工具。

它不是传统的"重复文件查找器"，而是用来处理这样的真实场景：

```text
多个硬盘
  + 多份备份
  + 多个时间点
  + 零散复制 / 下载 / 解压
  + 不同版本
  + 重复数据
  ↓
统一分析 → 识别数据关系 → 生成可查看可筛选的报告
  ↓
人工处理真正有歧义的部分 → 清理与整理 → 形成最终归档
```

## 当前状态

| 项 | 状态 |
|---|---|
| 需求规格说明书 | Draft v0.2（116 条编号需求） |
| 技术实现方案（.NET 主方案） | Draft v0.2 |
| 实施计划 | Draft v0.2（切片 0-9 + TDD 验收标准） |
| License | GPL-3.0 |
| 代码实现 | 切片 0-6 已完成（64 测试全绿） |

## 后续文档（待补）

- CLI 命令参考
- Manifest Schema 规范
- 插件 API
- UI 原型设计