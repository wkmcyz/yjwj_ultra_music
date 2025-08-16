using System;
using System.IO;
using System.Text;
using System.Threading;

namespace UltAssist.Logging
{
    public static class EventLogger
    {
        private static readonly object _lock = new object();
        private static string _logDirectory = string.Empty;
        private static string _currentLogFile = string.Empty;

        static EventLogger()
        {
            InitializeLogger();
        }

        private static void InitializeLogger()
        {
            try
            {
                // 创建日志目录
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                _logDirectory = Path.Combine(exeDir ?? ".", "logs");
                Directory.CreateDirectory(_logDirectory);

                // 创建当前会话的日志文件
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _currentLogFile = Path.Combine(_logDirectory, $"UltAssist_{timestamp}.log");

                // 写入会话开始标记
                LogEvent("SYSTEM", "会话开始", $"UltAssist V2 启动");
            }
            catch (Exception ex)
            {
                // 如果日志初始化失败，至少记录到控制台
                Console.WriteLine($"日志初始化失败: {ex.Message}");
            }
        }

        public static void LogEvent(string category, string action, string details = "", params object[] args)
        {
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    
                    var formattedDetails = args.Length > 0 ? string.Format(details, args) : details;
                    var logLine = $"[{timestamp}] [{threadId:D3}] [{category}] {action}: {formattedDetails}";

                    // 写入文件
                    File.AppendAllText(_currentLogFile, logLine + Environment.NewLine, Encoding.UTF8);

                    // 同时输出到控制台（调试用）
                    Console.WriteLine(logLine);
                }
            }
            catch (Exception ex)
            {
                // 如果写入日志失败，至少输出到控制台
                Console.WriteLine($"日志写入失败: {ex.Message}");
                Console.WriteLine($"原始日志: [{category}] {action}: {details}");
            }
        }

        public static void LogKeyPress(string keyName, bool hasMapping, string heroName, string mappingInfo = "")
        {
            var details = hasMapping 
                ? $"按键={keyName}, 方案={heroName}, 映射={mappingInfo}"
                : $"按键={keyName}, 方案={heroName}, 无映射";
            
            LogEvent("INPUT", hasMapping ? "按键触发" : "按键无映射", details);
        }

        public static void LogAudioPlay(string keyName, string audioFile, string heroName, bool isInterruptible)
        {
            LogEvent("AUDIO", "开始播放", "按键={0}, 文件={1}, 方案={2}, 可打断={3}", 
                keyName, Path.GetFileName(audioFile), heroName, isInterruptible);
        }

        public static void LogAudioStop(string audioFile, string reason)
        {
            LogEvent("AUDIO", "停止播放", "文件={0}, 原因={1}", 
                Path.GetFileName(audioFile), reason);
        }

        public static void LogAudioError(string audioFile, string error)
        {
            LogEvent("AUDIO", "播放错误", "文件={0}, 错误={1}", 
                Path.GetFileName(audioFile), error);
        }

        public static void LogDeviceChange(string deviceType, string deviceName, string action)
        {
            LogEvent("DEVICE", action, "类型={0}, 设备={1}", deviceType, deviceName);
        }

        public static void LogGameWindow(bool isActive, string processName = "")
        {
            LogEvent("GAME", isActive ? "窗口激活" : "窗口失焦", "进程={0}", processName);
        }

        public static void LogGlobalToggle(bool enabled, string trigger)
        {
            LogEvent("GLOBAL", enabled ? "启用监听" : "禁用监听", "触发方式={0}", trigger);
        }

        public static void LogError(string category, string action, Exception ex)
        {
            LogEvent("ERROR", $"{category}_{action}", "异常={0}, 消息={1}, 堆栈={2}", 
                ex.GetType().Name, ex.Message, ex.StackTrace?.Replace(Environment.NewLine, " | "));
        }

        public static void LogSystemInfo(string category, string info)
        {
            LogEvent("SYSTEM", category, info);
        }

        public static string GetCurrentLogFile()
        {
            return _currentLogFile;
        }

        public static string GetLogDirectory()
        {
            return _logDirectory;
        }

        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "UltAssist_*.log");
                
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(logFile);
                        LogEvent("SYSTEM", "清理旧日志", "删除文件={0}", Path.GetFileName(logFile));
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent("ERROR", "清理日志失败", "错误={0}", ex.Message);
            }
        }
    }
}
