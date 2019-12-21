using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoLogin
{
    public partial class LoginForm : Form
    {
        byte[] plainTextPassword;
        private delegate void SafeCallDelegate(string text);
        Process process = null;

        #region Load & Save
        private byte[] Protect(byte[] plainText, out byte[] entropy)
        {
            entropy = new byte[20];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(entropy);
            }

            byte[] cipherText = ProtectedData.Protect(plainText, entropy,
                DataProtectionScope.CurrentUser);
            return cipherText;
        }
        private byte[] Unprotect(byte[] cipherText, byte[] entropy)
        {
            byte[] plaintext = ProtectedData.Unprotect(cipherText, entropy,
                DataProtectionScope.CurrentUser);
            return plaintext;
        }
        public LoginForm()
        {
            InitializeComponent();
            CiscoLookupButton.Text = Properties.Settings.Default.CiscoPath;
            toolTip.SetToolTip(CiscoLookupButton, CiscoLookupButton.Text);
            ServerTextBox.Text = Properties.Settings.Default.Server;
            ServerTextBox.Text = Properties.Settings.Default.Server;
            RememberUsernameTextBox.Checked = Properties.Settings.Default.RememberUsername;
            RememberPasswordTextBox.Checked = Properties.Settings.Default.RememberPassword;
            if(RememberUsernameTextBox.Checked)
            {
                UsernameTextBox.Text = Properties.Settings.Default.Username;
            }
            if(RememberPasswordTextBox.Checked)
            {
                byte[] cipherText = Convert.FromBase64String(Properties.Settings.Default.Password);
                byte[] entropy = Convert.FromBase64String(Properties.Settings.Default.Entropy);
                PasswordTextBox.Text = "I am a placeholder";
                plainTextPassword = Unprotect(cipherText, entropy);
            }
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!(process is null) && !process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    MessageBox.Show("Failed to kill program.");
                }
            }
            Properties.Settings.Default.CiscoPath = CiscoLookupButton.Text;
            Properties.Settings.Default.Server = ServerTextBox.Text;
            Properties.Settings.Default.RememberUsername = RememberUsernameTextBox.Checked;
            Properties.Settings.Default.RememberPassword = RememberPasswordTextBox.Checked;
            if (RememberUsernameTextBox.Checked)
            {
                Properties.Settings.Default.Username = UsernameTextBox.Text;
            }
            else
            {
                Properties.Settings.Default.Username = "";
            }
            if(RememberPasswordTextBox.Checked)
            {
                byte[] entropy;
                Properties.Settings.Default.Password =
                    Convert.ToBase64String(Protect(plainTextPassword, out entropy));
                Properties.Settings.Default.Entropy = Convert.ToBase64String(entropy);
            }
            else
            {
                Properties.Settings.Default.Password = "";
                Properties.Settings.Default.Entropy = "";

            }
            Properties.Settings.Default.Save();
            notifyIcon.Visible = false;
        }
        private void PasswordTextBox_TextChanged(object sender, EventArgs e)
        {
            plainTextPassword = Encoding.UTF8.GetBytes(PasswordTextBox.Text);
        }

        #endregion

        #region Connection
        private void OnOutputReceived(object sender, DataReceivedEventArgs eventArgs)
        {
            Process process = sender as Process;
            if (eventArgs.Data is null)
            {
                Log("Program finished.");
            }
            else if (eventArgs.Data.Trim().Length > 0)
            {
                string data = eventArgs.Data.Trim();
                Log(data);
                if (data == "[ VPN Connection commands ]")
                {
                    if(!process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            Log("Failed to kill program.");
                        }
                    }
                }
            }
        }
        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (plainTextPassword is null)
                return;
            string program = CiscoLookupButton.Text;
            if(process!=null && !process.HasExited)
            {
                process.Kill();
            }
            process = new Process();
            ProcessStartInfo p = new ProcessStartInfo
            {
                FileName = program,
                Arguments = "-s",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo = p;
            StringBuilder output = new StringBuilder();
            process.OutputDataReceived += OnOutputReceived;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.StandardInput.WriteLine("connect " + ServerTextBox.Text);
                process.StandardInput.WriteLine(UsernameTextBox.Text);
                process.StandardInput.WriteLine(Encoding.UTF8.GetString(plainTextPassword));
                process.StandardInput.WriteLine("y"); // to ignore additional confirmation
                process.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                process = null;
            }
        }
        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            string program = CiscoLookupButton.Text;
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
            process = new Process();
            ProcessStartInfo p = new ProcessStartInfo
            {
                FileName = program,
                Arguments = "disconnect",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo = p;
            StringBuilder output = new StringBuilder();
            process.OutputDataReceived += OnOutputReceived;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                process = null;
            }
        }
        #endregion

        private void CiscoLookupButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
                return;
            CiscoLookupButton.Text = openFileDialog.FileName;
            toolTip.SetToolTip(CiscoLookupButton, CiscoLookupButton.Text);
        }


        public void Log(string text)
        {

            if (LogTextBox.InvokeRequired)
            {
                SafeCallDelegate del = new SafeCallDelegate(Log);
                LogTextBox.Invoke(del, new object[] { text });
            }
            else
            {
                string currentTime = string.Format("[{0:HH}:{0:mm}:{0:ss}]", DateTime.Now);
                LogTextBox.Text += currentTime + text + Environment.NewLine;
                toolStripStatusLabel.Text = text.StartsWith(">> ") ? text.Substring(3) : text;
                LogTextBox.SelectionStart = LogTextBox.TextLength;
                LogTextBox.ScrollToCaret();
                if(text== ">> state: Disconnected")
                {
                    ServerTextBox.BackColor = Color.LightCoral;
                    Icon = notifyIcon.Icon = Resource.RedIcon;
                }
                else if(text==">> state: Connected")
                {
                    ServerTextBox.BackColor = Color.LightGreen;
                    Icon = notifyIcon.Icon = Resource.GreenIcon;
                }
                else if (text.StartsWith(">> error:"))
                {
                    MessageBox.Show(text.Substring(3));
                }
            }
        }

        private void ShowLogButton_Click(object sender, EventArgs e)
        {
            if (Height <= 235)
                Height = 430;
            else
                Height = 235;
        }

        private void LoginForm_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NotifyIcon_DoubleClick(sender, e);
            Close();
        }

        private void ConnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NotifyIcon_DoubleClick(sender, e);
            ConnectButton_Click(sender, e);
        }

        private void DisconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NotifyIcon_DoubleClick(sender, e);
            DisconnectButton_Click(sender, e);
        }
    }
}
