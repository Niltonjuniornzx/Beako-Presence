using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Instalador do Beako Presence")]
[assembly: AssemblyProduct("Beako Presence")]
[assembly: AssemblyCompany("Beako")]
[assembly: AssemblyVersion("3.6.6.0")]
[assembly: AssemblyFileVersion("3.6.6.0")]
[assembly: AssemblyInformationalVersion("3.6.6")]

static class BeakoSetup {
    static readonly Color Ink = Color.FromArgb(31, 36, 43);
    static readonly Color Muted = Color.FromArgb(104, 112, 123);
    static readonly Color Accent = Color.FromArgb(111, 75, 219);
    static readonly string InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BeakoPresence");
    static Form window;
    static Panel content;
    static Label title, subtitle, status;
    static Button primary, back;
    static CheckBox desktop, startMenu, launch;
    static string stage;
    static int page;

    static Label TextLabel(string value, int x, int y, int width, int height, float size, bool bold, Color color) {
        return new Label { Text = value, Location = new Point(x, y), Size = new Size(width, height), Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, AutoSize = false };
    }
    static Button MakeButton(string value, int x, int y, int width, bool filled) {
        var b = new Button { Text = value, Location = new Point(x, y), Size = new Size(width, 40), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), BackColor = filled ? Accent : Color.White, ForeColor = filled ? Color.White : Ink, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderColor = filled ? Accent : Color.FromArgb(215, 219, 225);
        b.FlatAppearance.BorderSize = 1;
        return b;
    }
    static void SetPage(int next) {
        page = next; content.Controls.Clear(); back.Visible = page > 0; status.Text = "";
        if (page == 0) {
            title.Text = "Bem-vindo ao Beako Presence";
            subtitle.Text = "Vamos instalar o aplicativo no seu computador. O processo leva apenas alguns instantes.";
            content.Controls.Add(TextLabel("Uma instalação simples e segura", 0, 12, 450, 25, 11, true, Ink));
            content.Controls.Add(TextLabel("•  Não precisa extrair arquivos nem baixar o ZIP\n•  Instalação somente para sua conta, sem pedir administrador\n•  Suas configurações existentes serão preservadas", 0, 48, 490, 90, 10, false, Muted));
            content.Controls.Add(TextLabel("Pasta de instalação", 0, 158, 450, 20, 9, true, Ink));
            var path = new TextBox { Text = InstallPath, Location = new Point(0, 183), Size = new Size(490, 30), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(247, 248, 250) };
            content.Controls.Add(path);
            primary.Text = "Continuar";
        } else if (page == 1) {
            title.Text = "Escolha seus atalhos";
            subtitle.Text = "Selecione onde deseja acessar o Beako Presence depois da instalação.";
            desktop = new CheckBox { Text = "Criar atalho na Área de Trabalho", Checked = true, Location = new Point(0, 22), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            startMenu = new CheckBox { Text = "Adicionar ao menu Iniciar", Checked = true, Location = new Point(0, 64), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            launch = new CheckBox { Text = "Abrir o Beako Presence ao concluir", Checked = true, Location = new Point(0, 106), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            content.Controls.Add(desktop); content.Controls.Add(startMenu); content.Controls.Add(launch);
            primary.Text = "Instalar";
        } else {
            title.Text = "Instalando o Beako Presence";
            subtitle.Text = "Aguarde enquanto copiamos e configuramos os arquivos.";
            content.Controls.Add(status);
            var progress = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 24, Location = new Point(0, 64), Size = new Size(490, 8) };
            content.Controls.Add(progress);
            primary.Visible = false; back.Visible = false;
        }
        if (page < 2) primary.Visible = true;
    }
    static string Quote(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
    static void Install() {
        SetPage(2); window.Refresh();
        try {
            status.Text = "Preparando os arquivos...";
            string zip = Path.Combine(stage, "payload.zip"), package = Path.Combine(stage, "package");
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("BeakoPresence.SetupPayload.zip")) {
                if (resource == null) throw new InvalidDataException("Os arquivos de instalação não estão dentro deste executável.");
                using (FileStream output = File.Create(zip)) resource.CopyTo(output);
            }
            Directory.CreateDirectory(package);
            using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                string prefix = Path.GetFullPath(package).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (ZipArchiveEntry entry in archive.Entries) {
                    string destination = Path.GetFullPath(Path.Combine(package, entry.FullName));
                    if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O pacote contém um caminho inválido.");
                }
                ZipFile.ExtractToDirectory(zip, package);
            }
            string app = Path.Combine(package, "BeakoPresence.App.exe"), installer = Path.Combine(package, "INSTALAR.ps1");
            if (!File.Exists(app) || !File.Exists(installer)) throw new InvalidDataException("O pacote interno está incompleto.");
            byte[] header = new byte[2];
            using (FileStream input = File.OpenRead(app)) if (input.Read(header, 0, 2) != 2 || header[0] != 0x4D || header[1] != 0x5A) throw new InvalidDataException("O executável do aplicativo é inválido.");
            status.Text = "Instalando arquivos e criando os atalhos..."; window.Refresh();
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            string args = "-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File " + Quote(installer) + " -CreateDesktopShortcut " + desktop.Checked.ToString().ToLowerInvariant() + " -CreateStartMenuShortcut " + startMenu.Checked.ToString().ToLowerInvariant();
            using (Process process = Process.Start(new ProcessStartInfo(powershell, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = package })) {
                if (process == null) throw new IOException("O instalador não pôde ser iniciado.");
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException("A instalação não foi concluída. Feche o aplicativo e tente novamente.");
            }
            if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(InstallPath, "BeakoPresence.exe")) { UseShellExecute = true, WorkingDirectory = InstallPath });
            MessageBox.Show("Instalação concluída com sucesso.\n\nO Beako Presence está pronto para uso.", "Beako Presence", MessageBoxButtons.OK, MessageBoxIcon.Information);
            window.Close();
        } catch (Exception ex) {
            MessageBox.Show("Não foi possível concluir a instalação.\n\n" + ex.Message, "Instalação do Beako Presence", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetPage(1);
        } finally { try { if (!String.IsNullOrEmpty(stage) && Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } }
    }
    [STAThread] static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string installedApp = Path.Combine(InstallPath, "BeakoPresence.exe");
        if (File.Exists(installedApp)) {
            try {
                if (AssemblyName.GetAssemblyName(installedApp).Version >= Assembly.GetExecutingAssembly().GetName().Version) {
                    Process.Start(new ProcessStartInfo(installedApp) { UseShellExecute = true, WorkingDirectory = InstallPath }); return;
                }
            } catch { }
        }
        stage = Path.Combine(Path.GetTempPath(), "BeakoPresence-setup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        window = new Form { Text = "Instalação do Beako Presence", ClientSize = new Size(580, 405), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen, BackColor = Color.White, Font = new Font("Segoe UI", 9) };
        var header = new Panel { Location = new Point(0, 0), Size = new Size(580, 108), BackColor = Color.FromArgb(248, 247, 252) };
        header.Controls.Add(TextLabel("BEAKO PRESENCE", 32, 18, 500, 20, 9, true, Accent));
        title = TextLabel("", 32, 43, 520, 30, 17, true, Ink); header.Controls.Add(title);
        subtitle = TextLabel("", 32, 77, 520, 25, 9, false, Muted); header.Controls.Add(subtitle);
        content = new Panel { Location = new Point(42, 128), Size = new Size(500, 225), BackColor = Color.White };
        status = TextLabel("", 0, 28, 490, 24, 10, false, Muted);
        var footer = new Panel { Location = new Point(0, 360), Size = new Size(580, 45), BackColor = Color.FromArgb(250, 250, 251) };
        back = MakeButton("Voltar", 342, 3, 90, false); back.Click += (s, e) => SetPage(0);
        primary = MakeButton("Continuar", 442, 3, 105, true); primary.Click += (s, e) => { if (page == 0) SetPage(1); else Install(); };
        var cancel = MakeButton("Cancelar", 32, 3, 90, false); cancel.Click += (s, e) => window.Close();
        footer.Controls.Add(cancel); footer.Controls.Add(back); footer.Controls.Add(primary);
        window.Controls.Add(header); window.Controls.Add(content); window.Controls.Add(footer);
        window.FormClosed += (s, e) => { try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } };
        SetPage(0); Application.Run(window);
    }
}
