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

            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                AutostartCheck.IsChecked = rk.GetValue(AppName) != null;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (AutostartCheck.IsChecked == true)
                    rk.SetValue(AppName, System.Environment.ProcessPath);
                else
                    rk.DeleteValue(AppName, false);
            }

            if (int.TryParse(PortBox.Text, out int newPort))
            {
                ((MainWindow)Application.Current.MainWindow).UpdateServerPort(newPort);
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid port number.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}