using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UltAssist.Config;
using UltAssist.Core;
using UltAssist.Input;
using UltAssist.Logging;
using UltAssist.UI;
using UltAssist.Vision;

namespace UltAssist
{
    public partial class MainWindowV2 : Window
    {
        private UltAssistCoreV2 _core = null!;
        private List<KeyMappingViewModel> _mappingViewModels = new();
        private StatusOverlayWindow? _statusOverlay;
        private int _devClickCount = 0;
        private DateTime _lastDevClick = DateTime.MinValue;
        private Button? _currentTestButton; // 跟踪当前正在测试播放的按钮
        private string? _currentTestKeyId; // 跟踪当前测试播放的按键ID
        private KeyCombination? _globalToggleHotkey; // 存储录制的全局开关快捷键

        public MainWindowV2()
        {
            InitializeComponent();
            Loaded += MainWindowV2_Loaded;
            Closed += MainWindowV2_Closed;
        }

        private void MainWindowV2_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化核心系统
                _core = new UltAssistCoreV2();
                ReconnectCoreEvents();

                // 初始化状态栏
                InitializeStatusOverlay();

                // 初始化UI
                InitializeUI();
                LoadConfiguration();
                
                StatusText.Text = "系统已就绪";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReconnectCoreEvents()
        {
            if (_core == null) return;
            
            // 订阅事件
            _core.GlobalEnabledChanged += OnGlobalEnabledChanged;
            _core.GameWindowActiveChanged += OnGameWindowActiveChanged;
            _core.PlayingAudiosChanged += OnPlayingAudiosChanged;
            _core.LastKeyPressedChanged += OnLastKeyPressed;
        }

        private void InitializeStatusOverlay()
        {
            try
            {
                _statusOverlay = new StatusOverlayWindow();
                _statusOverlay.MinimizeMainWindow += OnMinimizeMainWindow;
                _statusOverlay.CloseApplication += OnCloseApplication;
                
                // 初始化状态显示
                UpdateStatusOverlay();
            }
            catch (Exception ex)
            {
                // 状态栏初始化失败时不影响主程序
                Console.WriteLine($"状态栏初始化失败: {ex.Message}");
            }
        }

        private void UpdateStatusOverlay()
        {
            if (_statusOverlay == null) return;
            
            // 设置显示样式
            var overlayStyle = _core?.Config?.Global?.Overlay?.Style ?? OverlayStyle.None;
            _statusOverlay.SetDisplayStyle(overlayStyle);
            
            // 更新状态信息
            _statusOverlay.UpdateListeningStatus(_core?.IsGlobalEnabled ?? false);
            _statusOverlay.UpdateGameStatus(_core?.IsGameWindowActive ?? false);
            
            // 设置目标进程名
            if (_core?.Config?.Global?.GameProcessNames?.Count > 0)
            {
                _statusOverlay.SetTargetProcessName(_core.Config.Global.GameProcessNames[0]);
            }
        }

        private void InitializeUI()
        {
            // 初始化设备列表（转换为简单数据结构避免卡顿）
            try
            {
                var audioService = new Services.AudioDeviceService();
                var renderDevices = audioService.GetRenderDevices();
                
                // 转换为简单的显示模型，避免复杂对象导致的性能问题
                var deviceItems = renderDevices.Select(d => new AudioDeviceItem 
                { 
                    Id = d.ID, 
                    Name = d.FriendlyName,
                    Device = d
                }).ToList();
                
                HeadphoneCombo.ItemsSource = deviceItems;
                VirtualMicCombo.ItemsSource = deviceItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取音频设备失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 初始化overlay配置
            OverlayStyleCombo.SelectedIndex = 0; // None
        }

        private void LoadConfiguration()
        {
            var config = _core.GetConfiguration();

            // 加载全局设置
            LoadGlobalSettings(config.Global);
            
            // 加载方案列表
            LoadProfileList(config);
            
            // 加载当前方案的按键映射
            LoadCurrentProfileMappings();
        }

        private void LoadGlobalSettings(GlobalSettings global)
        {
            // 设备选择
            if (HeadphoneCombo.ItemsSource is List<AudioDeviceItem> headphoneItems)
            {
                HeadphoneCombo.SelectedItem = headphoneItems.FirstOrDefault(i => i.Id == global.HeadphoneDeviceId);
            }
            if (VirtualMicCombo.ItemsSource is List<AudioDeviceItem> virtualMicItems)
            {
                VirtualMicCombo.SelectedItem = virtualMicItems.FirstOrDefault(i => i.Id == global.VirtualMicDeviceId);
            }
            TempDefaultMicCheck.IsChecked = global.TemporarilySetDefaultMic;

            // 监听模式
            GameWindowOnlyRadio.IsChecked = global.ListeningMode == ListeningMode.GameWindowOnly;
            GlobalListenRadio.IsChecked = global.ListeningMode == ListeningMode.Global;
            GameProcessCombo.Text = string.Join(";", global.GameProcessNames);
            
            // 更新游戏进程面板可见性（延迟执行确保UI已初始化）
            Dispatcher.BeginInvoke(() => UpdateGameProcessPanelVisibility());

            // Debug模式
            DebugModeCheck.IsChecked = global.DebugMode;
            Dispatcher.BeginInvoke(() => UpdateDebugPanelVisibility());

            // 顶部指示栏
            SetComboBoxSelection(OverlayStyleCombo, global.Overlay.Style.ToString());

            // 全局开关快捷键
            _globalToggleHotkey = global.GlobalToggleHotkey;
            GlobalHotkeyBox.Text = global.GlobalToggleHotkey?.ToDisplayString() ?? "未设置";

            // 状态显示
            GlobalEnabledText.Text = global.GlobalListenerEnabled ? "开启" : "关闭";
            GlobalEnabledText.Foreground = global.GlobalListenerEnabled ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(163, 190, 140)) : 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 97, 106));
        }

        private void LoadProfileList(AppConfigV2 config)
        {
            var profileItems = config.Profiles.Select(p => new ProfileDisplayItem 
            { 
                Id = p.Id, 
                Name = p.Name, 
                Description = p.Description,
                Profile = p
            }).ToList();
            
            HeroCombo.ItemsSource = profileItems;
            HeroCombo.DisplayMemberPath = "DisplayName";
            HeroCombo.SelectedValuePath = "Id";
            HeroCombo.SelectedValue = config.CurrentProfile;
        }

        private void LoadCurrentProfileMappings()
        {
            var profile = _core.GetCurrentProfile();
            if (profile == null) return;

            _mappingViewModels = profile.KeyMappings.Select(m => new KeyMappingViewModel(m)).ToList();
            KeyMappingsList.ItemsSource = _mappingViewModels;
        }

        private void SaveGlobalBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var global = new GlobalSettings();

                // 音频设备
                if (HeadphoneCombo.SelectedItem is AudioDeviceItem headphone)
                    global.HeadphoneDeviceId = headphone.Id;
                if (VirtualMicCombo.SelectedItem is AudioDeviceItem virtualMic)
                    global.VirtualMicDeviceId = virtualMic.Id;
                global.TemporarilySetDefaultMic = TempDefaultMicCheck.IsChecked ?? false;

                // Debug模式
                global.DebugMode = DebugModeCheck.IsChecked ?? false;

                // 监听模式
                global.ListeningMode = GameWindowOnlyRadio.IsChecked == true ? 
                    ListeningMode.GameWindowOnly : ListeningMode.Global;
                // 处理游戏进程名，如果包含中文说明则只取进程名部分
                var processText = GameProcessCombo.Text;
                var processNames = processText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => 
                    {
                        var trimmed = p.Trim();
                        
                        // 过滤掉分割线和说明文本
                        if (trimmed.StartsWith("─") || trimmed.StartsWith("📋") || trimmed.StartsWith("🎮") ||
                            trimmed.Contains("以下为当前设备上检测到的进程") ||
                            trimmed.Contains("常见游戏进程"))
                        {
                            return null;
                        }
                        
                        // 如果包含空格和括号，说明有中文说明，只取第一部分
                        var spaceIndex = trimmed.IndexOf(' ');
                        return spaceIndex > 0 ? trimmed.Substring(0, spaceIndex) : trimmed;
                    })
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                global.GameProcessNames = processNames;

                // 顶部指示栏（只保存样式，位置设置为默认值）
                if (OverlayStyleCombo.SelectedItem is ComboBoxItem styleItem &&
                    Enum.TryParse<OverlayStyle>(styleItem.Tag?.ToString(), out var style))
                    global.Overlay.Style = style;
                global.Overlay.Position = OverlayPosition.TopLeft; // 固定默认位置

                // 全局开关快捷键
                global.GlobalToggleHotkey = _globalToggleHotkey;

                _core.UpdateGlobalSettings(global);
                
                // 更新状态栏显示
                UpdateStatusOverlay();
                
                MessageBox.Show("全局设置已保存", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HeroCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HeroCombo.SelectedItem is ProfileDisplayItem profileItem)
            {
                _core.SwitchProfile(profileItem.Id);
                LoadCurrentProfileMappings();
            }
        }

        private void AddMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开按键录制对话框（新的录制器不会与主程序冲突）
                var dialog = new KeyRecordingDialog();
                if (dialog.ShowDialog() == true && dialog.RecordedKey != null)
                {
                    var mappingDialog = new KeyMappingEditDialog(dialog.RecordedKey);
                    if (mappingDialog.ShowDialog() == true)
                    {
                        _core.AddKeyMapping(_core.CurrentProfile, mappingDialog.KeyMapping);
                        LoadCurrentProfileMappings();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is KeyMappingViewModel vm)
            {
                var keyId = vm.Mapping.Keys.ToDisplayString();
                
                // 如果当前正在播放这个音频，则停止
                if (_currentTestButton == button && _currentTestKeyId == keyId)
                {
                    _core.StopAudio(keyId);
                    button.Content = "▶️ 测试";
                    _currentTestButton = null;
                    _currentTestKeyId = null;
                }
                else
                {
                    // 停止之前的测试播放
                    if (_currentTestButton != null && !string.IsNullOrEmpty(_currentTestKeyId))
                    {
                        _core.StopAudio(_currentTestKeyId);
                        _currentTestButton.Content = "▶️ 测试";
                    }
                    
                    // 开始新的测试播放
                    _core.TestPlayMapping(vm.Mapping);
                    button.Content = "⏹️ 停止";
                    _currentTestButton = button;
                    _currentTestKeyId = keyId;
                }
            }
        }

        private void EditMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is KeyMappingViewModel vm)
            {
                var dialog = new KeyMappingEditDialog(vm.Mapping);
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        _core.UpdateKeyMapping(_core.CurrentProfile, dialog.KeyMapping);
                        LoadCurrentProfileMappings();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is KeyMappingViewModel vm)
            {
                var result = MessageBox.Show($"确定要删除按键映射 \"{vm.KeyDisplayText}\" 吗？", 
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _core.RemoveKeyMapping(_core.CurrentProfile, vm.Mapping.Id);
                    LoadCurrentProfileMappings();
                }
            }
        }

        private void StopAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _core.StopAllAudios();
        }

        private void AddProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建新方案对话框
                var dialog = new ProfileEditDialog();
                if (dialog.ShowDialog() == true)
                {
                    var newProfile = new ConfigProfile
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = dialog.ProfileName,
                        Description = dialog.ProfileDescription,
                        KeyMappings = new List<KeyMapping>()
                    };

                    _core.AddProfile(newProfile);
                    _core.SwitchProfile(newProfile.Id);
                    
                    LoadProfileList(_core.Config);
                    LoadCurrentProfileMappings();
                    
                    MessageBox.Show($"方案 \"{newProfile.Name}\" 创建成功！", "新增方案", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (HeroCombo.SelectedItem is ProfileDisplayItem profileItem)
            {
                var result = MessageBox.Show($"确定要删除方案 \"{profileItem.Name}\" 吗？\n\n此操作将删除该方案下的所有按键映射，且无法撤销。", 
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _core.RemoveProfile(profileItem.Id);
                        LoadProfileList(_core.Config);
                        LoadCurrentProfileMappings();
                        
                        MessageBox.Show($"方案 \"{profileItem.Name}\" 已删除", "删除成功", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择要删除的方案", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void ImportConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "导入配置",
                    Filter = "配置包 (*.zip)|*.zip|配置文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "zip"
                };

                if (openDialog.ShowDialog() == true)
                {
                    // 先提示用户确认
                    var fileExt = Path.GetExtension(openDialog.FileName).ToLower();
                    var warningMessage = fileExt == ".zip" 
                        ? "导入配置包将覆盖当前所有设置并导入音乐文件，是否继续？\n\n建议在导入前先备份当前配置。"
                        : "导入配置将覆盖当前所有设置，是否继续？\n\n注意：JSON文件不包含音乐文件，建议使用ZIP配置包。\n\n建议在导入前先备份当前配置。";

                    var result = MessageBox.Show(warningMessage, "确认导入", 
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        bool success = false;
                        string successMessage = "";

                        if (fileExt == ".zip")
                        {
                            // 导入配置包（包含音乐文件）
                            success = ConfigServiceV2.ImportConfigPackage(openDialog.FileName);
                            successMessage = "配置包导入成功！\n\n配置文件和音乐文件已导入。\n应用将重新加载配置。";
                        }
                        else
                        {
                            // 导入仅配置文件（兼容旧版本）
                            success = ConfigServiceV2.ImportConfig(openDialog.FileName);
                            successMessage = "配置文件导入成功！\n\n注意：音乐文件未导入，请检查音乐文件路径。\n应用将重新加载配置。";
                        }

                        if (success)
                        {
                            MessageBox.Show(successMessage, "导入成功", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // 重新加载配置
                            _core.LoadConfiguration();
                            LoadConfiguration();
                            UpdateStatusOverlay();
                        }
                        else
                        {
                            MessageBox.Show("配置导入失败!\n\n请检查文件格式是否正确。", "错误", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出配置包",
                    Filter = "配置包 (*.zip)|*.zip|配置文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "zip",
                    FileName = $"UltAssist_Package_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    bool success = false;
                    string successMessage = "";

                    // 根据文件扩展名决定导出方式
                    if (Path.GetExtension(saveDialog.FileName).ToLower() == ".zip")
                    {
                        // 导出包含音乐文件的ZIP包
                        success = ConfigServiceV2.ExportConfigPackage(saveDialog.FileName);
                        successMessage = $"配置包已导出到:\n{saveDialog.FileName}\n\n包含配置文件和所有音乐文件。";
                    }
                    else
                    {
                        // 导出仅配置文件（兼容旧版本）
                        success = ConfigServiceV2.ExportConfig(saveDialog.FileName);
                        successMessage = $"配置文件已导出到:\n{saveDialog.FileName}\n\n注意：仅包含配置，不含音乐文件。";
                    }

                    if (success)
                    {
                        MessageBox.Show(successMessage, "导出成功", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("配置导出失败!", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectRoiBtn_Click(object sender, RoutedEventArgs e)
        {
            // 保留的ROI选择功能（用于将来扩展）
            var selector = new RoiSelectorWindow();
            selector.ShowDialog();
        }

        private void OpenLogBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logFile = EventLogger.GetCurrentLogFile();
                if (File.Exists(logFile))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logFile,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("日志文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLogDirBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logDir = EventLogger.GetLogDirectory();
                if (Directory.Exists(logDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("日志目录不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshProcessBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前运行的所有进程
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .Select(p => p.ProcessName + ".exe")
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                // 过滤掉系统进程，只显示可能的游戏进程
                var gameProcesses = processes.Where(p => 
                    !p.StartsWith("svchost", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("System", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("Registry", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("dwm", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("winlogon", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("csrss", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("smss", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("wininit", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("services", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("lsass", StringComparison.OrdinalIgnoreCase) &&
                    !p.StartsWith("explorer", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                // 添加一些常见的游戏进程到顶部（带中文名称说明）
                var commonGameProcesses = new List<string>
                {
                    "NarakaBladepoint.exe (永劫无间)",
                    "csgo.exe (反恐精英：全球攻势)",
                    "valorant.exe (无畏契约)",
                    "League of Legends.exe (英雄联盟)",
                    "Overwatch.exe (守望先锋)",
                    "ApexLegends.exe (Apex英雄)",
                    "TslGame.exe (绝地求生)",
                    "FortniteClient-Win64-Shipping.exe (堡垒之夜)",
                    "RainbowSix.exe (彩虹六号：围攻)",
                    "Genshin Impact.exe (原神)",
                    "StarRail.exe (崩坏：星穹铁道)",
                    "ZenlessZoneZero.exe (绝区零)",
                    "WutheringWaves.exe (鸣潮)",
                    "CrossFire.exe (穿越火线)",
                    "DNF.exe (地下城与勇士)",
                    "WorldOfWarcraft.exe (魔兽世界)",
                    "Wow.exe (魔兽世界)",
                    "destiny2.exe (命运2)",
                    "RocketLeague.exe (火箭联盟)",
                    "DeadByDaylight.exe (黎明杀机)"
                };

                // 从常见游戏进程中提取进程名（去掉中文说明部分）
                var commonProcessNames = commonGameProcesses
                    .Select(p => p.Split(' ')[0]) // 取第一部分（进程名）
                    .ToHashSet();

                // 合并列表：常见游戏（带说明）+ 分割线 + 其他进程（不在常见列表中的）
                var otherProcesses = gameProcesses.Where(p => !commonProcessNames.Contains(p)).ToList();
                
                var allProcesses = new List<string>();
                
                // 添加常见游戏进程
                if (commonGameProcesses.Count > 0)
                {
                    allProcesses.Add("🎮 常见游戏进程 (推荐):");
                    allProcesses.Add("─────────────────────────────────────");
                    allProcesses.AddRange(commonGameProcesses);
                }
                
                // 添加分割线
                if (otherProcesses.Count > 0)
                {
                    allProcesses.Add("─────────────────────────────────────");
                    allProcesses.Add("📋 以下为当前设备上检测到的进程:");
                    allProcesses.Add("─────────────────────────────────────");
                    allProcesses.AddRange(otherProcesses);
                }

                // 更新ComboBox
                GameProcessCombo.ItemsSource = allProcesses;

                MessageBox.Show($"已刷新进程列表，找到 {gameProcesses.Count} 个可选进程", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新进程列表失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListeningModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateGameProcessPanelVisibility();
        }

        private void UpdateGameProcessPanelVisibility()
        {
            // 确保UI控件已经初始化
            if (GameWindowOnlyRadio == null || GameProcessPanel == null)
                return;
                
            // 只有在"仅游戏窗口"模式下才显示游戏进程配置
            if (GameWindowOnlyRadio.IsChecked == true)
            {
                GameProcessPanel.Visibility = Visibility.Visible;
            }
            else
            {
                GameProcessPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateDebugPanelVisibility()
        {
            // 确保UI控件已经初始化
            if (DebugModeCheck == null || DebugPanel == null)
                return;
                
            // 只有在Debug模式下才显示Debug面板
            if (DebugModeCheck.IsChecked == true)
            {
                DebugPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DebugPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void DebugModeCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDebugPanelVisibility();
        }

        private void DevAccessText_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            
            // 如果距离上次点击超过3秒，重置计数
            if ((now - _lastDevClick).TotalSeconds > 3)
            {
                _devClickCount = 0;
            }
            
            _devClickCount++;
            _lastDevClick = now;
            
            // 连续点击3次启用Debug模式
            if (_devClickCount >= 3)
            {
                DebugModeCheck.IsChecked = true;
                UpdateDebugPanelVisibility();
                _devClickCount = 0;
                MessageBox.Show("Debug模式已启用", "开发者模式", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetComboBoxSelection(ComboBox comboBox, string tagValue)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tagValue)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void OnGlobalEnabledChanged(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                GlobalEnabledText.Text = enabled ? "开启" : "关闭";
                GlobalEnabledText.Foreground = enabled ? 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(163, 190, 140)) : 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 97, 106));
                
                StatusText.Text = enabled ? "监听中" : "已暂停";
                
                // 更新状态栏
                _statusOverlay?.UpdateListeningStatus(enabled);
            });
        }

        private void OnGameWindowActiveChanged(bool isActive)
        {
            Dispatcher.Invoke(() =>
            {
                GameWindowText.Text = isActive ? "已检测到" : "未检测到";
                GameWindowText.Foreground = isActive ? 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(163, 190, 140)) : 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 203, 139));
                
                // 更新状态栏
                _statusOverlay?.UpdateGameStatus(isActive);
            });
        }

        private void OnPlayingAudiosChanged(List<string> playingFiles)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentlyPlayingText.Text = playingFiles.Count > 0 ? 
                    string.Join(" | ", playingFiles) : "无";
                
                // 更新状态栏
                _statusOverlay?.UpdatePlayingAudios(playingFiles.ToArray());
                
                // 检查测试播放按钮状态
                UpdateTestButtonState(playingFiles);
            });
        }

        private void UpdateTestButtonState(List<string> playingFiles)
        {
            if (_currentTestButton != null && !string.IsNullOrEmpty(_currentTestKeyId))
            {
                // 检查当前测试的音频是否还在播放
                var isPlaying = _core?.CurrentlyPlayingFiles?.Contains(
                    System.IO.Path.GetFileName(GetTestAudioFilePath(_currentTestKeyId))) ?? false;
                
                if (!isPlaying)
                {
                    // 播放已停止，恢复按钮状态
                    _currentTestButton.Content = "▶️ 测试";
                    _currentTestButton = null;
                    _currentTestKeyId = null;
                }
            }
        }

        private string? GetTestAudioFilePath(string keyId)
        {
            var profile = _core?.Config?.Profiles?.FirstOrDefault(p => p.Name == _core.CurrentProfile);
            var mapping = profile?.KeyMappings?.FirstOrDefault(m => m.Keys.ToDisplayString() == keyId);
            return mapping?.Audio?.FilePath;
        }

        private void OnLastKeyPressed(string keyName, DateTime time)
        {
            Dispatcher.Invoke(() =>
            {
                LastKeyText.Text = keyName;
                LastKeyTimeText.Text = time.ToString("HH:mm:ss.fff");
                
                // 更新状态栏
                _statusOverlay?.UpdateLastKey(keyName, time);
            });
        }

        private void OnMinimizeMainWindow()
        {
            Dispatcher.Invoke(() =>
            {
                WindowState = WindowState.Minimized;
            });
        }

        private void OnCloseApplication()
        {
            Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }

        private void MainWindowV2_Closed(object? sender, EventArgs e)
        {
            _statusOverlay?.Close();
            _core?.Dispose();
        }



        private void BackupConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "备份配置",
                    Filter = "配置备份 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"UltAssist_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    if (ConfigServiceV2.BackupConfig(saveDialog.FileName))
                    {
                        MessageBox.Show($"配置已备份到:\n{saveDialog.FileName}", "备份成功", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("配置备份失败!", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"备份失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetGlobalHotkeyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new KeyRecordingDialog();
                dialog.Owner = this;
                dialog.Title = "录制全局开关快捷键";
                
                if (dialog.ShowDialog() == true && dialog.RecordedKey != null)
                {
                    _globalToggleHotkey = dialog.RecordedKey;
                    GlobalHotkeyBox.Text = dialog.RecordedKey.ToDisplayString();
                    UpdateSaveButtonState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"录制失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearGlobalHotkeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _globalToggleHotkey = null;
            GlobalHotkeyBox.Text = "未设置";
            UpdateSaveButtonState();
        }

        private void UpdateSaveButtonState()
        {
            // 可以在这里添加保存按钮状态更新逻辑
        }
    }

    // 按键映射的ViewModel，用于UI绑定
    public class KeyMappingViewModel
    {
        public KeyMapping Mapping { get; }

        public KeyMappingViewModel(KeyMapping mapping)
        {
            Mapping = mapping;
        }

        public string KeyDisplayText => Mapping.Keys.ToDisplayString();
        
        public string AudioFileName => string.IsNullOrEmpty(Mapping.Audio.FilePath) ? 
            "未设置" : Path.GetFileName(Mapping.Audio.FilePath);
        
        public string DisplayName => string.IsNullOrEmpty(Mapping.DisplayName) ? 
            $"映射 {KeyDisplayText}" : Mapping.DisplayName;
        
        public string SettingsText
        {
            get
            {
                var settings = new List<string>();
                settings.Add(Mapping.ExactMatch ? "精准匹配" : "包含匹配");
                if (Mapping.Audio.Loop) settings.Add("循环");
                if (!Mapping.Audio.Interruptible) settings.Add("不可打断");
                settings.Add($"音量 {Mapping.Audio.Volume:P0}");
                settings.Add($"淡入 {Mapping.Audio.FadeInMs}ms");
                return string.Join(" | ", settings);
            }
        }
    }

    // 音频设备显示项，避免直接使用MMDevice导致的性能问题
    public class AudioDeviceItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public NAudio.CoreAudioApi.MMDevice Device { get; set; } = null!;
        
        public override string ToString() => Name;
    }

    // 配置文件显示项
    public class ProfileDisplayItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConfigProfile Profile { get; set; } = null!;
        
        public string DisplayName => string.IsNullOrEmpty(Description) ? Name : $"{Name} ({Description})";
        
        public override string ToString() => DisplayName;
    }
}
