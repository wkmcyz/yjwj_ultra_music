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
                _statusOverlay.Show();
                
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
            
            _statusOverlay.UpdateListeningStatus(_core?.IsGlobalEnabled ?? false);
            _statusOverlay.UpdateGameStatus(_core?.IsGameWindowActive ?? false);
            
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
            OverlayPositionCombo.SelectedIndex = 0; // TopLeft
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
            GameProcessBox.Text = string.Join(";", global.GameProcessNames);

            // 顶部指示栏
            OverlayStyleCombo.SelectedValue = global.Overlay.Style.ToString();
            OverlayPositionCombo.SelectedValue = global.Overlay.Position.ToString();

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

                // 监听模式
                global.ListeningMode = GameWindowOnlyRadio.IsChecked == true ? 
                    ListeningMode.GameWindowOnly : ListeningMode.Global;
                global.GameProcessNames = GameProcessBox.Text.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                // 顶部指示栏
                if (Enum.TryParse<OverlayStyle>(OverlayStyleCombo.SelectedValue?.ToString(), out var style))
                    global.Overlay.Style = style;
                if (Enum.TryParse<OverlayPosition>(OverlayPositionCombo.SelectedValue?.ToString(), out var position))
                    global.Overlay.Position = position;

                _core.UpdateGlobalSettings(global);
                
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
                _core.TestPlayMapping(vm.Mapping);
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

        private void CopyConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现配置复制功能
            MessageBox.Show("配置复制功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "配置文件|*.json|所有文件|*.*",
                Title = "导入配置"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = ConfigServiceV2.ImportConfig(dialog.FileName);
                    if (config != null)
                    {
                        // TODO: 合并或替换当前配置
                        MessageBox.Show("配置导入成功", "导入", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadConfiguration();
                    }
                    else
                    {
                        MessageBox.Show("配置文件格式错误", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "配置文件|*.json|所有文件|*.*",
                Title = "导出配置",
                FileName = $"UltAssist_Config_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var success = ConfigServiceV2.ExportConfig(_core.GetConfiguration(), dialog.FileName);
                    if (success)
                    {
                        MessageBox.Show("配置导出成功", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("配置导出失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            });
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
