using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FlightConnectFSX
{
    class SystemTray
    {
        public static String VERSION_NUMBER = "2.8";

        public NotifyIcon SysTrayIcon;
        private ContextMenu myContextMenu;
        private MenuItem menuClose;
        private MenuItem menuConfig;
        private MenuItem menuAbout;
        private System.ComponentModel.IContainer components;

        // display icons
        private Icon connectedIcon;
        private Icon disconenctedIcon;

        public SystemTray()
        {
            connectedIcon = new Icon("connection_on.ico");
            disconenctedIcon = new Icon("connection_off.ico");
        }

        public void CreateNotifyicon()
        {
            this.components = new System.ComponentModel.Container();

            this.myContextMenu = new ContextMenu();

            this.menuClose = new MenuItem();
            this.menuConfig = new MenuItem();
            this.menuAbout = new MenuItem();

            // Initialize menuItems
            this.menuClose.Index = 0;
            this.menuClose.Text = "Close FlightConnect";
            this.menuClose.Click += new System.EventHandler(this.menuClose_Click);

            this.menuConfig.Index = 1;
            this.menuConfig.Text = "FlightConnect Settings";
            this.menuConfig.Click += new System.EventHandler(this.menuNetworkConfig_Click);

            this.menuAbout.Index = 2;
            this.menuAbout.Text = "About FlightConnect for FSX";
            this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);

            // Initialize menu items
            this.myContextMenu.MenuItems.AddRange(new MenuItem[] { this.menuAbout });
            this.myContextMenu.MenuItems.Add("-");
            this.myContextMenu.MenuItems.AddRange(new MenuItem[] { this.menuConfig });
            this.myContextMenu.MenuItems.Add("-");
            this.myContextMenu.MenuItems.AddRange(new MenuItem[] { this.menuClose });

            // Create the NotifyIcon.
            this.SysTrayIcon = new NotifyIcon(this.components);
            SysTrayIcon.ContextMenu = this.myContextMenu;
            SysTrayIcon.Visible = true;

            // set initial state
            setIcon(DataCollectorFSX.isFsxActive, true);

            SysTrayIcon.BalloonTipIcon = ToolTipIcon.Info;
            SysTrayIcon.BalloonTipTitle = "FlightConnect FSX Running";
            if (Properties.Settings.Default.IP_Override != string.Empty)
            {
                SysTrayIcon.BalloonTipText = "Server IP Address: " + Properties.Settings.Default.IP_Override;
            }
            else
            {
                SysTrayIcon.BalloonTipText = "Server IP Address: " + NetworkInterface.findServerIP(false);
            }

            SysTrayIcon.ShowBalloonTip(500);

            // handle events
            SysTrayIcon.MouseUp += new System.Windows.Forms.MouseEventHandler(this.sysTrayIcon_MouseUp);
        }

        private void sysTrayIcon_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                mi.Invoke(SysTrayIcon, null);
            }
        }

        public void setIcon( bool isConnected, bool doRefresh )
        {
            if (isConnected)
            {
                if (SysTrayIcon.Icon != connectedIcon || doRefresh)
                {
                    SysTrayIcon.Icon = connectedIcon;
                    if (Properties.Settings.Default.IP_Override != string.Empty)
                    {
                        SysTrayIcon.Text = "FlightConnect FSX ( Connected ) - IP:" + Properties.Settings.Default.IP_Override;
                    }
                    else
                    {
                        SysTrayIcon.Text = "FlightConnect FSX ( Connected ) - IP:" + NetworkInterface.findServerIP(false);
                    }
                }
            }
            else
            {
                if (SysTrayIcon.Icon != disconenctedIcon || doRefresh)
                {
                    SysTrayIcon.Icon = disconenctedIcon;
                    if (Properties.Settings.Default.IP_Override != string.Empty)
                    {
                        SysTrayIcon.Text = "FlightConnect FSX ( Disconnected ) - IP:" + Properties.Settings.Default.IP_Override;
                    }
                    else
                    {
                        SysTrayIcon.Text = "FlightConnect FSX ( Disconnected ) - IP:" + NetworkInterface.findServerIP(false);
                    }
                }                
            }
        }

        public void shutdownMenu()
        {
            // dispose of the icon
            SysTrayIcon.Visible = false;
            SysTrayIcon.Dispose();
        }

        private void menuClose_Click(object Sender, EventArgs e)
        {
            shutdownMenu();

            // shut down the main app loop
            FlightConnectFSX.runProgram = false;
            FlightConnectFSX.shutDown();
        }

        private void menuNetworkConfig_Click(object Sender, EventArgs e)
        {
            frmConfiguration myNetworkForm = new frmConfiguration();
            myNetworkForm.Show();
        }

        private void menuAbout_Click(object Sender, EventArgs e)
        {
            frmAbout myAboutForm = new frmAbout();
            myAboutForm.Show();
        }
    }
}
