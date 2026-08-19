# Helichrysum

> 蜡菊（永久花）—— 永久归档。

**Helichrysum** 是一款面向个人数字资产整理与归档的工具。

它不是传统意义上的"重复文件查找器"，而是用来处理**多块硬盘、多份备份、不同时期、零散复制下载解压**出来的数据，把它们整合为一套完整、干净、有序、可长期维护的标准归档（Canonical Archive）。

---

## 文档导航

| 文档 | 内容 |
|---|---|
| **[需求规格说明书](REQUIREMENTS.md)** | 背景、核心概念、典型场景、功能需求（116 条编号需求）、非功能需求、边界 |
| **[技术实现方案（.NET 版）](TECHNICAL_DESIGN.md)** | 主方案：.NET 10 / C# 技术栈，架构、数据模型、模块设计、CLI/WebUI/WPF 三端、路线图 |
| **[实施计划](IMPLEMENTATION_PLAN.md)** | 垂直切片（0-14）、功能清单、TDD 红线、fixture 验收、完成定义 |
| **[备选：技术实现方案（Rust 版）](TECHNICAL_DESIGN_RUST.md)** | 早期备选方案：Rust / Cargo / Tauri 技术栈（保留作参考） |

---

## 一句话定位

```text
多个硬盘 + 多个备份 + 多个时间点 + 零散复制/下载/解压
                  ↓
            统一分析
                  ↓
          识别数据关系
                  ↓
      生成可查看、可筛选的报告
                  ↓
        人工处理真正有歧义的部分
                  ↓
            清理和整理
                  ↓
           形成最终归档
```

---

## 关键能力

- **Scope 感知**：只在用户指定范围内工作，扫描器不主动扩大边界
- **Link 正确处理**：symlink / hardlink / junction / mount point 按平台语义分别处理
- **分层渐进分析**：目录结构 → 元数据 → SampledHash → FullHash，从轻到重
- **关系识别**：ExactDuplicate / StructuralSibling / ArchivePair / Versioned 等 9 种关系
- **压缩包配对**：识别 `.zip` / `.7z` / `.tar.gz` 与解压目录的关系，结合 mtime 判断可清理
- **报告驱动**：目录树逐层展开、按问题类型筛选、Plan / Exec 严格分离
- **三端形态**：CLI / SDK / WebUI / WPF 桌面 GUI（Windows 优先），共用 .NET 核心引擎

---

## 阅读建议

- **第一次读**：先看 [需求文档 §1 背景与目标](REQUIREMENTS.md) 和 [§3 典型用户场景](REQUIREMENTS.md)，理解我们要解决什么问题
- **想了解架构**：从 [技术方案 §1 总体架构](TECHNICAL_DESIGN.md) 开始
- **想了解技术选型**：直接看 [技术方案 §2 技术栈选型](TECHNICAL_DESIGN.md)
- **想了解落地节奏**：看 [技术方案 §9 阶段路线图](TECHNICAL_DESIGN.md)

---

> 当前文档版本：v0.2 Draft · 最后更新：2026-08-15
> 代码状态：**切片 0-14 全部完成（113 测试全绿，CI 三平台绿）；UI 线（WebUI/WPF）由用户单独推进**
