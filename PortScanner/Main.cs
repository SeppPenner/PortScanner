using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using Languages.Implementation;
using Languages.Interfaces;

namespace PortScanner
{
    public partial class Main : Form
    {
        private readonly ILanguageManager _lm = new LanguageManager();
        private readonly BackgroundWorker _scanner = new BackgroundWorker();

        public Main()
        {
            InitializeComponent();
            InitializeCaption();
            InitializeBackgroundWorker();
            InitializeLanguageManager();
            LoadLanguagesToCombo();
        }

        private void InitializeCaption()
        {
            Text = Application.ProductName + @" " + Application.ProductVersion;
        }


        private void InitializeLanguageManager()
        {
            _lm.SetCurrentLanguage("de-DE");
            _lm.OnLanguageChanged += OnLanguageChanged;
        }

        private void LoadLanguagesToCombo()
        {
            foreach (var lang in _lm.GetLanguages())
                comboBoxLanguage.Items.Add(lang.Name);
            comboBoxLanguage.SelectedIndex = 0;
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            _lm.SetCurrentLanguageFromName(comboBoxLanguage.SelectedItem.ToString());
        }

        private void OnLanguageChanged(object sender, EventArgs eventArgs)
        {
            label_Host.Text = _lm.GetCurrentLanguage().GetWord("InsertHost");
            button_Start.Text = _lm.GetCurrentLanguage().GetWord("StartPortscan");
        }

        private void InitializeBackgroundWorker()
        {
            _scanner.WorkerSupportsCancellation = true;
            _scanner.WorkerReportsProgress = true;
            _scanner.RunWorkerCompleted += Scanner_Completed;
            _scanner.DoWork += Scanner_DoWork;
            _scanner.ProgressChanged += Scanner_ProgressChanged;
        }


        private void button_Start_Click(object sender, EventArgs e)
        {
            if (textBox_Host.Text.Equals(""))
            {
                var hostMustntBeEmpty = _lm.GetCurrentLanguage().GetWord("HostMustntBeEmpty");
                var hostIsEmpty = _lm.GetCurrentLanguage().GetWord("HostIsEmpty");
                MessageBox.Show(hostMustntBeEmpty, hostIsEmpty, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else
            {
                button_Start.Enabled = false;
                textBox_Host.ReadOnly = true;
                progressBar_Progress.Value = 0;
                _scanner.RunWorkerAsync();
            }
        }

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
            var filename = "ScanResult_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            var host = "";
            this.UIThreadInvoke(() => { host = textBox_Host.Text; });

            using (var sw = File.AppendText(filename))
            {
                var scanFrom = _lm.GetCurrentLanguage().GetWord("ScanFrom");
                sw.WriteLine(scanFrom + DateTime.Now.ToString("dd.MM.yyyy:hh:mm:ss") + ":");
                sw.WriteLine(_lm.GetCurrentLanguage().GetWord("PortList"));
                sw.WriteLine("-------------------------------------------------------------------------------------");
                sw.WriteLine(_lm.GetCurrentLanguage().GetWord("ListOfOpenPorts") + host + ":");
            }

            for (var i = 0; i <= 65535; i++)
            {
                _scanner.ReportProgress(i / 65535);
                try
                {
                    new TcpClient(host, i);
                    using (var sw = File.AppendText(filename))
                    {
                        sw.WriteLine(Convert.ToString(i));
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void Scanner_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            button_Start.Enabled = true;
            textBox_Host.ReadOnly = false;
        }

        private void Scanner_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar_Progress.Value = e.ProgressPercentage > 100 ? 100 : e.ProgressPercentage;
        }
    }
}