<p align="center">
  <img src="docs-site/_media/logo.svg" width="128" height="128" alt="Helichrysum Logo">
</p>

# Helichrysum

> 蜡菊（永久花）—— 面向个人数字资产的整理与归档工具。

**Helichrysum** 不是传统的"重复文件查找器"。它处理的是这样的真实场景：

```text
多块硬盘 + 多份备份 + 多个时间点 + 零散复制/下载/解压 + 不同版本 + 重复数据
        ↓
   统一分析（识别数据之间的语义关系）
        ↓
   生成可查看、可筛选的报告
        ↓
   人工处理真正有歧义的部分
        ↓
   清理与整理 → 形成最终归档
```

它理解"同一套数据在不同时间点的关系"——完全重复、部分变化、重命名、移动、版本演进、压缩包与解压目录配对——而不是只判断文件字节是否相同。

## 当前状态

| 项 | 状态 |
|---|---|
| 需求规格说明书 | ✅ v0.2（Draft，116 条编号需求） |
| 技术实现方案（.NET 主方案） | ✅ v0.2（Draft） |
| 实施计划 | ✅ v0.2（Draft，切片 0-9 + TDD 验收标准） |
| License | GPL-3.0 |
| 代码实现 | ⏳ 未开始（待切片 0 启动） |

## 关键特性

- **Scope 感知**：只在用户指定范围内工作，扫描器不主动扩大边界
- **Link 正确处理**：symlink / hardlink / junction / mount point 按平台语义分别处理；Scope 外 Link 标记但不扫描
- **分层渐进分析**：目录结构 → 元数据 → SampledHash → FullHash，从轻到重，避免无差别全盘 hash
- **关系识别**：ExactDuplicate / StructuralSibling / ArchivePair / Versioned / Renamed 等 9 种语义关系
- **压缩包配对**：识别 `.zip` / `.7z` / `.tar.gz` 与解压目录的关系，结合 mtime 判断"解压后未改动"的高可信清理标记
- **报告驱动**：操作界面（配置/扫描/问题列表/标记/计划/执行）+ 报告界面（目录树展开/预览差异），Plan → Exec 严格分离、二次确认、默认走回收站
- **三端形态**：CLI（Spectre.Console） / SDK / WebUI / WPF 桌面壳（Windows 优先），共用 .NET 核心引擎

## 技术栈

| 层 | 选型 |
|---|---|
| 核心引擎 | .NET 8 / C#（NativeAOT 可选） |
| 存储 | SQLite（manifest 可跨语言、可审计） |
| CLI | Spectre.Console |
| WebUI | ASP.NET Core + Vite（Svelte/Solid） |
| 桌面壳 | WPF（Windows 优先）；Avalonia / Tauri 为 v1.x 候选 |

详见 [docs/technical-design.md](docs/technical-design.md)（含分层选型与可替换接口设计）。

## 项目文档

| 文档 | 内容 |
|---|---|
| [docs/requirements.md](docs/requirements.md) | 需求规格说明书（116 条编号需求，含处理决策模型） |
| [docs/technical-design.md](docs/technical-design.md) | .NET 技术实现方案（主方案，含分层选型与可替换接口） |
| [docs/implementation-plan.md](docs/implementation-plan.md) | 实施计划（切片 0-9 + TDD 验收标准 + 需求全覆盖测试策略） |
| [docs/technical-design-rust.md](docs/technical-design-rust.md) | Rust 版备选方案（早期探索，保留参考） |

## 路线图

- **切片 0-1**：项目骨架 + 扫描/重复/报告端到端握手（5 周内可用）
- **切片 2-6**：Link 处理 / 分层 hash / 关系分析 / ArchivePair / Plan + Executor
- **切片 7-8**：WebUI（交互实验室）→ WPF 桌面壳（Windows 优先）
- **切片 9**：增量扫描、规模调优、压力基准

## 开发

> 代码尚未开始，以下为计划：需要 .NET SDK 8.0+，`dotnet build` 构建，各平台 CI 构建中。

## License

[GPL-3.0](LICENSE) —— 允许自由使用与修改（含 fork 自用/改良），但**任何分发衍生作品必须保持开源并保留原版权声明**。

> 设计意图：代码可以改、可以拿去用、拿来演化——但"改成自己的再闭源占为己有"不被允许。