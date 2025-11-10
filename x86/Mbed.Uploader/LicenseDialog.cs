using Microsoft.MbedUploader.Properties;
using System;
using System.Windows.Forms;

namespace Microsoft.MbedUploader
{
    public partial class LicenseDialog : Form
    {
        public LicenseDialog()
        {
            InitializeComponent();
            this.textBox.Text = Resources.MSR_LA___2576;
        }

        private void acceptButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Yes;
            this.Close();
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.No;
            this.Close();
        }

        private void bindingNavigator1_RefreshItems(object sender, EventArgs e)
        {

        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
