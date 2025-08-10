## 对话记录导出（用于在 Windows 的 Cursor 里继续协作）

时间：请以本文件的 Git 提交时间为准

### 需求（来自用户）
- 写一个永劫无间的辅助程序：玩家开大时播放指定音频，并将音频输出到用户的麦克风上（队友可听到）；大招结束时音乐停止。
- 平台：Windows。
- 可安装虚拟声卡；最好提供一键安装（主程序 + 依赖引导）。
- 事件识别：热键触发 + 视觉确认 + 视觉收尾。
- 不同英雄可配置不同音频和时长，需要淡入淡出和循环。
- 默认开大键 V；希望支持各种分辨率/窗口模式（必要时提出限制）。
- 快速小步迭代；软件未开启不影响原系统音频设置。

### 方案要点（助理给出的实现思路摘要）
- 技术栈：C#(.NET 8) + WPF；音频 NAudio（双路输出/淡入淡出/循环/限幅）；视觉 OpenCvSharp4（模板匹配）+ Windows.Graphics.Capture（ROI 捕获）。
- 音频路由：应用内将音乐同时输出到“耳机/扬声器”和“虚拟麦（如 VB-CABLE 的 CABLE Input）”；语音软件麦克风指向 `CABLE Output`，避免修改系统默认设备；进程退出不留改动。
- 事件识别：
  - 热键触发（V）→ 立即淡入播放，同时做短时间“开大确认（ROI 模板匹配）”；失败则立刻停播。
  - 播放期间以低帧率监控 ROI，判定“收大/进入冷却”→ 立即淡出停止；兜底“最长时长 + 再按 V 强停”。
- 分辨率/窗口：首次引导框选技能栏 ROI，以窗口/屏幕相对坐标保存；分辨率变化按比例换算，可快速重校。
- 打包：主程序安装器内置运行库；首次运行提示并引导安装 VB-CABLE（用户同意后静默安装），不强改系统默认设备。

### 已完成（M1 最小可用原型）
- 功能：
  - 全局热键 V 启动/强停音乐。
  - 双路输出（耳机 + 虚拟麦），淡入淡出，循环，限幅，最长时长兜底。
  - 每英雄独立配置（音频路径、音量、最大时长、是否循环）。
  - 配置持久化到 `config.json`，进程退出不影响系统默认音频设备。
- 代码位置（关键文件）：
  - `src/UltAssist/UltAssist.sln`
  - `src/UltAssist/UltAssist.csproj`
  - `src/UltAssist/App.xaml`, `src/UltAssist/App.xaml.cs`
  - `src/UltAssist/MainWindow.xaml`, `src/UltAssist/MainWindow.xaml.cs`
  - `src/UltAssist/Audio/DualOutputPlayer.cs`（双路播放/淡入淡出/循环/限幅）
  - `src/UltAssist/Core/UltStateMachine.cs`（热键触发/TTL 兜底）
  - `src/UltAssist/Input/GlobalHotkey.cs`
  - `src/UltAssist/Services/AudioDeviceService.cs`
  - `src/UltAssist/Config/Models.cs`, `src/UltAssist/Config/ConfigService.cs`
  - `src/UltAssist/Properties/AssemblyInfo.cs`
  - `src/UltAssist/config.json`（默认配置样例）
  - 根目录：`README.md`（运行说明）

### 待办（M2/M3 计划）
- M2 视觉模块：
  - ROI 校准向导（Windows.Graphics.Capture 捕获 + 拖拽框选）。
  - 模板采集（开大/冷却），阈值/连续帧判定，接入状态机“开大确认/收大停止”。
  - 误判控制：多模板、分数平滑、连续帧门限、最大时长兜底。
- M3 安装与引导：
  - 安装器（MSIX/Wix/Squirrel）+ .NET 运行时。
  - 首次运行引导安装虚拟声卡（VB-CABLE），提示语音软件麦克风切到 `CABLE Output`。

### 在 Windows 上运行（简要）
1. 安装 .NET 8 SDK。
2. 打开 `src/UltAssist/UltAssist.sln`（Visual Studio 2022），还原 NuGet 包并运行。
3. 程序界面选择“耳机/扬声器设备”和“虚拟麦设备（CABLE Input）”。
4. 在“英雄配置”选择音频、设置音量/循环/最长时长，保存。
5. 游戏里按 `V` 开始播放；再次按 `V` 强制停止。

---

### 对话逐字稿（精简标注）

1) 用户：提出需求（开大播音/路由到麦/收大停），询问实现思路。

2) 助理：给出外部无注入方案，热键 + 视觉确认/收尾，虚拟声卡路由，模块划分，技术选型（C# + NAudio + OpenCV），合规风险与问题清单。

3) 用户：确认 Windows、可装虚拟声卡、选择“热键+视觉确认+视觉收尾”、支持不同英雄/淡入淡出/循环、默认键 V；要求快速迭代与不改系统设置。

4) 助理：敲定实现方案（.NET 8 WPF），给出 M1→M2→M3 计划，详细配置项与说明，问是否先产出 M1。

5) 用户：让助理开始实现代码。

6) 助理：创建 M1 工程与核心代码（设备选择/热键/双路输出/淡入淡出/循环/配置），说明运行与测试建议。

7) 用户：要求导出聊天记录用于在 Windows 的 Cursor 继续开发。

（本文件即导出内容，供在 Windows 环境继续协作使用）

