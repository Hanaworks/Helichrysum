# CLAUDE.md — Helichrysum 项目规范

本文件为 AI 编码代理（Claude Code 及同类工具）在本仓库工作时的**硬性规则**。与 `docs/technical-design.md` §1.1 设计原则同源。

---

## 硬性规则（5 条，不可违反）

### 规则 1：命名不使用缩写

代码命名（类型、成员、变量、文件名、配置键、表名/列名）一律使用完整、自描述的命名，**不使用缩写形式**。

```csharp
// 正确
manifestRepository
scanCompletedCount
Helichrysum.Filesystem     // 而非 Helichrysum.Fs
FilesystemObject           // 而非 FsObject

// 错误
manifRepo
scanCnt
```

**例外**：仅限业界通用标准术语——`IO` / `SQL` / `HTTP` / `JSON` / `GUI` / `CLI` / `API` 等。

### 规则 2：复杂度控制第一原则

- **简单优先**：原则上不增加系统复杂度
- 优先利用现有框架/架构能力，而非创建新的抽象层/中间层
- 仅当确实能**降低整体复杂度**时，才考虑增加复杂度的方案
- **禁止重复造轮子**：现有框架/架构已提供的功能绝不重复实现

### 规则 3：代码美学与换行规范（强制执行）

- **完全禁止 120 字符自动换行限制**——不因行长度超过 120 字符就强行折行
- 仅在确实能提高可读性的情况下换行：链式调用、Builder 模式、参数过多、长表达式
- 简单代码、初始化配置、日志语句、短方法调用等**优先保持单行**，不做无意义拆分
- **链式调用对齐**：第一个方法调用紧跟在变量后不换行，后续每个 `.Method()` 的 `.` 与第一个方法调用的 `.` 严格垂直对齐：

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

### 规则 4：初版 WebUI 验证交互（WebUI-First，限初版窗口期）

- **背景**：项目初版的 UX 未经真实使用验证与需求设计，交互是否合理、好用是未知的（目前只有功能性设计）
- **规则**：**初版开发期**，展示层一律先经 **WebUI 验证交互**（WebUI 是廉价快速迭代的"交互实验室"）：先把功能性能力跑起来，通过 WebUI 亲手操作验证交互逻辑合理性与用户体验
- **初版交互验证定稿后，才开始做 NativeUI（WPF/WinUI）**——桌面壳是定稿交互的复刻落地，不反向
- 不跳过 WebUI 直接写 WPF/WinUI；此规则限于初版 UX 验证窗口期，并非"所有 UI 永远先 WebUI"，交互定型后新功能可直接落在既有壳上

### 规则 5：切片实施流程（每个切片/里程碑开工前必须执行）

```text
① 编写切片计划（docs/slice-<n>-plan.md）
   ├─ 实现目标（验收命令）
   ├─ 任务计划与功能点（按依赖排序）
   └─ TDD 测试用例（测试名 + 断言 + 涉及 F-xxx）
② 确认计划（用户审阅）
③ 按任务顺序逐个实现（TDD：红 → 绿 → 重构）
④ 端到端验收（跑验收命令，用户亲手确认）
⑤ 切片完成，计划文档标注状态
```

---

## 附：项目关键约定（背景）

- 技术栈：.NET 8 / C#，SQLite（manifest），Spectre.Console（CLI），ASP.NET Core（WebUI），WPF（桌面壳，Windows 优先）
- 核心架构：单一核心 `Helichrysum.Core` + 平台抽象 `Helichrysum.Filesystem` + `Helichrysum.Cli` / `Helichrysum.Desktop` / Web 各壳
- 实施方式：垂直切片 + TDD（红-绿-重构）+ fixture 目录树测试 + 需求全覆盖（每条 F-xxx 有对应测试）
- 完整设计见 `docs/technical-design.md` 与 `docs/implementation-plan.md`