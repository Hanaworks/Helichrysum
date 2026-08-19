# 插件 API 规范

| 项目 | 内容 |
|---|---|
| 文档状态 | v0.1（Draft） |
| 实现位置 | `Helichrysum.Core` / `Helichrysum.Filesystem` |
| 当前状态 | **部分抽象已存在，插件加载机制尚未实现** |

> 本文档记录 Helichrysum 的**扩展点现状**与**插件化演进方向**。当前版本通过接口注入实现扩展，尚未提供动态加载（运行时加载外部程序集）。插件机制列为 phase 7+ 的演进项（技术方案 §5.5 N-Ext）。

---

## 1. 现状：接口注入（编译期扩展）

当前所有"插件点"均为**编译期接口**，通过构造函数注入使用，不提供运行时发现/加载。好处：类型安全、无反射、AOT 兼容。

### 1.1 `IFilesystemService`（Helichrysum.Filesystem）

```csharp
public interface IFilesystemService
{
    string GetCanonicalPath(string path);
    ulong GetFileId(string path);
    ulong GetDeviceId(string path);
}
```

- **用途**：平台文件系统抽象——canonical 路径解析、文件身份、设备号
- **当前实现**：由 `PlatformLinkInspector` 等平台类承担；本接口作为未来扫描器依赖注入的契约
- **扩展方式**：第三方可提供自定义 FS 服务（如自定义 canonical 规则）

### 1.2 `ILinkInspector`（Helichrysum.Filesystem）

```csharp
public interface ILinkInspector
{
    LinkInfo Inspect(string path);
}
```

- **用途**：Link 检测抽象——symlink / hardlink / junction / reparse point
- **实现**：`PlatformLinkInspector`（跨平台，用 .NET 内建 API + P/Invoke stat）
- **扩展方式**：新增平台（如 BSD）或增强检测（如 Windows reparse 深度解析）时实现此接口

### 1.3 Analysis 组件（Helichrysum.Core 静态类）

以下分析器均为 **static class**，当前不可插拔（调用方直接引用）：

| 组件 | 类型 | 说明 |
|---|---|---|
| `ExactDuplicateDetector` | static | 内容 hash 重复分组 |
| `StructuralSiblingDetector` | static | 目录结构相似度（Jaccard） |
| `MovedRenamedDetector` | static | 移动/重命名检测 |
| `VersionedDetector` | static | 版本演进检测 |
| `ArchivePairDetector` | static | 压缩包↔解压目录配对（含锚点提取） |
| `NearDuplicateDetector` | static | 文本归一化近似重复 |
| `ConsistencyVoter` | static | 多数派一致性投票（孤立者检测） |
| `TimeTrustEvaluator` | static | ctime 聚集 → 时间可信度 |
| `ConflictArbiter` | static | 同层意图冲突仲裁 |
| `ResolutionResolver` | static | Equality/Compatibility/Conflict 三态判定 |
| `DependencyChainBuilder` | instance | 处理链依赖关系建模 |

**扩展方向**：将这些 static 类收敛为 `IRelationDetector` 接口 + 注册表，使第三方关系检测器（如"识别相似图片"）可插入分析管线。

### 1.4 可替换接口（技术方案 §2.4）

技术方案已定义三个"预留可替换"接口，尚未在代码中落地为独立类型（当前由具体类承担）：

| 接口 | 意图 | 当前承担者 |
|---|---|---|
| `IScannerDriver` | 扫描器可替换（.NET 内建起步，预留 Rust 子进程/MFT） | `Scanner` |
| `IHashProvider` | hash 算法可替换（SHA256 起步，预留 blake3） | `HashService` |
| `IPreviewProvider` | 内容解析外包（内置 + Tika/ffmpeg） | 未实现（依赖 UI） |

---

## 2. 扩展点速查表

| 扩展点 | 当前类型 | 是否可插拔 | 落地阶段 |
|---|---|---|---|
| 文件系统服务 | `IFilesystemService` | 是（接口注入） | ✅ 已定义 |
| Link 检测 | `ILinkInspector` | 是（接口注入） | ✅ 已实现 |
| 关系检测器 | static 类 ×11 | ❌ 不可插拔 | phase 7 |
| 扫描驱动 | `Scanner` 具体类 | ❌ 预留 | phase 7 |
| Hash 提供 | `HashService` 静态 | ❌ 预留 | phase 7 |
| 内容解析 | 未实现 | — | UI 线合流后 |
| 配置 | `HelichrysumConfiguration` | 是（JSON 配置） | ✅ 已实现 |

---

## 3. 演进方向：`IRelationDetector` 统一接口（展望）

```csharp
public interface IRelationDetector
{
    string Name { get; }
    IAsyncEnumerable<IReadOnlyList<Relation>> DetectAsync(
        ManifestRepository manifest,
        CancellationToken cancellationToken);
}
```

设计要点（来自技术方案 §5.4 N-Ext）：

1. **注册表模式**：`RelationDetectorRegistry.Register(IRelationDetector)` 收集所有检测器
2. **可配置启用**：`analyze --detectors star` / `--detectors a,b`（配置哲学：默认全启用，可按需裁剪）
3. **无反射加载**：优先采用编译期注册（DI 容器）而非运行时程序集加载，保持 AOT 兼容
4. **第三方扩展**（若社区需要）：通过 `AssemblyLoadContext` 加载外部 DLL 中实现接口的检测器——此时需评估 AOT/trimming 影响

---

## 4. 安全与边界

- 插件运行在**进程内**，无沙箱隔离——第三方检测器视为可信代码
- 检测器只读 manifest + 文件系统，**无写能力**（写动作严格经 Plan→Exec 流水线）
- 未来若支持外部 DLL：建议提供

  ```text
  ① 版本兼容检查（针对契约程序集）
  ② 异常隔离（检测器异常不终止整个分析）
  ③ 路径/资源限制提示
  ```

---

## 5. 待办

- [ ] 将 static 关系检测器收敛为 `IRelationDetector` 接口 + 注册表（phase 7）
- [ ] 落地 `IScannerDriver` / `IHashProvider`（技术方案 §2.4.2/2.4.3 的可替换边界）
- [ ] 评估第三方 DLL 加载（AssemblyLoadContext）与 AOT 的兼容性
- [ ] `IPreviewProvider`（内容解析外包：Tika/ffmpeg/内置降级）——UI 线合流后