// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Main.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The main form.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner
{
    /// <summary>
    /// The main form.
    /// </summary>
    public partial class Main : Form
    {
        /// <summary>
        /// The language manager.
        /// </summary>
        private readonly ILanguageManager languageManager = new LanguageManager();

        /// <summary>
        /// The scanner background worker.
        /// </summary>
        private readonly BackgroundWorker scanner = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            this.InitializeComponent();
            this.InitializeCaption();
            this.InitializeBackgroundWorker();
            this.InitializeLanguageManager();
            this.LoadLanguagesToCombo();
        }

        /// <summary>
        /// Initializes the caption.
        /// </summary>
        private void InitializeCaption()
        {
            this.Text = Application.ProductName + @" " + Application.ProductVersion;
        }

        /// <summary>
        /// Initializes the language manager.
        /// </summary>
        private void InitializeLanguageManager()
        {
            this.languageManager.SetCurrentLanguage("de-DE");
            this.languageManager.OnLanguageChanged += this.OnLanguageChanged;
        }

        /// <summary>
        /// Loads the languages to the combo boxes.
        /// </summary>
        private void LoadLanguagesToCombo()
        {
            foreach (var lang in this.languageManager.GetLanguages())
            {
                this.comboBoxLanguage.Items.Add(lang.Name);
            }

            this.comboBoxLanguage.SelectedIndex = 0;
        }

        /// <summary>
        /// Handles the selected language changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ComboBoxLanguageSelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = this.comboBoxLanguage.SelectedItem.ToString();

            if (string.IsNullOrWhiteSpace(selectedItem))
            {
                return;
            }

            this.languageManager.SetCurrentLanguageFromName(selectedItem);
        }

        /// <summary>
        /// Handles the language changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            this.label_Host.Text = this.languageManager.GetCurrentLanguage().GetWord("InsertHost");
            this.button_Start.Text = this.languageManager.GetCurrentLanguage().GetWord("StartPortscan");
        }

        /// <summary>
        /// Initializes the background worker.
        /// </summary>
        private void InitializeBackgroundWorker()
        {
            this.scanner.WorkerSupportsCancellation = true;
            this.scanner.WorkerReportsProgress = true;
            this.scanner.RunWorkerCompleted += this.ScannerCompleted;
            this.scanner.DoWork += this.ScannerDoWork;
            this.scanner.ProgressChanged += this.ScannerProgressChanged;
        }

        /// <summary>
        /// Handles the start button click.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ButtonStartClick(object sender, EventArgs e)
        {
            if (this.textBox_Host.Text.Equals(string.Empty))
            {
                var hostMustntBeEmpty = this.languageManager.GetCurrentLanguage().GetWord("HostMustntBeEmpty");
                var hostIsEmpty = this.languageManager.GetCurrentLanguage().GetWord("HostIsEmpty");
                MessageBox.Show(hostMustntBeEmpty, hostIsEmpty, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else
            {
                this.button_Start.Enabled = false;
                this.textBox_Host.ReadOnly = true;
                this.progressBar_Progress.Value = 0;
                this.scanner.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Runs the background scan process.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ScannerDoWork(object sender, DoWorkEventArgs e)
        {
            var filename = "ScanResult_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            var host = string.Empty;
            this.UiThreadInvoke(() => { host = this.textBox_Host.Text; });

            using (var sw = File.AppendText(filename))
            {
                var scanFrom = this.languageManager.GetCurrentLanguage().GetWord("ScanFrom");
                sw.WriteLine(scanFrom + DateTime.Now.ToString("dd.MM.yyyy:hh:mm:ss") + ":");
                sw.WriteLine(this.languageManager.GetCurrentLanguage().GetWord("PortList"));
                sw.WriteLine("-------------------------------------------------------------------------------------");
                sw.WriteLine(this.languageManager.GetCurrentLanguage().GetWord("ListOfOpenPorts") + host + ":");
            }

            for (var i = 0; i <= 65535; i++)
            {
                this.scanner.ReportProgress(i / 65535);
                try
                {
                    var tcpClient = new TcpClient(host, i);
                    using var sw = File.AppendText(filename);
                    sw.WriteLine(Convert.ToString(i));
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Handles the scanner completed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ScannerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.button_Start.Enabled = true;
            this.textBox_Host.ReadOnly = false;
        }

        /// <summary>
        /// Handles the scanner progress changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void ScannerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.progressBar_Progress.Value = e.ProgressPercentage > 100 ? 100 : e.ProgressPercentage;
        }
    }
}