using System;
using System.Text;
using System.Windows.Forms;
using System.Net;

namespace FlightConnectFSX
{
    public partial class frmConfiguration : Form
    {
        public frmConfiguration()
        {
            InitializeComponent();
            loadAddresses();

            if (Properties.Settings.Default.IP_Override != String.Empty)
            {
                cmbIpAddress.Text = Properties.Settings.Default.IP_Override;
            }

            chkLogging.Checked = Properties.Settings.Default.Enable_Logging;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // guess at the serve ip for discovery
        private void loadAddresses()
        {
            IPHostEntry host;
            string localIP = String.Empty;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily.ToString() == "InterNetwork")
                {
                    cmbIpAddress.Items.Add(ip.ToString());
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (cmbIpAddress.Text.Trim() != string.Empty)
            {
                if (!isValidIp(cmbIpAddress.Text.Trim()))
                {
                    MessageBox.Show(cmbIpAddress.Text.Trim() + " is not a valid IP Address. It must be in the format 0.0.0.0", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            Properties.Settings.Default.IP_Override = cmbIpAddress.Text.Trim();
            Properties.Settings.Default.Enable_Logging = chkLogging.Checked;
            Properties.Settings.Default.Save();

            FlightConnectFSX.myIcon.setIcon(DataCollectorFSX.isFsxActive, true);

            this.Close();
        }

        private bool isValidIp(string ip_address)
        {
            String[] tmpArray = ip_address.Split('.');
            if (tmpArray.Length == 4) // full address
            {
                IPAddress address;
                if (IPAddress.TryParse(ip_address, out address))
                {
                    switch (address.AddressFamily)
                    {
                        case System.Net.Sockets.AddressFamily.InterNetwork:
                            return true; 
                    }
                }
            }

            return false;
        }
    }
}
