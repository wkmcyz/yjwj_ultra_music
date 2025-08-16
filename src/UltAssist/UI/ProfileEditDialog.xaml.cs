using System;
using System.Windows;

namespace UltAssist.UI
{
    public partial class ProfileEditDialog : Window
    {
        public string ProfileName => ProfileNameBox.Text?.Trim() ?? string.Empty;
        public string ProfileDescription => ProfileDescriptionBox.Text?.Trim() ?? string.Empty;

        public ProfileEditDialog(string existingName = "", string existingDescription = "")
        {
            InitializeComponent();
            
            // 如果是编辑模式，预填充数据
            if (!string.IsNullOrEmpty(existingName))
            {
                Title = "编辑方案";
                ProfileNameBox.Text = existingName;
                ProfileDescriptionBox.Text = existingDescription;
                SaveBtn.Content = "保存";
            }
            
            // 焦点设置到名称输入框
            ProfileNameBox.Focus();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (!ValidateInput()) return;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            // 验证方案名称
            if (string.IsNullOrWhiteSpace(ProfileNameBox.Text))
            {
                MessageBox.Show("请输入方案名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameBox.Focus();
                return false;
            }

            // 检查名称长度
            if (ProfileNameBox.Text.Trim().Length > 50)
            {
                MessageBox.Show("方案名称不能超过50个字符", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameBox.Focus();
                return false;
            }

            // 检查描述长度
            if (!string.IsNullOrEmpty(ProfileDescriptionBox.Text) && ProfileDescriptionBox.Text.Trim().Length > 100)
            {
                MessageBox.Show("方案描述不能超过100个字符", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileDescriptionBox.Focus();
                return false;
            }

            return true;
        }
    }
}
