using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Beako Presence Setup")]
[assembly: AssemblyProduct("Beako Presence")]
[assembly: AssemblyCompany("Beako")]
[assembly: AssemblyVersion("3.6.5.0")]
[assembly: AssemblyFileVersion("3.6.5.0")]
[assembly: AssemblyInformationalVersion("3.6.5")]

static class BeakoSetup {
    static string QuoteArgument(string value) {
        System.Text.StringBuilder result = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value ?? "") {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') result.Append('\\', slashes * 2 + 1).Append('"');
            else result.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }
    static void Fail(string message) {
        MessageBox.Show(message, "Instalação do Beako Presence", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    [STAThread] static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string installedApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BeakoPresence", "BeakoPresence.exe");
        if (File.Exists(installedApp)) {
            try {
                Version installedVersion = AssemblyName.GetAssemblyName(installedApp).Version;
                Version setupVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (installedVersion >= setupVersion) {
                    Process.Start(new ProcessStartInfo(installedApp) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(installedApp) });
                    return;
                }
            } catch { }
        }
        string stage = Path.Combine(Path.GetTempPath(), "BeakoPresence-setup-" + Guid.NewGuid().ToString("N"));
        try {
            string resourceName = "BeakoPresence.SetupPayload.zip";
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                if (resource == null) throw new InvalidDataException("Os arquivos de instalação não estão dentro deste executável.");
                Directory.CreateDirectory(stage);
                string zip = Path.Combine(stage, "payload.zip");
                using (FileStream output = File.Create(zip)) resource.CopyTo(output);
                string package = Path.Combine(stage, "package");
                Directory.CreateDirectory(package);
                using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                    string prefix = Path.GetFullPath(package).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        string destination = Path.GetFullPath(Path.Combine(package, entry.FullName));
                        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("O pacote contém um caminho de arquivo inválido.");
                    }
                }
                ZipFile.ExtractToDirectory(zip, package);
                string app = Path.Combine(package, "BeakoPresence.App.exe");
                string installer = Path.Combine(package, "INSTALAR.ps1");
                if (!File.Exists(app) || !File.Exists(installer)) throw new InvalidDataException("O pacote interno está incompleto.");
                byte[] header = new byte[2];
                using (FileStream input = File.OpenRead(app)) if (input.Read(header, 0, 2) != 2 || header[0] != 0x4D || header[1] != 0x5A) throw new InvalidDataException("O executável do app é inválido.");
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                string arguments = "-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File " + QuoteArgument(installer) + " -StartApp";
                using (Process process = Process.Start(new ProcessStartInfo(powershell, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })) {
                    if (process == null) throw new IOException("O instalador não pôde ser iniciado.");
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new IOException("A instalação não foi concluída. Tente novamente ou execute INSTALAR.bat do pacote completo.");
                }
            }
        } catch (Exception ex) {
            Fail("Não foi possível instalar o Beako Presence." + Environment.NewLine + Environment.NewLine + ex.Message);
        } finally {
            try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
        }
    }
}

