using System;
using System.Collections.Generic;
using System.Linq;
using UltAssist.Config;

namespace UltAssist.Input
{
    public class InputManagerV2 : IDisposable
    {
        private readonly KeyListenerV2 _keyListener;
        private readonly MouseListenerV2 _mouseListener;
        
        // 当前按下的所有按键（键盘+鼠标）
        private readonly HashSet<string> _currentlyPressed = new();
        
        // 全局开关状态（Ctrl+1控制）
        private bool _globalEnabled = true;
        
        // 配置
        private ListeningMode _listeningMode = ListeningMode.GameWindowOnly;
        private List<string> _gameProcessNames = new();

        // 事件
        public event Action<KeyCombination>? KeyCombinationTriggered;
        public event Action<bool>? GlobalEnabledChanged;
        public event Action<bool>? GameWindowActiveChanged;

        public bool GlobalEnabled => _globalEnabled;
        public bool IsGameWindowActive => _keyListener.IsGameWindowActive;

        public InputManagerV2()
        {
            _keyListener = new KeyListenerV2();
            _mouseListener = new MouseListenerV2();

            // 订阅键盘事件
            _keyListener.KeyCombinationPressed += OnKeyCombinationPressed;
            _keyListener.GameWindowActiveChanged += OnGameWindowActiveChanged;

            // 订阅鼠标事件
            _mouseListener.MouseButtonPressed += OnMouseButtonPressed;
            _mouseListener.MouseButtonReleased += OnMouseButtonReleased;
        }

        public void UpdateSettings(GlobalSettings settings)
        {
            _listeningMode = settings.ListeningMode;
            _gameProcessNames = settings.GameProcessNames?.ToList() ?? new();
            _globalEnabled = settings.GlobalListenerEnabled;

            // 更新子监听器
            _keyListener.UpdateSettings(_listeningMode, _gameProcessNames);
            _keyListener.Enabled = _globalEnabled;
            _mouseListener.Enabled = _globalEnabled;
        }

        public void SetGlobalEnabled(bool enabled)
        {
            if (_globalEnabled != enabled)
            {
                _globalEnabled = enabled;
                _keyListener.Enabled = enabled;
                _mouseListener.Enabled = enabled;
                
                if (!enabled)
                {
                    _currentlyPressed.Clear(); // 清除按键状态
                }
                
                GlobalEnabledChanged?.Invoke(enabled);
            }
        }

        private void OnKeyCombinationPressed(KeyCombination combination)
        {
            if (!_globalEnabled) return;

            // 检查是否是全局开关按键 Ctrl+1
            if (IsGlobalToggleKey(combination))
            {
                SetGlobalEnabled(!_globalEnabled);
                return;
            }

            // 检查是否应该监听（根据监听模式）
            bool shouldListen = _listeningMode == ListeningMode.Global || 
                               (_listeningMode == ListeningMode.GameWindowOnly && _keyListener.IsGameWindowActive);
            
            if (!shouldListen) return;

            // 触发按键组合事件
            KeyCombinationTriggered?.Invoke(combination);
        }

        private void OnMouseButtonPressed(string buttonName)
        {
            if (!_globalEnabled) return;
            
            _currentlyPressed.Add(buttonName);
            CheckForCombinationWithMouse();
        }

        private void OnMouseButtonReleased(string buttonName)
        {
            _currentlyPressed.Remove(buttonName);
        }

        private void CheckForCombinationWithMouse()
        {
            if (_currentlyPressed.Count == 0) return;

            // 检查是否应该监听（根据监听模式）
            bool shouldListen = _listeningMode == ListeningMode.Global || 
                               (_listeningMode == ListeningMode.GameWindowOnly && _keyListener.IsGameWindowActive);
            
            if (!shouldListen) return;

            // 创建包含鼠标按键的组合键
            var keys = _currentlyPressed.ToList();
            var combination = new KeyCombination { Keys = keys };
            
            KeyCombinationTriggered?.Invoke(combination);
        }

        private void OnGameWindowActiveChanged(bool isActive)
        {
            GameWindowActiveChanged?.Invoke(isActive);
        }

        private static bool IsGlobalToggleKey(KeyCombination combination)
        {
            // 检查是否是 Ctrl+1 组合键
            return combination.Keys.Count == 2 &&
                   combination.Keys.Contains("Ctrl") &&
                   combination.Keys.Contains("1");
        }

        // 判断某个按键组合是否被禁止（不能与全局开关冲突）
        public static bool IsProhibitedKeyCombination(KeyCombination combination)
        {
            // 禁止配置 Ctrl+1（全局开关）
            if (IsGlobalToggleKey(combination)) return true;
            
            // 禁止配置 ESC（录制取消键）
            if (combination.Keys.Count == 1 && combination.Keys.Contains("Escape")) return true;
            
            return false;
        }

        public void Dispose()
        {
            _keyListener?.Dispose();
            _mouseListener?.Dispose();
        }
    }
}
