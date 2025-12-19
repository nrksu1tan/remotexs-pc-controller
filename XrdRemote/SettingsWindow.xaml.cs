using Microsoft.Win32;
using System.Reflection;
using System.Windows;

namespace XrdRemote
{
    public partial class SettingsWindow : Window
    {
        private const string AppName = "RemoteXS_Server";
        public SettingsWindow(int currentPort)
        {
            InitializeComponent();
            PortBox.Text = currentPort.ToString();

            // Проверка автозагрузки
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                AutostartCheck.IsChecked = rk.GetValue(AppName) != null;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Сохранение автозагрузки
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (AutostartCheck.IsChecked == true)
                    rk.SetValue(AppName, System.Environment.ProcessPath); // Для .NET 6+
                else
                    rk.DeleteValue(AppName, false);
            }

            if (int.TryParse(PortBox.Text, out int newPort))
            {
                ((MainWindow)Application.Current.MainWindow).UpdateServerPort(newPort);
                this.Close();
            }
            else { MessageBox.Show("Некорректный порт"); }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}