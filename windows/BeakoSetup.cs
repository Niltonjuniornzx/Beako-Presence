using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Instalador do Beako Presence")]
[assembly: AssemblyProduct("Beako Presence")]
[assembly: AssemblyCompany("Beako")]
[assembly: AssemblyVersion("3.6.9.0")]
[assembly: AssemblyFileVersion("3.6.9.0")]
[assembly: AssemblyInformationalVersion("3.6.9")]

static class BeakoSetup {
    static readonly Color Ink = Color.FromArgb(31, 36, 43);
    static readonly Color Muted = Color.FromArgb(104, 112, 123);
    static readonly Color Accent = Color.FromArgb(111, 75, 219);
    static readonly string InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BeakoPresence");
    static Form window;
    static Panel content;
    static Label title, subtitle, status;
    static Button primary, back, cancel;
    static CheckBox desktop, startMenu, launch;
    static string stage, logPath;
    static int page;

    sealed class ChangedFile { public string Target, Backup; public bool HadOriginal; }
    static void Log(string message) {
        try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("s") + "] " + message + Environment.NewLine); } catch { }
    }
    static Label TextLabel(string value, int x, int y, int width, int height, float size, bool bold, Color color) {
        return new Label { Text = value, Location = new Point(x, y), Size = new Size(width, height), Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, AutoSize = false };
    }
    static Button MakeButton(string value, int x, int y, int width, bool filled) {
        var b = new Button { Text = value, Location = new Point(x, y), Size = new Size(width, 40), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), BackColor = filled ? Accent : Color.White, ForeColor = filled ? Color.White : Ink, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderColor = filled ? Accent : Color.FromArgb(215, 219, 225); b.FlatAppearance.BorderSize = 1; return b;
    }
    static string Quote(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
    static void SetPage(int next) {
        page = next; content.Controls.Clear(); back.Visible = page > 0; cancel.Visible = page < 2; status.Text = "";
        if (page == 0) {
            title.Text = "Bem-vindo ao Beako Presence";
            subtitle.Text = "Vamos instalar o aplicativo no seu computador. O processo leva apenas alguns instantes.";
            content.Controls.Add(TextLabel("Uma instalação simples e segura", 0, 12, 450, 25, 11, true, Ink));
            content.Controls.Add(TextLabel("•  Não precisa extrair arquivos nem baixar o ZIP\n•  Instalação somente para sua conta, sem pedir administrador\n•  Suas configurações existentes serão preservadas", 0, 48, 490, 90, 10, false, Muted));
            content.Controls.Add(TextLabel("Pasta de instalação", 0, 158, 450, 20, 9, true, Ink));
            content.Controls.Add(new TextBox { Text = InstallPath, Location = new Point(0, 183), Size = new Size(490, 30), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(247, 248, 250) });
            primary.Text = "Continuar";
        } else if (page == 1) {
            title.Text = "Escolha seus atalhos"; subtitle.Text = "Selecione onde deseja acessar o Beako Presence depois da instalação.";
            desktop = new CheckBox { Text = "Criar atalho na Área de Trabalho", Checked = true, Location = new Point(0, 22), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            startMenu = new CheckBox { Text = "Adicionar ao menu Iniciar", Checked = true, Location = new Point(0, 64), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            launch = new CheckBox { Text = "Abrir o Beako Presence ao concluir", Checked = true, Location = new Point(0, 106), Size = new Size(450, 30), Font = new Font("Segoe UI", 10), ForeColor = Ink };
            content.Controls.Add(desktop); content.Controls.Add(startMenu); content.Controls.Add(launch); primary.Text = "Instalar";
        } else {
            title.Text = "Instalando o Beako Presence"; subtitle.Text = "Aguarde enquanto copiamos e configuramos os arquivos.";
            content.Controls.Add(status); content.Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 24, Location = new Point(0, 64), Size = new Size(490, 8) });
            primary.Visible = false; back.Visible = false;
        }
    }
    static string CreateShortcut(string folder, string fileName, string appPath, string iconPath) {
        Directory.CreateDirectory(folder);
        string shortcutPath = Path.Combine(folder, fileName);
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) throw new InvalidOperationException("O Windows não disponibilizou a criação de atalhos.");
        object shell = null, shortcut = null;
        try {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { appPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallPath });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Gerenciador de atividades para Discord" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            return shortcutPath;
        } finally {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
    static void CloseRunningApp() {
        foreach (Process process in Process.GetProcessesByName("BeakoPresence")) {
            using (process) {
                string processPath = null;
                try { processPath = process.MainModule.FileName; } catch { }
                if (String.IsNullOrEmpty(processPath) || !Path.GetFullPath(processPath).Equals(Path.Combine(InstallPath, "BeakoPresence.exe"), StringComparison.OrdinalIgnoreCase)) continue;
                var answer = MessageBox.Show("O Beako Presence está aberto. Deseja fechá-lo normalmente para continuar a instalação? Suas configurações serão preservadas.", "Beako Presence em execução", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) throw new OperationCanceledException("Feche o Beako Presence e clique em Instalar novamente.");
                try { if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow(); } catch { }
                if (!process.WaitForExit(15000)) throw new OperationCanceledException("O aplicativo continua aberto. Feche-o manualmente e clique em Instalar novamente.");
            }
        }
    }
    static List<string> GetPackageFiles(string package) {
        string appName = File.Exists(Path.Combine(package, "BeakoPresence.App.exe")) ? "BeakoPresence.App.exe" : "BeakoPresence.exe";
        string[] required = { appName, "BeakoPresence.ico", "BeakoPresence-Avatar.png", "catalog.json", "community-paths.json", "icon-index.json", "INSTALAR.bat", "INSTALAR.ps1", "DESINSTALAR.bat", "DESINSTALAR.ps1", "ATUALIZAR.ps1" };
        foreach (string file in required) if (!File.Exists(Path.Combine(package, file))) throw new FileNotFoundException("Arquivo obrigatório ausente no pacote: " + file, file);
        byte[] signature = new byte[2]; using (FileStream f = File.OpenRead(Path.Combine(package, appName))) if (f.Read(signature, 0, 2) != 2 || signature[0] != 0x4D || signature[1] != 0x5A) throw new InvalidDataException("O executável do aplicativo não é válido.");
        var result = new List<string>(); foreach (string file in required) if (file != appName) result.Add(file);
        result.Add(appName + "|BeakoPresence.exe");
        string icons = Path.Combine(package, "icons"); if (Directory.Exists(icons)) foreach (string file in Directory.GetFiles(icons, "*", SearchOption.AllDirectories)) result.Add(file.Substring(package.Length + 1));
        return result;
    }
    static void RegisterUninstall(string appPath, string uninstaller) {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BeakoPresence")) {
            if (key == null) throw new IOException("Não foi possível registrar a desinstalação no Windows.");
            string version = FileVersionInfo.GetVersionInfo(appPath).FileVersion;
            key.SetValue("DisplayName", "Beako Presence", RegistryValueKind.String); key.SetValue("DisplayVersion", version, RegistryValueKind.String);
            key.SetValue("Publisher", "Beako", RegistryValueKind.String); key.SetValue("InstallLocation", InstallPath, RegistryValueKind.String);
            key.SetValue("DisplayIcon", appPath, RegistryValueKind.String); key.SetValue("UninstallString", "powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File " + Quote(uninstaller), RegistryValueKind.String);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
    static void PerformInstall(string package) {
        string appName = File.Exists(Path.Combine(package, "BeakoPresence.App.exe")) ? "BeakoPresence.App.exe" : "BeakoPresence.exe";
        List<string> packageFiles = GetPackageFiles(package); var changed = new List<ChangedFile>(); var shortcuts = new List<string>();
        string backupRoot = Path.Combine(stage, "backup"), appPath = Path.Combine(InstallPath, "BeakoPresence.exe");
        try {
            Directory.CreateDirectory(backupRoot); Directory.CreateDirectory(InstallPath);
            foreach (string item in packageFiles) {
                string sourceName = item, targetName = item;
                int separator = item.IndexOf('|'); if (separator >= 0) { sourceName = item.Substring(0, separator); targetName = item.Substring(separator + 1); }
                string source = Path.Combine(package, sourceName), target = Path.Combine(InstallPath, targetName), backup = Path.Combine(backupRoot, targetName);
                if (!File.Exists(source)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)); bool hadOriginal = File.Exists(target);
                if (hadOriginal) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(target, backup, true); }
                changed.Add(new ChangedFile { Target = target, Backup = backup, HadOriginal = hadOriginal });
                string candidate = target + ".beako-new"; File.Copy(source, candidate, true);
                if (File.Exists(target)) File.Replace(candidate, target, null); else File.Move(candidate, target);
                Log("Copiado " + targetName);
                Application.DoEvents();
            }
            if (startMenu.Checked) shortcuts.Add(CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), "Beako Presence.lnk", appPath, Path.Combine(InstallPath, "BeakoPresence.ico")));
            if (desktop.Checked) shortcuts.Add(CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Beako Presence.lnk", appPath, Path.Combine(InstallPath, "BeakoPresence.ico")));
            RegisterUninstall(appPath, Path.Combine(InstallPath, "DESINSTALAR.ps1"));
        } catch {
            for (int i = shortcuts.Count - 1; i >= 0; i--) try { File.Delete(shortcuts[i]); } catch { }
            for (int i = changed.Count - 1; i >= 0; i--) {
                ChangedFile f = changed[i]; try { if (f.HadOriginal && File.Exists(f.Backup)) File.Copy(f.Backup, f.Target, true); else if (!f.HadOriginal && File.Exists(f.Target)) File.Delete(f.Target); File.Delete(f.Target + ".beako-new"); } catch { }
            }
            throw;
        }
    }
    static void Install() {
        SetPage(2); primary.Enabled = false; window.Refresh();
        try {
            Log("Instalação iniciada. Destino: " + InstallPath);
            status.Text = "Preparando e validando os arquivos..."; window.Refresh();
            string zip = Path.Combine(stage, "payload.zip"), package = Path.Combine(stage, "package");
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("BeakoPresence.SetupPayload.zip")) {
                if (resource == null) throw new InvalidDataException("O pacote de instalação não está dentro deste executável.");
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
            CloseRunningApp();
            status.Text = "Instalando arquivos e criando os atalhos..."; window.Refresh();
            PerformInstall(package);
            if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(InstallPath, "BeakoPresence.exe")) { UseShellExecute = true, WorkingDirectory = InstallPath });
            MessageBox.Show("Instalação concluída com sucesso.\n\nO Beako Presence está pronto para uso.", "Beako Presence", MessageBoxButtons.OK, MessageBoxIcon.Information);
            window.Close();
        } catch (Exception ex) {
            Log("Falha: " + ex.ToString());
            MessageBox.Show("Não foi possível concluir a instalação.\n\n" + ex.Message + "\n\nLog: " + logPath, "Instalação do Beako Presence", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetPage(1); primary.Enabled = true;
        }
    }
    [STAThread] static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        logPath = Path.Combine(Path.GetTempPath(), "BeakoPresence-install-" + Guid.NewGuid().ToString("N") + ".log");
        string installedApp = Path.Combine(InstallPath, "BeakoPresence.exe");
        if (File.Exists(installedApp)) {
            try { if (AssemblyName.GetAssemblyName(installedApp).Version >= Assembly.GetExecutingAssembly().GetName().Version) { Process.Start(new ProcessStartInfo(installedApp) { UseShellExecute = true, WorkingDirectory = InstallPath }); return; } } catch { }
        }
        stage = Path.Combine(Path.GetTempPath(), "BeakoPresence-setup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        window = new Form { Text = "Instalação do Beako Presence", ClientSize = new Size(580, 405), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen, BackColor = Color.White, Font = new Font("Segoe UI", 9) };
        var header = new Panel { Location = new Point(0, 0), Size = new Size(580, 108), BackColor = Color.FromArgb(248, 247, 252) };
        header.Controls.Add(TextLabel("BEAKO PRESENCE", 32, 18, 500, 20, 9, true, Accent));
        title = TextLabel("", 32, 43, 520, 30, 17, true, Ink); header.Controls.Add(title);
        subtitle = TextLabel("", 32, 77, 520, 25, 9, false, Muted); header.Controls.Add(subtitle);
        content = new Panel { Location = new Point(42, 128), Size = new Size(500, 225), BackColor = Color.White }; status = TextLabel("", 0, 28, 490, 24, 10, false, Muted);
        var footer = new Panel { Location = new Point(0, 360), Size = new Size(580, 45), BackColor = Color.FromArgb(250, 250, 251) };
        back = MakeButton("Voltar", 342, 3, 90, false); back.Click += (s, e) => SetPage(0);
        primary = MakeButton("Continuar", 442, 3, 105, true); primary.Click += (s, e) => { if (page == 0) SetPage(1); else Install(); };
        cancel = MakeButton("Cancelar", 32, 3, 90, false); cancel.Click += (s, e) => window.Close();
        footer.Controls.Add(cancel); footer.Controls.Add(back); footer.Controls.Add(primary); window.Controls.Add(header); window.Controls.Add(content); window.Controls.Add(footer);
        window.FormClosed += (s, e) => { try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } };
        SetPage(0); Application.Run(window);
    }
}
