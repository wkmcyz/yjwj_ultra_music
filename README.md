## UltAssist (M1)

Windows 上的永劫无间开大音乐辅助（最小可用版）。

- 功能：
  - 全局热键 V：开始/强制停止播放当前英雄音乐
  - 双路输出：耳机/扬声器 + 虚拟麦（VB-CABLE/Voicemeeter）
  - 每英雄独立音频、音量、循环、最长时长
  - 淡入/淡出、防炸麦限幅
  - 进程内播放，不修改系统默认音频设备；关闭软件不留改动
- 下一步（M2）：
  - ROI 校准 + 模板匹配：热键后“开大确认”，播放期间“收大停止”

### 构建与运行

1) Windows 上安装 .NET 8 SDK

2) 进入 `src/UltAssist` 文件夹，恢复依赖并运行：

```powershell
cd src/UltAssist
dotnet restore
dotnet run
```

3) 虚拟声卡（让队友能听到）
- 建议安装 VB-Audio Virtual Cable（CABLE Input/Output）或 Voicemeeter。
- 语音软件的“麦克风”选择 `CABLE Output`。
- 本程序里将“虚拟麦设备”选择为 `CABLE Input`，耳机选择你自己的输出设备。

### 使用
- 打开程序后，先在“输出设备”选择耳机与虚拟麦。
- 在“英雄配置”里选择英雄、选择音频文件、设置音量/时长/是否循环，保存。
- 在游戏中按 `V`：开始播放；再次按 `V`：强制停止。

### 说明
- 本版本暂不包含视觉识别；不做内存读写/注入；仅在运行时播放到指定设备。
- 程序关闭后不影响系统默认音频设备设置。

### 后续计划
- M2：添加 `Windows.Graphics.Capture` + `OpenCvSharp` 做 ROI 捕获与模板匹配。
- M3：安装器与虚拟声卡一键安装引导。

