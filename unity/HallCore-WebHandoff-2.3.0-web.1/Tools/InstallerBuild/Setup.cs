using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

class Setup : Form
{
    const string Version = "__VERSION__";
    const string PayloadHash = "__PAYLOAD_SHA256__";
    const string Marker = "HALL_TEACHING_SETUP_PAYLOAD_V1";
    readonly Button install;
    readonly TextBox path;
    readonly Label status;

    public Setup()
    {
        Text = "霍尔效应教学仿真 " + Version + " 安装程序";
        ClientSize = new Size(620, 310); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        Controls.Add(new Label { Text = "霍尔效应教学仿真", Font = new Font("Microsoft YaHei", 16, FontStyle.Bold), Left = 28, Top = 22, AutoSize = true });
        Controls.Add(new Label { Text = "逐步演示 · 学生亲手接线与操作仪器\nWindows 10/11 64 位；DirectX 11 显卡；可离线运行。\n建议 4 GB 内存、500 MB 空间。默认节能画质。", Left = 30, Top = 68, AutoSize = true });
        Controls.Add(new Label { Text = "安装目录：", Left = 30, Top = 156, AutoSize = true });
        path = new TextBox { Left = 105, Top = 152, Width = 370, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hall Effect Teaching") };
        Controls.Add(path);
        var browse = new Button { Text = "浏览…", Left = 485, Top = 150, Width = 100 };
        browse.Click += (s, e) => { using (var d = new FolderBrowserDialog { SelectedPath = path.Text }) if (d.ShowDialog() == DialogResult.OK) path.Text = d.SelectedPath; };
        Controls.Add(browse);
        install = new Button { Text = "安装", Left = 372, Top = 202, Width = 100, Height = 34 };
        install.Click += Install; Controls.Add(install);
        var cancel = new Button { Text = "取消", Left = 485, Top = 202, Width = 100, Height = 34 };
        cancel.Click += (s, e) => Close(); Controls.Add(cancel);
        status = new Label { Left = 30, Top = 248, Width = 555, Height = 52 }; Controls.Add(status);
    }

    static byte[] ReadPayload()
    {
        using (var fs = File.OpenRead(Application.ExecutablePath))
        using (var reader = new BinaryReader(fs, Encoding.ASCII))
        {
            byte[] marker = Encoding.ASCII.GetBytes(Marker);
            if (fs.Length < marker.Length + 8) throw new InvalidDataException("安装包不完整");
            fs.Seek(-marker.Length, SeekOrigin.End);
            if (Encoding.ASCII.GetString(reader.ReadBytes(marker.Length)) != Marker) throw new InvalidDataException("安装包标记无效");
            fs.Seek(-marker.Length - 8, SeekOrigin.End);
            long size = reader.ReadInt64();
            if (size <= 0 || size > int.MaxValue || size > fs.Length - marker.Length - 8) throw new InvalidDataException("安装包长度无效");
            fs.Seek(-marker.Length - 8 - size, SeekOrigin.End);
            byte[] bytes = reader.ReadBytes((int)size);
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") != PayloadHash) throw new InvalidDataException("安装包校验失败，请重新获取文件");
            return bytes;
        }
    }

    static void Extract(string directory)
    {
        if (!Environment.Is64BitOperatingSystem) throw new NotSupportedException("本版本需要 64 位 Windows");
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("安装目录不能为空");
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (var data = new MemoryStream(ReadPayload(), false))
        using (var zip = new ZipArchive(data, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
                if (!Path.GetFullPath(Path.Combine(root, entry.FullName)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("安装包包含无效路径");
            Directory.CreateDirectory(root);
            foreach (var entry in zip.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var input = entry.Open())
                using (var output = File.Create(target)) input.CopyTo(output);
            }
        }
    }

    void Install(object sender, EventArgs e)
    {
        try
        {
            install.Enabled = false; status.Text = "正在校验并解包…"; status.Refresh();
            string directory = path.Text.Trim(); Extract(directory);
            string exe = Path.Combine(Path.GetFullPath(directory), "Hall-Teaching.exe");
            string shortcutError = "";
            try
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "霍尔效应教学仿真.lnk"), exe);
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "霍尔效应教学仿真.lnk"), exe);
            }
            catch (Exception error) { shortcutError = "\n快捷方式未创建：" + error.Message + "\n可从安装目录直接启动。"; }
            if (MessageBox.Show("安装完成。" + shortcutError + "\n是否立即运行？", "完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
            Close();
        }
        catch (Exception error) { install.Enabled = true; status.Text = "安装失败：" + error.Message; }
    }

    static void CreateShortcut(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link));
        Type type = Type.GetTypeFromProgID("WScript.Shell"); dynamic shell = Activator.CreateInstance(type);
        dynamic shortcut = shell.CreateShortcut(link); shortcut.TargetPath = target;
        shortcut.WorkingDirectory = Path.GetDirectoryName(target); shortcut.Description = "霍尔效应教学仿真"; shortcut.Save();
    }

    [STAThread] static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            try
            {
                if (args.Length == 1 && args[0] == "/verify") ReadPayload();
                else if (args.Length == 2 && args[0] == "/extract") Extract(args[1]);
                else return 2;
                return 0;
            }
            catch { return 1; }
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Setup()); return 0;
    }
}
