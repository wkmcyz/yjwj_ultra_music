using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UltAssist.Config;
using UltAssist.Core;
using UltAssist.Vision;

namespace UltAssist
{
    public partial class MainWindowV2 : Window
    {
        private UltAssistCoreV2 _core = null!;
        private List<KeyMappingViewModel> _mappingViewModels = new();

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
            
            // 加载英雄列表
            LoadHeroList(config);
            
            // 加载当前英雄的按键映射
            LoadCurrentHeroMappings();
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

        private void LoadHeroList(AppConfigV2 config)
        {
            HeroCombo.ItemsSource = config.HeroConfigs.Keys.ToList();
            HeroCombo.SelectedItem = config.CurrentHero;
        }

        private void LoadCurrentHeroMappings()
        {
            var heroConfig = _core.GetCurrentHeroConfig();
            if (heroConfig == null) return;

            _mappingViewModels = heroConfig.KeyMappings.Select(m => new KeyMappingViewModel(m)).ToList();
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
            if (HeroCombo.SelectedItem is string heroName)
            {
                _core.UpdateCurrentHero(heroName);
                LoadCurrentHeroMappings();
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
                        _core.AddKeyMapping(_core.CurrentHero, mappingDialog.KeyMapping);
                        LoadCurrentHeroMappings();
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
                        _core.UpdateKeyMapping(_core.CurrentHero, dialog.KeyMapping);
                        LoadCurrentHeroMappings();
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
                    _core.RemoveKeyMapping(_core.CurrentHero, vm.Mapping.Id);
                    LoadCurrentHeroMappings();
                }
            }
        }

        private void StopAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _core.StopAllAudios();
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

        private void OnGlobalEnabledChanged(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                GlobalEnabledText.Text = enabled ? "开启" : "关闭";
                GlobalEnabledText.Foreground = enabled ? 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(163, 190, 140)) : 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 97, 106));
                
                StatusText.Text = enabled ? "监听中" : "已暂停";
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
            });
        }

        private void OnPlayingAudiosChanged(List<string> playingFiles)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentlyPlayingText.Text = playingFiles.Count > 0 ? 
                    string.Join(" | ", playingFiles) : "无";
            });
        }

        private void OnLastKeyPressed(string keyName, DateTime time)
        {
            Dispatcher.Invoke(() =>
            {
                LastKeyText.Text = keyName;
                LastKeyTimeText.Text = time.ToString("HH:mm:ss.fff");
            });
        }

        private void MainWindowV2_Closed(object? sender, EventArgs e)
        {
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
}
