// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Main.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The main form.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner;

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
    /// The port scan service.
    /// </summary>
    private readonly IPortScanService portScanService = new PortScanService();

    /// <summary>
    /// The scan result writer.
    /// </summary>
    private readonly IScanResultWriter scanResultWriter = new ScanResultWriter();

    /// <summary>
    /// The settings of a scan run.
    /// </summary>
    private readonly ScanSettings scanSettings = new();

    /// <summary>
    /// The scanner background worker.
    /// </summary>
    private readonly BackgroundWorker scanner = new();

    /// <summary>
    /// The cancellation token source of the running scan. It is created when a scan is started and is the
    /// only way to stop it. The cancellation of the <see cref="BackgroundWorker"/> itself is not used,
    /// because the scan service must not know anything about Windows Forms and therefore takes a
    /// <see cref="CancellationToken"/>.
    /// </summary>
    private CancellationTokenSource? cancellation;

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
        this.languageManager.OnLanguageChanged += this.OnLanguageChanged!;
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
        var selectedItem = this.comboBoxLanguage.SelectedItem?.ToString();

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
        var language = this.languageManager.GetCurrentLanguage();
        this.label_Host.Text = language.GetWord("InsertHost");
        this.button_Start.Text = this.scanner.IsBusy ? language.GetWord("CancelPortscan") : language.GetWord("StartPortscan");
    }

    /// <summary>
    /// Initializes the background worker.
    /// </summary>
    private void InitializeBackgroundWorker()
    {
        this.scanner.WorkerReportsProgress = true;
        this.scanner.RunWorkerCompleted += this.ScannerCompleted!;
        this.scanner.DoWork += this.ScannerDoWork!;
        this.scanner.ProgressChanged += this.ScannerProgressChanged!;
    }

    /// <summary>
    /// Handles the start button click. The same button cancels a running scan.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event args.</param>
    private void ButtonStartClick(object sender, EventArgs e)
    {
        if (this.scanner.IsBusy)
        {
            this.button_Start.Enabled = false;
            this.cancellation?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(this.textBox_Host.Text))
        {
            var hostMustntBeEmpty = this.languageManager.GetCurrentLanguage().GetWord("HostMustntBeEmpty");
            var hostIsEmpty = this.languageManager.GetCurrentLanguage().GetWord("HostIsEmpty");
            MessageBox.Show(hostMustntBeEmpty, hostIsEmpty, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            return;
        }

        this.cancellation = new CancellationTokenSource();
        this.button_Start.Text = this.languageManager.GetCurrentLanguage().GetWord("CancelPortscan");
        this.textBox_Host.ReadOnly = true;
        this.progressBar_Progress.Value = 0;
        this.scanner.RunWorkerAsync(this.cancellation.Token);
    }

    /// <summary>
    /// Runs the background scan process.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event args.</param>
    private void ScannerDoWork(object sender, DoWorkEventArgs e)
    {
        var cancellationToken = (CancellationToken)e.Argument!;
        var host = string.Empty;
        var texts = new ScanResultTexts(string.Empty, string.Empty, string.Empty);

        this.UiThreadInvoke(() =>
        {
            host = this.textBox_Host.Text.Trim();
            var language = this.languageManager.GetCurrentLanguage();
            texts = new ScanResultTexts(
                language.GetWord("ScanFrom") ?? string.Empty,
                language.GetWord("PortList") ?? string.Empty,
                language.GetWord("ListOfOpenPorts") ?? string.Empty);
        });

        var timestamp = DateTime.Now;
        var address = this.portScanService.ResolveHost(host);
        var openPorts = this.portScanService.ScanPorts(address, this.scanSettings, this.scanner.ReportProgress, cancellationToken);
        this.scanResultWriter.WriteResult(this.scanResultWriter.GetFileName(timestamp), host, timestamp, texts, openPorts);
        e.Cancel = cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Handles the scanner completed event.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event args.</param>
    private void ScannerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        this.cancellation?.Dispose();
        this.cancellation = null;

        this.button_Start.Enabled = true;
        this.button_Start.Text = this.languageManager.GetCurrentLanguage().GetWord("StartPortscan");
        this.textBox_Host.ReadOnly = false;

        if (e.Error is not null)
        {
            this.progressBar_Progress.Value = 0;
            var title = this.languageManager.GetCurrentLanguage().GetWord("ErrorTitle");
            var text = $"{e.Error.Message}{Environment.NewLine}{Environment.NewLine}{e.Error.StackTrace}";
            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        this.progressBar_Progress.Value = e.Cancelled ? 0 : 100;
    }

    /// <summary>
    /// Handles the scanner progress changed event.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event args.</param>
    private void ScannerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        this.progressBar_Progress.Value = Math.Clamp(e.ProgressPercentage, this.progressBar_Progress.Minimum, this.progressBar_Progress.Maximum);
    }
}
