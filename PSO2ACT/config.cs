using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace PSO2ACT
{
    public partial class config : UserControl
    {
        public string selectedFolder = string.Empty;
        public volatile bool refreshFlag;

        public config()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                selectedFolder = dialog.SelectedPath;
                if (!Directory.Exists(String.Format(@"{0}\damagelogs", selectedFolder)))
                    MessageBox.Show("Unable to find damagelog folder. Make sure the damage dump plugin is enabled.");

                directory.Text = selectedFolder;
            }

        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            refreshFlag = true;
        }
    }
}
