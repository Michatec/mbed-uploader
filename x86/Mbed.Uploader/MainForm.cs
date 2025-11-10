using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Security;
using System.Threading;
using System.Windows.Forms;

namespace Microsoft.MbedUploader
{
    internal partial class MainForm : Form
    {
        FileSystemWatcher watcher;
        private string customcopypath = "";
        private string downloads;

        public MainForm()
        {
            InitializeComponent();
            var v = typeof(MainForm).Assembly.GetName().Version;
            this.versionLabel.Text = "v" + v.Major + "." + v.Minor;
        }

        public void ReloadFileWatch(string path)
        {
            customcopypath = path;
            initializeFileWatch();
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            this.initializeFileWatch();
        }

        private void initializeFileWatch()
        {
            customcopypath = (string)Application.UserAppDataRegistry.GetValue("CustomDirectory", "");

            if (!String.IsNullOrEmpty(customcopypath) && Directory.Exists(customcopypath))
            {
                downloads = customcopypath;
            }
            else
            {
                downloads = KnownFoldersNativeMethods.GetDownloadPath();
            }

            if (String.IsNullOrEmpty(downloads) || !Directory.Exists(downloads))
            {
                this.updateStatus("oops, der `Downloads` Ordner kann nicht gefunden werden. Bitte gibt einen Pfad in den Einstellungen an.");
                return;
            }

            this.watcher = new FileSystemWatcher(downloads);
            this.watcher.Renamed += (sender, e) => this.handleFileEvent(e);
            this.watcher.Created += (sender, e) => this.handleFileEvent(e);
            this.watcher.EnableRaisingEvents = true;

            this.waitingForHexFileStatus();
        }

        private void waitingForHexFileStatus()
        {
            this.updateStatus($"Warte auf .hex-Datei ...");
            this.trayIcon.ShowBalloonTip(3000, "Bereit...", $"Warte auf .hex-Datei ...", ToolTipIcon.None);
            this.label1.Text = downloads;
        }

        delegate void Callback();

        private void updateStatus(string value)
        {
            Callback a = (Callback)(() =>
            {
                this.statusLabel.Text = value;
                this.trayIcon.Text = value;
            });
            this.Invoke(a);
        }

        void handleFileEvent(FileSystemEventArgs e)
        {
            this.handleFile(e.FullPath);
        }

        volatile int copying;
        void handleFile(string fullPath)
        {
            try
            {
                // In case this is data-url download, at least Chrome will not rename file, but instead write to it
                // directly. This mean we may catch it in the act. Let's leave it some time to finish writing.
                Thread.Sleep(500);

                var info = new System.IO.FileInfo(fullPath);
                Trace.WriteLine("download: " + info.FullName);

                if (info.Extension != ".hex") return;

                var infoName = info.Name;
                Trace.WriteLine("download name: " + info.Name);
                if (info.Name.EndsWith(".uploaded.hex", StringComparison.OrdinalIgnoreCase)) return;
                if (info.Length > 10000000) return; // make sure we don't try to copy large files


                // already copying?
                if (Interlocked.Exchange(ref this.copying, 1) == 1)
                    return;

                try
                {

                    var driveletters = getCalliopeMiniDrives();
                    List<String> drives = new List<String>();
                    foreach (var d in driveletters)
                    {
                        drives.Add(d.RootDirectory.FullName);
                    }
                    if (drives.Count == 0)
                    {
                        this.updateStatus("Kein Roboter gefunden");
                        this.trayIcon.ShowBalloonTip(3000, "Kopieren abgebrochen...", "Kein mini gefunden", ToolTipIcon.None);
                        return;
                    }

                    this.updateStatus("kopiere .hex Datei");
                    this.trayIcon.ShowBalloonTip(3000, "Kopiere...", "Kopiere .hex Datei", ToolTipIcon.None);

                    // copy to all boards
                    copyFirmware(info.FullName, drives);

                    // move away hex file
                    var temp = System.IO.Path.ChangeExtension(info.FullName, ".uploaded.hex");
                    try
                    {
                        File.Copy(info.FullName, temp, true);
                        File.Delete(info.FullName);
                    }
                    catch (IOException) { }
                    catch (NotSupportedException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { }

                    // update ui
                    this.updateStatus("uploading done");
                    this.waitingForHexFileStatus();
                }
                finally
                {
                    Interlocked.Exchange(ref this.copying, 0);
                }
            }
            catch (IOException) { }
            catch (NotSupportedException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
        }

        static void copyFirmware(string file, List<string> drives)
        {
            var waitHandles = new List<WaitHandle>();
            foreach (var drive in drives)
            {
                var ev = new AutoResetEvent(false);
                waitHandles.Add(ev);
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        var trg = System.IO.Path.Combine(drive, "firmware.hex");

                        var fs1 = new FileStream(file, FileMode.Open, FileAccess.Read);

                        var fs2 = new FileStream(trg, FileMode.Create);

                        fs1.CopyTo(fs2);

                        fs2.Close(); fs1.Close();

                    }
                    catch (IOException) { }
                    catch (NotSupportedException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { }
                    ev.Set();
                }, ev);
            }

            //waits for all the threads (waitHandles) to call the .Set() method
            //and inform that the execution has finished.
            WaitHandle.WaitAll(waitHandles.ToArray());
        }

        static DriveInfo[] getCalliopeMiniDrives()
        {
            var drives = System.IO.DriveInfo.GetDrives();
            var r = new System.Collections.Generic.List<DriveInfo>();
            foreach (var di in drives)
            {
                var label = getVolumeLabel(di);
                if (label.StartsWith("MINI", StringComparison.Ordinal) | label.StartsWith("MICROBIT", StringComparison.Ordinal))
                    r.Add(di);
            }
            return r.ToArray();
        }

        static string getVolumeLabel(DriveInfo di)
        {
            try { return di.VolumeLabel; }
            catch (IOException) { }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            return "";
        }

        private void trayIcon_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.WindowState = FormWindowState.Normal;
            this.Show();
            this.Activate();
        }

        private void versionLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/michatec/mbed-uploader/releases");
            }
            catch (IOException) { }
        }

        private void SettingsLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var settings = new Settings(customcopypath);
   
            settings.ShowDialog();
            customcopypath = settings.CustomCopyPath;
            Application.UserAppDataRegistry.SetValue("CustomDirectory", customcopypath, RegistryValueKind.String);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try 
            {
                Process.Start("https://github.com/michatec/mbed-uploader/");
            }
            catch (Exception) { }
        }
    }
}
