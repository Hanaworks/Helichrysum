# Helichrysum 技术实现方案（.NET 版）

| 项目 | 内容 |
|---|---|
| 文档状态 | Draft v0.2 |
| 最后更新 | 2026-08-14 |
| 适用版本 | 目标 v1.0 |
| 关联文档 | [REQUIREMENTS.md](./requirements.md) |
| 备选方案 | [TECHNICAL_DESIGN_RUST.md](./technical-design-rust.md) |

> 本文档定义 Helichrysum 工程实现的 **.NET 技术方案**（替代初版 Rust 方案，Rust 版保留在 `technical-design-rust.md` 作为备选）。所有需求条目（F-xxx / N-xxx）请参阅需求文档。

---

## 1. 总体架构

### 1.1 设计原则

1. **核心引擎单一**：所有扫描、分析、报告、计划、执行逻辑集中在 `.NET Class Library`（`Helichrysum.Core`），CLI / WebUI / 桌面 GUI 都是它的"壳"。
2. **形态可分离**：CLI 必须可在无 GUI / 无浏览器环境下独立完成全部流程；WebUI 与 GUI 仅作为人机交互的便利层。
3. **只读扫描与写入执行严格分离**：扫描阶段绝对零写动作；所有写动作必须经过 Plan → Exec 流水线。
4. **Manifest 是事实来源**：所有阶段产物都基于或生成 manifest；manifest 可独立审计、独立复用。
5. **分层渐进**：从目录层到 hash 层逐级加深，上层未命中冲突时下层不启动。
6. **命名不使用缩写**：一般情况下，代码命名（类型、成员、变量、文件名、配置键、表名/列名）一律使用完整、自描述的命名而非缩写形式（如 `manifestRepository` 而非 `manifRepo`、`scanCompletedCount` 而非 `scanCnt`）。例外仅限业界通用标准术语（`IO` / `SQL` / `HTTP` / `JSON` / `GUI` / `CLI` 等）——`Fs` 这类项目内缩写一律展开为完整词（`Helichrysum.Filesystem`、`FilesystemObject`）。
6a. **初版 WebUI 验证交互（WebUI-First，限初版窗口期）**：本工具初版的 UX 未经真实使用验证与需求设计，交互是否合理、好用是未知的。因此在**初版开发期**，展示层一律以 **WebUI 作为交互验证介质**（廉价快速迭代的"交互实验室"）——先把功能性的设计（扫描、分析、报告、标记流程）跑起来，通过 WebUI 亲手操作验证交互逻辑的合理性与用户体验；**初版交互验证定稿后，才启动 NativeUI（WPF/WinUI）复刻落地**。此规则不是"所有 UI 永远先 WebUI"，而是限定于"初版 UX 验证窗口期"：一旦交互经真实使用确认定型，后期新增功能的 UI 可依成熟度直接落在既有壳上。
7. **复杂度控制第一原则**：原则上不应增加系统复杂度，**简单优先**。优先利用现有架构能力，而非创建新的抽象层/中间层；仅当确实能降低整体复杂度时，才考虑增加复杂度的方案；禁止重复造轮子——现有框架/架构已提供的功能绝不重复实现。
8. **代码美学与换行规范（强制执行）**：
   - **完全禁止 120 字符自动换行限制**：不因行长度超过 120 字符就强行折行
   - 仅在确实能提高可读性时才换行：链式调用、Builder 模式、参数过多、长表达式等场景
   - 简单代码、初始化配置、日志语句、短方法调用等优先保持单行，不做无意义拆分
   - **链式调用对齐**：Lambda/builder 链式调用中，第一个方法调用紧跟在变量后不换行，后续每个 `.Method()` 的 `.` 与第一个方法调用的 `.` 严格垂直对齐：

     ```csharp
     // 正确：首调不换行，后续 . 垂直对齐
     services.AddDataflow(options => options.AddTrigger<TestTrigger>("trigger1")
                                            .AddTrigger<TestTrigger>("trigger2")
                                            .AddStep<CollectorStep>("collector")
                                            .AddLink("trigger1", "collector")
                                            .AddLink("trigger2", "collector"));

     // 错误（禁止）：首调即折行 + 后续缩进不对齐
     services.AddDataflow(options => options
         .AddTrigger<TestTrigger>("trigger1")
         .AddStep<CollectorStep>("collector")
         .AddLink("trigger1", "collector"));
     ```
   - 代码应当像艺术品一样漂亮、干净、整洁、优雅。

### 1.2 解决方案结构（.sln）

```text
helichrysum/
├── Helichrysum.sln
├── src/
│   ├── Helichrysum.Core/                 # SDK + 业务逻辑（class library）
│   │   ├── Scope/
│   │   ├── Scanning/                     # 扫描器（并行、断点续扫、增量）
│   │   ├── Links/                        # Link 处理（按平台分流）
│   │   ├── Analysis/                     # 关系分析（duplicate/sibling/archive）
│   │   ├── Hashing/                      # 分层 hash 策略
│   │   ├── Reporting/                    # 报告生成（HTML / JSON / SQLite）
│   │   ├── Planning/                     # 处理计划生成与冲突检测
│   │   ├── Execution/                    # 执行器（Trash、Move、Link 替换）
│   │   ├── Manifest/                     # manifest schema + 迁移
│   │   └── Contracts/                    # 公共 DTO / 接口 / 事件
│   ├── Helichrysum.Filesystem/                   # 平台抽象（P/Invoke 按平台条件编译）
│   │   ├── Windows/
│   │   ├── Linux/
│   │   └── macOS/
│   ├── Helichrysum.Cli/                  # 命令行（Spectre.Console）
│   │   └── Commands/                     # scope/scan/analyze/report/plan/exec/...
│   └── Helichrysum.Desktop/              # Windows 桌面（WPF / WinUI 3）
│       └── ViewsViewModel/               # MVVM 结构
├── web/
│   ├── Helichrysum.Web/                  # ASP.NET Core Web 服务（API + 静态报告）
│   │   ├── Controllers/                  # /api/...
│   │   ├── Middleware/
│   │   └── wwwroot/                      # 报告前端静态资源
│   └── helichrysum-web-ui/               # 前端工程（Vite + 轻量框架）
├── tests/
│   ├── Helichrysum.Core.Tests/
│   ├── Helichrysum.Filesystem.Tests/
│   ├── Helichrysum.Integration.Tests/
│   └── Helichrysum.Cli.Tests/
├── docs/
│   ├── requirements.md
│   ├── technical-design.md               # 本文档（.NET 版）
│   ├── technical-design-rust.md          # 备选方案（Rust 版）
│   ├── cli-reference.md
│   ├── manifest-schema.md
│   └── plugin-api.md
└── examples/
    └── SdkUsage/                         # SDK 示例
```

### 1.3 分层架构

```text
┌────────────────────────────────────────────────────────────────────┐
│  形态层（Shells）                                                   │
│  ┌───────────────┐  ┌─────────────────┐  ┌───────────────────────┐ │
│  │ CLI           │  │ Web Server      │  │ Desktop (WPF/WinUI 3) │ │
│  │ Spectre.Console│  │ ASP.NET Core    │  │ MVVM                 │ │
│  └───────┬───────┘  └────────┬────────┘  └──────────┬────────────┘ │
│          └───────────────────┴───────────────────────┘              │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ SDK 层（public .NET API）           Helichrysum.Core          │  │
│  │ Scope / Scanner / Analyzer / Reporter / Planner / Executor   │  │
│  └───────────────────────────────┬───────────────────────────────┘  │
│                                  │                                  │
│  ┌───────────────────────────────┴───────────────────────────────┐  │
│  │ 平台抽象层 (Helichrysum.Filesystem)                                   │  │
│  │ Windows(NTFS)/ Linux / macOS：fs 操作、link 语义、路径规范化   │  │
│  └───────────────────────────────┬───────────────────────────────┘  │
│                                  │                                  │
│  ┌───────────────────────────────┴───────────────────────────────┐  │
│  │ 存储层 (Helichrysum.Core.Manifest)                            │  │
│  │ SQLite (Microsoft.Data.Sqlite) + 迁移 + manifest 仓库         │  │
│  └────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
```

### 1.4 目标框架与运行环境

| 项 | 选择 | 理由 |
|---|---|---|
| 目标框架 | **.NET 8 (LTS)** 基线；GUI 可选 net8.0-windows | LTS 支持期最长；NativeAOT 成熟 |
| Windows | `net8.0-windows` | WPF / WinUI 3 |
| Linux / macOS | `net8.0` | 跨平台运行时 |
| 发布方式 | **NativeAOT 单文件**（CLI）；框架依赖（Web/Desktop 默认） | CLI 零依赖分发；Web/Desktop 用快的发布模式 |
| 最低运行时 | .NET 8 独立部署时自包含 | 用户无需装 SDK |

---

## 2. 技术栈选型

### 2.1 为什么选 .NET/C#

| 维度 | .NET 的契合度 |
|---|---|
| 开发效率 | 强类型 + IDE（VS/Rider）+ NuGet 生态，迭代极快 |
| 跨平台 | .NET 6+ 三端一等公民；文件系统 API 跨平台统一 |
| 性能 | .NET 8 AOT / Tiered JIT 已接近 native；Span/ReadOnlySpan 无 GC 开销处理大文件 |
| 桌面 GUI | WPF（Windows 原生）/ WinUI 3（现代 Windows）；如需跨平台 Avalonia |
| WebUI | ASP.NET Core Minimal API 性能顶级；共享前端工程 |
| 类型安全 | 强类型 + nullable reference types + analyzers，避免 `as any` 类问题 |
| 数据访问 | SQLite 有 `Microsoft.Data.Sqlite`（官方）、EF Core（可选） |
| 生态成熟度 | 社区活跃、微软持续投入、工具链统一（MSBuild 一套搞定） |

**对比 Rust 版的主要取舍（详见 `technical-design-rust.md`）：**

| 维度 | .NET | Rust |
|---|---|---|
| 开发速度 | 快（熟手高产出） | 慢（借用检查学习曲线） |
| 性能 | 足够（AOT 接近 native） | 极致（零开销抽象） |
| GUI 生态 | WPF/Avalonia 成熟 | Tauri（前端技术栈） |
| 参考先例 | 磁盘分析 `TreeSize`、`WizTree`、`Everything`（C++ .NET 系常见） | ripgrep / fclones / czkawka |

**结论：** 本项目是个人工具、生命周期长、开发者熟悉 .NET → **.NET 是长期维护成本更低的方案**。

### 2.2 关键 NuGet 依赖

| 用途 | 包 | 选型理由 |
|---|---|---|
| CLI 框架 | `Spectre.Console` + `Spectre.Console.Cli` | 现代 CLI 事实标准，树/表格/进度条开箱即用 |
| 目录遍历 | `System.IO` 自带或 `SafeFileHandle` 原生枚举 | .NET 8 `EnumerationOptions` 高效 |
| Hash | `System.Security.Cryptography`（SHA256）+ `Avalonia` 无关的 `blake3` 或 `xxhash` | 分层 hash：SHA256 内置；快速层用 xxhash（OxyHashing） |
| 数据库 | `Microsoft.Data.Sqlite` | 官方 ADO.NET provider，零配置 |
| 异步运行时 | `System.Threading.Tasks`（TPL / `Parallel.ForEachAsync`） | 内建，无额外依赖 |
| Web | `ASP.NET Core`（Minimal API / Controllers） | 官方，性能顶尖 |
| 桌面 GUI | `WPF`（WinUI 3 可选） | Windows 原生、MVVM 生态成熟 |
| 序列化 | `System.Text.Json` | 内置；source generation 高性能 |
| 错误处理 | 异常 + `Result` 模式（自建轻量） | CLI 层转友好错误 |
| 日志 | `Serilog` + Console/File sink | 结构化日志、低开销 |
| 配置 | `Microsoft.Extensions.Configuration`（JSON/环境变量） | 官方，标准化 |
| DI | `Microsoft.Extensions.DependencyInjection` | 官方，模块解耦 |
| Trash | `Mono.Posix`（Linux）或 WPF/平台 API；或自研 wrapper | .NET 无官方 Trash，需跨平台封装（见 §7.2） |
| 压缩包读取 | `SharpCompress` / `System.IO.Compression`（zip/tar 内置） | zip/tar/gz 内置支持；7z/rar 用 SharpCompress |
| 路径规范化 | `System.IO.Path.GetFullPath` + 自研 canonical | Windows 长路径 `\\?\` 前缀 |

### 2.3 平台特有 API

| 平台 | 用途 | 接入方式 |
|---|---|---|
| Windows | Reparse Point / Junction / File ID | P/Invoke `GetFileInformationByHandleEx`、`FSCTL_GET_REPARSE_POINT`、`DeviceIoControl` |
| Windows | 最终路径解析 | P/Invoke `GetFinalPathNameByHandle` |
| Linux | Mount detection | 解析 `/proc/self/mountinfo` |
| Linux / macOS | inode / nlink | P/Invoke `stat` / `lstat` |
| macOS | firmlink / APFS | `getattrlist` |

> 均为少量 P/Invoke 代码（`LibraryImport` source generator），封装在 `Helichrysum.Filesystem` 层，平台行为不泄漏到 Core。

### 2.4 分层选型与可替换接口（Polyglot Architecture）

本项目采用**分层择优（polyglot）**策略：每一层选择最适合它的技术，而不是单一语言套到底。核心原则是**按耦合度分三档**：

| 档位 | 特征 | 策略 |
|---|---|---|
| **高耦合层** | 高频调用、深度共享内存数据结构、分析逻辑相互依赖 | 必须进程内同语言，**绝不跨语言** |
| **中耦合层** | 边界清晰、接口稳定（输入/输出明确）、调用频次可控 | 独立进程 / 独立库，跨语言划算 |
| **低耦合层** | 低频调用、外部成熟工具现成 | 直接外包专用工具 |

#### 2.4.1 逐层选型决议

| 层 | 选型 | 通信模式 | 决策理由 |
|---|---|---|---|
| **UI 层** | WebUI（Vite + Vue）+ WPF（桌面壳 Windows 优先） | HTTP 3000 / 进程内 | **初版 WebUI 验证交互**：初版窗口期交互先经 WebUI 验证，定稿后 WPF 复刻（设计原则 6a） |
| **报告渲染** | 前端渲染 JSON 数据 + 虚拟滚动 | HTTP | 大报告（数十万文件）前端虚拟化 |
| **分析 / 计划 / 执行** | .NET（C# 12） | **进程内** | 与核心逻辑深度耦合，多语 = 灾难 |
| **文件遍历 + 元数据** | .NET 起步，**接口隔离可替换**（IScannerDriver） | 进程内，预留切换 | 见 2.4.2 |
| **内容 hash** | SHA256（硬件加速）+ xxhash | 进程内 | 差距 <2x，IPC 成本不值 |
| **内容解析** | 外包：Tika / ffmpeg / 7z 命令行 | 进程 / HTTP | 低频 + 专用工具碾压自研 |
| **存储** | SQLite | 进程内 | 语言无关，无选型问题 |

#### 2.4.2 文件遍历层：先隔离，后替换

.NET 的 `EnumerationOptions` + `Parallel.ForEachAsync` 对**个人文档量级**扫描足够（性能差距 ≤ 2x）。真正的数量级差异只出现在两种场景：

1. **NTFS $MFT 直扫**（Everything / WizTree 式秒扫全盘）——只能在原生代码上实现
2. **千万级文件全量 hash**——超出本工具目标规模

因此策略是：**现在不跨语言，但让架构允许未来跨**。遍历层定义为接口并独立程序集，不依赖 Core 内部结构：

```csharp
/// 扫描驱动程序契约：路径 → FilesystemObject 流
/// 输出基于协议（Path/Size/Mtime/InodeGroup...），与实现语言无关
public interface IScannerDriver
{
    IAsyncEnumerable<FilesystemObject> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken = default);
}

// v1：.NET 内建实现（默认，够用）
internal sealed class DotNetScannerDriver : IScannerDriver { ... }

// v2（如遇性能瓶颈）：Rust 独立扫描器子进程，通过 stdout 协议流式输出
//   helichrysum-scan (Rust 二进制) → 结构化流 → FilesystemObject
// 仅新增一个 driver 实现，Core 分析逻辑零改动
```

同类接口还包括：

```csharp
public interface IHashProvider
{
    Task<string> ComputeAsync(string path, HashTier tier, CancellationToken ct);
    //  实现：SHA256(内置硬加速) / xxhash(快速层) / blake3(NuGet)
}

public interface IPreviewProvider
{
    PreviewKind Supports(string extension);
    Task<Stream?> RenderAsync(string path, PreviewRequest request, CancellationToken ct);
    //  实现：内置(OpenXml/PdfPig) + 可选外包(Tika Server / ffmpeg / pdftoppm)
}
```

#### 2.4.3 内容解析外包

内容解析是"低频调用 + 专用工具碾压"的典型，因此**外包给专区工具**，通过 `IPreviewProvider` 可插拔：

| Provider | 覆盖格式 | 部署方式 |
|---|---|---|
| 内置（OpenXml + PdfPig） | docx / xlsx / pptx / PDF 基础 | 无外部依赖 |
| ffmpeg / ffprobe | 音视频元数据、缩略图 | 检测到即可启用 |
| Tika Server（JVM 进程） | 全格式内容提取（工业标准） | 可选 HTTP 服务 |
| pdftoppm / unoconv | PDF / Office 渲染成图 | 可选 |

**运行时策略：** 检测到哪个启用哪个，无可用时自动降级内置解析，核心逻辑不受影响。

#### 2.4.4 组合边界总结

```text
┌─ 分析/计划/执行（.NET 主进程）─────────────────────┐
│  IScannerDriver   ← 内建起步，预留 Rust/MFT 替换   │
│  IHashProvider    ← SHA256/xxhash，预留 blake3    │
│  IPreviewProvider ← 内置 + Tika/ffmpeg/7z 外包    │
└────────────┬──────────────────────────────────────┘
             │ HTTP (3000)
     ┌───────┴────────┐
     │ Vite+Vue UI │   ← 独立前端工程
     └────────────────┘
     + WPF 桌面壳（Windows）
```

**现在就是组合态：** 前端独立、解析外包、Trash 平台化、特殊格式走命令行兜底。

**预留将来可组合：** 遍历层接口隔离、hash 层接口隔离——**需要时才替换，不为了组合而组合**。

---

## 3. 核心数据模型

### 3.1 Filesystem Object

```csharp
/// 文件系统对象的唯一逻辑标识（manifest 内稳定）
public readonly record struct ObjectId(long Value);

/// 对象类型
public enum ObjectKind
{
    RegularFile,
    Directory,
    Symlink,
    Hardlink,             // 引用 inode group
    WindowsJunction,
    ReparsePoint,
    MountPoint,
    Unknown
}

/// 对象 Scope 关系
public enum ScopeRelation
{
    InScope,
    OutOfScope,
    Broken,
    Circular
}

public sealed record FilesystemObject
{
    public required ObjectId Id { get; init; }
    public required long ScopeId { get; init; }
    public required string Path { get; init; }             // 相对 scope 根
    public required string CanonicalPath { get; init; }    // 规范化绝对路径
    public required ObjectKind Kind { get; init; }
    public long? Size { get; init; }
    public DateTimeOffset? Mtime { get; init; }
    public DateTimeOffset? Ctime { get; init; }
    public long? InodeGroup { get; init; }                 // hardlink 共享
    public required ulong DeviceId { get; init; }
    public required ScopeRelation ScopeRelation { get; init; }
    public string? LinkTarget { get; init; }               // 原始 target（未 resolve）
    public string? ResolvedLinkTarget { get; init; }       // canonical target
}
```

### 3.2 Hash 分层

```csharp
public enum HashTier
{
    Metadata,      // size + mtime，不读内容
    SampledHash,   // 头/中/尾采样 + xxhash
    FullHash       // 全量内容 hash (SHA256 或 blake3)
}

public sealed record HashRecord
{
    public required ObjectId ObjectId { get; init; }
    public required HashTier Tier { get; init; }
    public string? HashValue { get; init; }   // hex
    public required long BytesRead { get; init; }
    public required DateTimeOffset ComputedAt { get; init; }
}
```

### 3.3 Relation

```csharp
public sealed record Relation
{
    public required long Id { get; init; }
    public required RelationKind Kind { get; init; }
    public required IReadOnlyList<ObjectId> Members { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<Evidence> Evidence { get; init; }
}

public enum RelationKind
{
    ExactDuplicate,
    NearDuplicate,
    Renamed,
    Moved,
    StructuralSibling,
    ArchivePair,
    LinkReference,
    PartialOverlap,
    Versioned
}

public enum ArchivePairKind
{
    FullyExtracted,
    ModifiedAfterExtraction,
    PartialExtraction,
    Unrelated
}

public sealed record Evidence
{
    public required string Type { get; init; }   // HashMatch/SizeMatch/MtimeMatch/StructureSimilarity/ArchiveListing
    public string? Details { get; init; }
}
```

### 3.4 Manifest Schema（SQLite）

```sql
-- 与 Rust 版一致，此处不重复，见 ./manifest-schema.md
```

**快照元数据（F-Report-12）：** manifest 库级 `_manifest_meta` 表必须含 `created_at` / `scanned_at` / `scope_snapshot` / `tool_version`，并在报告与 UI 展示快照年龄；schema 版本号字段（F-Report-11）与版本转换以 `if (v<n) upgradeToVn()` 一次性硬编码实现。

**索引策略：**（与 Rust 版一致）

```sql
CREATE INDEX idx_objects_size  ON objects(size)  WHERE size IS NOT NULL;
CREATE INDEX idx_objects_inode ON objects(inode_group) WHERE inode_group IS NOT NULL;
CREATE INDEX idx_relation_members_obj ON relation_members(object_id);
```

> Manifest 是完全跨语言的（SQLite 文件），后续如需迁移技术栈，数据不丢。

---

## 4. 关键模块设计

### 4.1 Scope 模块

**职责：**

- 接受用户输入的根路径集合（`ScopeConfig`）
- 路径规范化：`Path.GetFullPath` + Windows 下调用 `GetFinalPathNameByHandle` 得到 canonical path（解析 symlink）
- 提供 `Scope.Contains(path)` 快速判定
- 检测 Scope 嵌套与重叠（警告）
- 持久化 Scope 配置（JSON config file）

**关键算法：** Canonical Path 前缀匹配 + DeviceId 双重校验。

### 4.2 Scanner 模块

**职责：**

- 输入：Scope + 排除规则（glob/regex，`FileSystemName.MatchesSimpleExpression`）
- 输出：`FilesystemObject` 流（`IAsyncEnumerable<FilesystemObject>` 或 chunked channel）
- 并发：`Parallel.ForEachAsync` 按目录分派并行处理；`Channel` 背压
- Link 处理：见 4.3
- Mount Point 检测：非根卷作为边界对象，默认不跨越
- 循环引用：维护已访问 Canonical Path 的 `ConcurrentDictionary`
- 断点续扫：定期写 `scan_state` 表
- 增量：对比上次 manifest，仅扫描新增/修改

```csharp
public interface IScanner
{
    IAsyncEnumerable<FilesystemObject> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default);
}
```

### 4.3 Link Handler

按平台条件编译：

```csharp
public interface ILinkInspector
{
    LinkInfo Inspect(string path);
}

public sealed class LinkInfo
{
    public required bool IsLink { get; init; }
    public required LinkKind Kind { get; init; }   // Symlink/Hardlink/Junction/ReparsePoint/None
    public string? Target { get; init; }
    public string? ResolvedTarget { get; init; }
    public ScopeRelation ScopeRelation { get; init; }
    public long? InodeGroup { get; init; }
}

// Platform implementations
/// <summary>Windows: Reparse Point + FileId</summary>
internal sealed class WindowsLinkInspector : ILinkInspector { /* P/Invoke */ }

/// <summary>Linux: lstat + readlink + statx</summary>
internal sealed class LinuxLinkInspector : ILinkInspector { /* P/Invoke */ }

/// <summary>macOS: lstat + readlink + getattrlist</summary>
internal sealed class MacOsLinkInspector : ILinkInspector { /* P/Invoke */ }
```

**关键策略（与 Rust 版一致）：**

- Link **不跟随**，仅记录 source/target + scope_relation
- Scope 内 → `LinkReference`，不重复扫描
- Scope 外 → `OutOfScope` 标记后跳过
- Broken / Circular 分别标记

### 4.4 分层 Hash 模块

```csharp
public sealed class LayeredHasher
{
    // 1. (Metadata tier) size + mtime 入库
    // 2. 若 (size, mtime) 与其他对象碰撞 → SampledHash
    // 3. 若 SampledHash 仍碰撞 → FullHash
    // 升级单调，永不回退
}
```

**SampledHash：** 头 16KB + 中段 32KB + 尾 16KB（xxhash）；文件 < 64KB 全量读。

**FullHash：** SHA256（内置）或 blake3（`Blake3` NuGet，SIMD 加速）；`ReadOnlySpan<byte>` 零拷贝处理，`FileStream` 大 buffer 流式读。

### 4.5 Relation Analyzer

**Detector 接口：**

```csharp
public interface IRelationDetector
{
    string Name { get; }
    IAsyncEnumerable<IReadOnlyList<Relation>> DetectAsync(
        Manifest manifest,
        CancellationToken ct = default);
}
```

**核心 Detector 与 Rust 版一致：** ExactDuplicate / HardlinkGroup / Renamed / StructuralSibling / ArchivePair / NearDuplicate / Versioned。

### 4.6 Archive Pair Detector

- 提取压缩包内部清单：zip 用 `System.IO.Compression.ZipFile`（中央目录）、tar 用 `System.IO.Compression.TarFile`、7z/rar 用 `SharpCompress`
- 同一目录或就近查找候选解压目录
- 匹配度 → FullyExtracted / PartialExtraction / ModifiedAfterExtraction / Unrelated
- mtime 容差（默认 1h）+ 内容一致 → "建议清理压缩包"高可信标记
- 加密压缩包 → `EncryptedArchive`，跳过配对

### 4.7 Report Builder

**三种输出（与 Rust 版一致）：**

1. **HTML 交互报告**：单文件自包含（内嵌 JSON + 轻量前端框架产物），可离线打开
2. **JSON**：机器可读，`System.Text.Json` source generation
3. **SQLite 视图**：在 manifest 数据库上建视图

### 4.8 Plan Generator

```csharp
public sealed record Plan
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<PlannedAction> Actions { get; init; }
    public required IReadOnlyList<PlanConflict> Conflicts { get; init; }
    public required IReadOnlyList<RollbackEntry> RollbackInfo { get; init; }
}

public enum PlannedActionType
{
    Keep,
    MoveToTrash,
    MoveTo,
    Rename,
    ReplaceWithLink,
    Merge
}
```

冲突检测与 Rust 版一致：目标已存在、对象被多 action 引用、跨卷移动风险。

### 4.9 Executor

```csharp
public sealed class Executor
{
    // 1. Confirm: CLI 交互 yes / WebUI 显式按钮
    // 2. Trash 优先（跨平台封装）
    // 3. 跨卷两阶段：先 Copy 后 Delete
    // 4. 每步执行日志（写 manifest）
    // 5. 中断可恢复（基于执行日志）
}
```

---

## 5. 三端形态

### 5.1 CLI（Spectre.Console）

命令集与 Rust 版一致（`scope` / `scan` / `analyze` / `report` / `plan` / `exec` / `manifest`），但渲染用 Spectre：

```text
helichrysum scan --scope default

    正在扫描 Backup1 (D:\Backup1)...
    ─────────────────────────────────
    ████████████████░░░░  62%  1,284,301 / 2,071,463
    发现重复组 1,203
    发现 ArchivePair 45
    发现 OutOfScope Link 3
```

Spectre 好处：树渲染、进度条、表格开箱即用，无需自定义。

**退出码：** 0 成功 / 1 用户错误 / 2 系统错误 / 130 中断。

### 5.2 SDK

`Helichrysum.Core` 主要面向 .NET 程序集引用：

```csharp
using Helichrysum.Core;
using Helichrysum.Core.Manifest;

var scope = new ScopeBuilder()
    .AddRoot(@"D:\Backup1")
    .AddRoot(@"D:\Backup2")
    .Exclude("**/.git/**")
    .Build();

using var manifestRepo = ManifestRepository.Open("/path/to/manifest.sqlite");

var scanner = new Scanner(manifestRepo, scope);
await foreach (var obj in scanner.ScanAsync(request)) { /* 进度 */ }

var analyzer = new RelationAnalyzer(manifestRepo);
var relations = await analyzer.AnalyzeAsync(tier: HashTier.FullHash);

var report = ReportBuilder.Create(manifestRepo, RelationFilter.Duplicates | RelationFilter.ArchivePairs);
await report.SaveHtmlAsync("/path/to/report.html");

var planner = new Planner(manifestRepo);
var plan = await planner.BuildAsync(marks);

var executor = new Executor(manifestRepo);
await executor.PreviewAsync(plan);   // dry-run
await executor.ExecuteAsync(plan);   // 需 confirm
```

### 5.3 WebUI

**架构：** ASP.NET Core Web 应用，提供两类端点：

```text
# 操作界面（Operation API）
POST /api/scope                  # 配置 Scope
POST /api/scan                   # 启动扫描
GET  /api/scan/status            # 扫描进度
GET  /api/manifests              # manifest 列表
GET  /api/manifests/{id}/relations?filter=duplicate|archive
POST /api/plans                  # 生成计划
POST /api/plans/{id}/execute     # 执行（服务端再次确认）

# 报告界面（Report API）
GET  /api/manifests/{id}/tree   # 目录树分页
GET  /api/manifests/{id}/report # 渲染后的 HTML 报告（单文件）
GET  /api/preview/{objectId}    # 文件预览（文本/图片/PDF 元数据）
```

**两种界面：**

| 界面 | 用途 | 默认视图 |
|---|---|---|
| **操作界面（Operation）** | 配置 Scope、选分析逻辑、看进度、问题列表、标记、生成计划、执行 | 问题列表（按类型分组） |
| **报告界面（Report）** | 浏览已完成的报告，定位文件上下文，导出 | 目录树（从根逐层展开，问题在树中标记） |

**操作界面**是用户日常工作的主视图：打开即看到所有重复组、ArchivePair、OutOfScope Link，每条展示路径/大小/置信度，支持批量标记。目录树组件仅作为辅助导航——点击某条问题，展开目录树定位到该文件所在位置。

**报告界面**是一个独立的阅览模式，可从操作界面中的"查看报告"入口进入，也可通过 `helichrysum report view` 命令直接打开。它呈现的是完整的目录树结构，问题的标记在树中可视化（类似 Git 冲突查看器），供用户从全局视角理解数据结构。

**前端：** Vite + Vue，构建产物输出到 `wwwroot/`，由 ASP.NET Core 静态托管。

**安全：** 默认绑定 `127.0.0.1`；写操作（plan/exec）必须经显式 API 且服务端确认；无任何外网依赖。

### 5.4 桌面 GUI（WPF，Windows 优先）

**定位：** 桌面壳是工具的一等公民，CLI 和 WebUI 覆盖不了的核心体验（问题列表批量处理、一键定位到文件、系统托盘后台扫描）由桌面壳提供。v1.0 以 Windows 为目标，跨平台桌面作为后续里程碑。

**WPF（Windows 原生）：** 桌面壳内部分为两个独立界面模式，由导航栏切换：

**操作界面（Operation View）—— 工作台（行动导向）：**

- `Helichrysum.Core` 直接引用，零通信开销
- MVVM：`CommunityToolkit.Mvvm`（源生成器，类型安全），ViewModel 层与 WebUI 共用逻辑模型
- **配置面板：** 选择 Scope 路径、排除规则、分析深度（metadata / sampled / full）
- **扫描进度：** 实时进度条 + 已扫描文件数 + 发现的问题数
- **问题列表（默认视图）：** 扫描完成后自动打开，按类型分组展示"重复组 1,203 / ArchivePair 45 / OutOfScope Link 3 / Broken Link 12"，每条展示路径、大小、hash、置信度、mtime，支持单选/多选/全选批量标记（保留/删除/保留最新/保留最长路径/替换为符号链接）
- **目录树辅助面板：** 点击问题列表中某条文件，右侧或底部展开目录树定位到该文件所在位置，查看同一目录下的其他文件
- **计划管理：** 从标记汇总生成 Plan，展示冲突检测结果，预览 dry-run 状态
- **执行：** 确认后执行，实时日志，执行完成后更新 manifest
- **文件行动：** "在资源管理器中定位"（`Process.Start("explorer.exe", "/select,path")`）、"用系统默认程序打开"——对选中文件的外部动作

**报告界面（Report View）—— 阅览室（查看导向）：**

- 从导航栏"查看报告"入口进入，或从 CLI `helichrysum report view` 打开
- 目录树从 Scope 根逐层展开，支持展开/折叠/键盘导航
- 问题在树中可视化标记（目录节点图标变色、文件节点标注"Duplicated / ArchivePair / OutOfScope / Broken"）
- **预览/差异面板（查看具体文件内容）：**
  - 文本类文件：选中重复组内两个文件 → 并排 Diff（内置文本对比）
  - 图片：缩略图网格 + 点击放大对比
  - ArchivePair：压缩包清单 vs 解压目录清单的差异表（多哪些/少哪些/哪些改过）
  - 可执行文件：大小、hash、版本、签名元数据
- 点击问题文件可切换回操作界面的问题列表以进行标记处理
- 支持导出当前报告为 HTML（单文件自包含）、JSON、SQLite

**后台：**

- 系统托盘：后台扫描进度通知，双击展开主窗口
- 进度条/通知/拖拽文件到系统 —— 原生体验，WebUI 做不到

**WinUI 3（后续演进）：** 现代化 Fluent 界面，与 WPF 共用 ViewModel 层和 Core，切换时只重写 View 层。

**跨平台桌面壳（后续里程碑，v1.x 评估）：** 两种候选组合，按真实需求再选：

| 候选 | 思路 | 适合场景 |
|---|---|---|
| **Tauri + .NET sidecar** | .NET 核心作为后台本地服务（WebUI 的 HTTP 形态复用），Tauri 提供原生窗口外壳；前端与 WebUI 共享一份代码 | 需要"原生窗口 + 一份前端到处跑"（浏览器 + 桌面共用 UI） |
| **Avalonia** | 全 C# 跨平台窗口，API 与 WPF 接近，平移成本低 | 需要"全 C# 代码库 + 跨平台窗口"，不想引入 Rust |

**组合决策（已定）：** v1.0 采用 **WPF（Windows 优先）+ WebUI（浏览器备选）** 的组合，不引入 Rust；v1.x 若出现"原生桌面窗口"的真实需求，在 Tauri 与 Avalonia 之间按上述维度评估，无需重写 Core 或前端。

---

## 6. 性能策略

### 6.1 扫描并发

- 目录遍历 + `Parallel.ForEachAsync`（`MaxDegreeOfParallelism = 逻辑核数`）
- `Channel<FilesystemObject>`（bounded）做生产者-消费者，SQLite 单 writer 批量事务写入（每 1000 条提交）
- `ConcurrentDictionary` 去重（hardlink group、循环检测）

### 6.2 Hash 优化

- 分层升级：元数据碰撞才升采样 hash，采样碰撞才升全量，避免无差别 hash 全盘
- SHA256 是硬件加速的；单文件用 `Stream.Read` 大 buffer + hash transform 同步累积，不异步（避免线程开销）
- 文件间并行：`Parallel.ForEachAsync`

### 6.3 SQLite 调优

```csharp
var conn = new SqliteConnection("Data Source=manifest.db");
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = """
    PRAGMA journal_mode = WAL;
    PRAGMA synchronous = NORMAL;
    PRAGMA cache_size = -200000;   -- 200 MB
    PRAGMA temp_store = MEMORY;
    """;
cmd.ExecuteNonQuery();
```

### 6.4 内存预算

- `IAsyncEnumerable` / `Channel` 流式处理，不将全量 FilesystemObject 载入内存
- Analyzer 按 chunk（10 万条）加载
- 报告前端虚拟滚动（DOM 上限 5 万节点）

---

## 7. 安全性设计

### 7.1 只读 / 写入分离

- Core 中所有写文件系统的逻辑集中在 `Execution/` 命名空间；Scanning/Analysis/Reporting 仅写 manifest 库
- 代码审查时只检查 `Execution/` 即可

### 7.2 Trash 跨平台封装

```csharp
public interface ITrashProvider
{
    Task<bool> MoveToTrashAsync(string path, CancellationToken ct);
}

// Windows: SHFileOperation (FO_DELETE | FOF_ALLOWUNDO) → 回收站
// macOS:  AppleScript/osascript 'tell application "Finder" to delete'
// Linux:  gio trash（若有桌面环境）；否则检出跨 shell 策略
```

默认 `Trash`；`Permanent` 必须显式指定。

### 7.3 路径安全

- 所有用户输入 Canonical Path 化
- `MoveTo` 目标必须在 Scope 或指定归档目录内
- WebAPI 参数路径校验，防 path traversal

### 7.4 隐私

- 零网络请求；manifest 可配置不存 hash；`helichrysum manifest purge` 清理元数据

---

## 8. 配置与默认值

### 8.1 配置文件位置

| 平台 | 路径 |
|---|---|
| Windows | `%APPDATA%\helichrysum\config.json` |
| Linux | `~/.config/helichrysum/config.json` |
| macOS | `~/Library/Application Support/helichrysum/config.json` |

使用 `Microsoft.Extensions.Configuration`（JSON + 环境变量覆盖）。

### 8.2 关键默认值

（与 Rust 版一致，此处列差异项）

| 项 | 默认 | 说明 |
|---|---|---|
| `scan.followSymlinks` | `false` | 默认不跟随 |
| `scan.crossDevice` | `false` | 默认不跨设备 |
| `scan.parallelism` | `Environment.ProcessorCount` | 并发度 |
| `hash.defaultTier` | `full` | 默认分析到 FullHash |
| `relation.structuralSiblingThreshold` | `0.7` | Jaccard |
| `relation.archiveMtimeToleranceSecs` | `3600` | 压缩包配对容差 |
| `exec.deletionStrategy` | `trash` | 默认回收站 |
| `web.bindLocalhostOnly` | `true` | 仅本地 |
| 内置忽略目录 | `$RECYCLE.BIN`, `System Volume Information`, `.git/objects`, `node_modules`, `.DS_Store`, ... | 同 Rust 版 |

---

## 9. 阶段路线图

### Phase 0：基础设施（2 周）

- .sln 骨架 + `Helichrysum.Filesystem` 平台抽象（三平台基础 P/Invoke）
- CI（GitHub Actions：Windows / Linux / macOS 三平台构建 + 测试）
- 日志（Serilog）+ DI + 配置基础设施

### Phase 1：扫描 + 报告 MVP（3 周）

- Scope 管理（CLI + Spectre）
- Scanner 基础版（+ symlink 识别）
- Manifest SQLite 持久化
- ExactDuplicate（元数据 + FullHash SHA256）
- CLI：`scope`/`scan`/`analyze`/`report`
- HTML 报告基础版

**交付：** 命令行完成"找出所有完全重复文件"端到端。

### Phase 2：Link 完整支持 + 分层 hash（2 周）

- Hardlink / Junction / Reparse Point 识别（P/Invoke）
- Mount Point 边界
- SampledHash 层
- 循环引用 / OutOfScope / Broken / Circular 标记

### Phase 3：关系分析扩展（3 周）

- StructuralSibling
- ArchivePair（zip/tar/7z/rar）
- Renamed / Moved
- NearDuplicate（文本，可选）

### Phase 4：Plan + Executor（2 周）

- Plan 模型 + 冲突检测
- Trash 封装（三平台）
- 执行日志 + 恢复
- 两阶段跨卷

### Phase 5：WebUI（3 周）

- ASP.NET Core API（操作接口 + 报告接口两套端点）
- 前端（Vite + Vue）：操作界面（Scope 配置、问题列表、标记、计划） + 报告界面（目录树导航）
- 标记管理 / Plan 编辑器
- 文件预览
- 配置 UI

### Phase 6：桌面 GUI（2 周）

- **WPF MVVM 外壳（Windows 优先）**
- 操作界面：Scope 配置面板、扫描进度、问题列表（默认视图，按类型分组，批量标记）
- 报告界面：目录树逐层展开，问题在树中可视化标记
- 从操作界面一键切换到报告界面
- 系统集成（资源管理器定位、系统托盘、通知）
- 文件预览（系统默认程序打开）
- 自动更新（可选）

**跨平台桌面壳（Avalonia）作为后续里程碑，v1.0 不包含。**

### Phase 7：增量与性能（持续）

- 增量扫描
- Hash 缓存
- 大规模调优（100 万+ 文件）

**v1.0 在 ~17 周内交付完整三端；Phase 1 MVP 5 周可用。**

---

## 10. 风险与未决问题

### 10.1 风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| Windows Reparse Point 种类繁多 | 误识别 | 只处理已知类型，未知标记 `Unknown`, 保留原始数据 |
| RAR 格式闭源 | 解析受限 | `SharpCompress` 尽力，失败降级"无法读取清单" |
| NativeAOT 与 WinUI 3 兼容性 | 桌面发布受限 | AOT 仅用于 CLI；桌面用框架依赖发布 |
| Trash 在 Linux 无桌面环境 | 删除动作失败 | 检出无 gio 时报错，绝不静默永久删除 |
| 同一份数据不同时期真实差异 | 整合困难 | `Versioned` 必须人工确认 |
| .NET 生态的重复文件工具参考少 | 设计参考有限 | 参考 TreeSize / WizTree / Everything 的目录树交互 |

### 10.2 未决问题

1. **桌面壳主框架**：WPF（已有决定）；WinUI 3 在 Fluent 2 成熟后评估迁移；跨平台桌面壳在 v1.x 于 Tauri + .NET sidecar 与 Avalonia 之间评估，v1.0 不引入 Rust。
2. **Web 前端框架（已定）**：**Vite（构建）+ Vue（框架）**——社区热度高、生态成熟，Vite 提供标准 Vue 模板，构建产物入 `wwwroot/`。
3. **数据访问（已定）**：不引入 EF Core，手写 `Microsoft.Data.Sqlite`——manifest 是 schema 稳定的"数据仓库"，手写 SQL 配合版本转换（F-Report-11）更可控。
4. **CLI 退出码与 Spectre 展示**：是否需要完整 TUI 报表模式（类似 `lazygit`）？暂定 CLI 报表走 HTML 导出 + 终端摘要。
5. **Hash 算法（已定）**：**SHA256（内置硬件加速）起步**，预留 hash 字段可扩展（未来可按需切 Blake3）。
6. **`helichrysum verify` 命令（已定）**：v1.0 包含——归档完整性 hash 校验。

### 10.3 版本号方案（已定）

工具版本号**双轨展示**：独立版本计数（SemVer）+ git commit hash，同时呈现，各司其职：

```text
展示格式：v0.1.0 (git 1634a0c)
           │         └─ git commit hash（精确可追溯到具体代码）
           └─ SemVer 独立版本计数（里程碑叙事）

独立计数：遵循语义化版本递增
  MAJOR：破坏性变更 / 功能集确定（v1.0 = 首个正式版）
  MINOR：新增向后兼容的功能
  PATCH：向后兼容的缺陷修复
  （开发期 0.x.y 逐步递增；不受 commit 次数影响）

git 信息：随每个构建附带当前 commit hash（构建准出那一刻的 git HEAD）

来源：
  AssemblyInformationalVersion = SemVer 计数；git 信息由构建脚本注入（Scripts/build 读取 git rev-parse）
显示：helichrysum --version（CLI）、WebUI 页脚、报告头部
```

---

## 11. 附录：与 Rust 版的差异摘要

| 方面 | .NET 版（本文档） | Rust 版（备选） |
|---|---|---|
| 语言 / 运行时 | C# 12 / .NET 8 LTS | Rust 2021 edition |
| 工程结构 | `.sln` + csproj | Cargo workspace |
| CLI | Spectre.Console | clap |
| 桌面 GUI | WPF（v1）；Avalonia / Tauri 为 v1.x 候选 | Tauri（前端） |
| Web | ASP.NET Core + Vite 前端 | axum + 同前端 |
| 并发 | TPL `Parallel.ForEachAsync` + Channel | rayon + tokio |
| Hash | SHA256 + xxhash（可选 blake3） | blake3 + xxh3 |
| 序列化 | System.Text.Json | serde |
| Trash | 自研三平台封装 | `trash` crate |
| 压缩包 | System.IO.Compression + SharpCompress | zip/sevenz/tar/unrar crate |