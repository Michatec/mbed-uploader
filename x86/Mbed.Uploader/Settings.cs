using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace Microsoft.MbedUploader
{
    public partial class Settings : Form
    {

        public string CustomCopyPath;
   
        public Settings(string currentpath)
        {
            InitializeComponent();
            CustomCopyPath = currentpath;
        }

        private void Settings_Load(object sender, EventArgs e)
        {
            textBox1.Text = CustomCopyPath;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            CustomCopyPath = textBox1.Text;
            Application.UserAppDataRegistry.SetValue("CustomDirectory", CustomCopyPath, RegistryValueKind.String);
            var mainForm = (MainForm)Application.OpenForms["MainForm"];
            mainForm.ReloadFileWatch(CustomCopyPath);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.ShowNewFolderButton = true;
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.SelectedPath = CustomCopyPath;
                fbd.Description = "Wählen Sie einen Ordner aus, um den Pfad auszuwählen:";
                fbd.ShowDialog();

                if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    CustomCopyPath = fbd.SelectedPath;
                    textBox1.Text = CustomCopyPath;
                }
            }
        }
    }
}
