using DiscordRPC;
#if DEBUG
using DiscordRPC.Logging;
#endif
using Newtonsoft.Json;
using PresenceCommon.Types;
using VitaPresence_GUI.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace VitaPresence_GUI
{
    public partial class MainForm : Form
    {
        private Thread listenThread;
        private static Socket client;
        private static DiscordRpcClient rpc;
        private IPAddress ipAddress;
        private int updateInterval = 10;

        private bool ManualUpdate = false;
        private string LastTitleID = "";
        private Timestamps time = null;
        private static Timer timer;
        private bool HasSeenMacPrompt = false;

        public MainForm()
        {
            InitializeComponent();
            listenThread = new Thread(TryConnect);
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (connectButton.Text == "Connect")
            {
                // Check and see if ClientID is empty
                if (string.IsNullOrWhiteSpace(clientBox.Text))
                {
                    Show();
                    Activate();
                    UpdateStatus("Client ID cannot be empty", Color.DarkRed);
                    SystemSounds.Exclamation.Play();
                    return;
                }

                // Check and see if we have an IP
                // If we have an IP, prompt to swap to MAC Address
                if (IPAddress.TryParse(addressBox.Text, out ipAddress))
                {
                    if (!HasSeenMacPrompt)
                    {
                        HasSeenMacPrompt = true;

                        string message = "We've detected that you're using an IP to connect to your Vita. Connecting via MAC address may make it easier to reconnect to your device in case the IP changes." +
                                         "\n\nWould you like to swap to connecting via MAC address? \n(We'll only ask this once.)";

                        if (MessageBox.Show(message, "IP Detected", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            UseMacDefault.Checked = true;
                            IpToMac();
                        }
                        else
                            UseMacDefault.Checked = false;
                    }
                    else if (UseMacDefault.Checked == true)
                        IpToMac();
                }
                else
                {
                    // If in this block, means we dont have a valid IP.
                    // Check and see if it's a MAC Address
                    try
                    {
                        IPAddress.TryParse(Utils.GetIpByMac(addressBox.Text), out ipAddress);
                        if (ipAddress == null)
                        {
                            Show();
                            Activate();
                            UpdateStatus("Couldn't translate MAC to IP\nIs your Vita connected to Wifi?", Color.DarkRed);
                            SystemSounds.Exclamation.Play();
                            return;
                        }

                    }
                    catch (FormatException)
                    {
                        Show();
                        Activate();
                        UpdateStatus("Invalid IP or MAC Address", Color.DarkRed);
                        SystemSounds.Exclamation.Play();
                        return;
                    }
                }

                // Parse update interval
                if (!int.TryParse(updateIntervalBox.Text, out updateInterval))
                {
                    Show();
                    Activate();
                    UpdateStatus("Invalid update interval!\nPlease enter time in seconds.", Color.DarkRed);
                    SystemSounds.Exclamation.Play();
                    return;
                }

                listenThread.Start();

                connectButton.Text = "Disconnect";
                connectToolStripMenuItem.Text = "Disconnect";

                addressBox.Enabled = false;
                clientBox.Enabled = false;
                updateIntervalBox.Enabled = false;
            }
            else
            {
                listenThread.Abort();
                if (rpc != null && !rpc.IsDisposed)
                {
                    rpc.ClearPresence();
                    rpc.Dispose();
                }

                if (client != null) client.Close();
                if (timer != null) timer.Dispose();
                listenThread = new Thread(TryConnect);
                UpdateStatus("", Color.Gray);
                connectButton.Text = "Connect";
                connectToolStripMenuItem.Text = "Connect";
                trayIcon.Icon = Resources.Disconnected;
                trayIcon.Text = "VitaPresence (Disconnected)";

                ipAddress = null;
                addressBox.Enabled = true;
                clientBox.Enabled = true;
                updateIntervalBox.Enabled = true;
                LastTitleID = "";
                time = null;
            }
        }

        private void OnConnectTimeout(object source, ElapsedEventArgs e)
        {
            LastTitleID = "";
            time = null;
        }

        private void SetUserInfoConnecting()
        {
            UpdateStatus("Attemping to connect to server...", Color.Gray);
            trayIcon.Icon = Resources.Disconnected;
            trayIcon.Text = "VitaPresence (Connecting...)";
        }

        private void TryConnect()
        {
            if (rpc != null && !rpc.IsDisposed)
            {
                rpc.ClearPresence();
                rpc.Dispose();
            }

            rpc = new DiscordRpcClient(clientBox.Text);
            rpc.Initialize();

            //Create a timer that will be enabled when we lose connection to the server. 
            //Once the full time has passed, it will clear the info it had of the previous game
            timer = new Timer()
            {
                Interval = 60000,
                SynchronizingObject = this,
                Enabled = false,
            };
            timer.Elapsed += new ElapsedEventHandler(OnConnectTimeout);

#if DEBUG
            rpc.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            //Subscribe to events
            rpc.OnReady += (s, obj) =>
            {
                Console.WriteLine("Received Ready from user {0}", obj.User.Username);
            };

            rpc.OnPresenceUpdate += (s, obj) =>
            {
                Console.WriteLine("Received Update! {0}", obj.Presence);
            };
#endif

            SetUserInfoConnecting();

            while (true)
            {
                client = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = 5500,
                    SendTimeout = 5500,
                };
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 0xCAFE);

                timer.Enabled = true;

                try
                {
                    IAsyncResult result = client.BeginConnect(localEndPoint, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                    if (!success)
                    {
                        UpdateStatus("Could not connect to PS Vita! Retrying...", Color.DarkRed);
                        client.Close();
                    }
                    else
                    {
                        timer.Enabled = false;

                        DataListenOne();
                        client.EndConnect(result);
                        client.Close();

                        Thread.Sleep(updateInterval * 1000); // wait before another connect
                    }
                }
                catch (ArgumentNullException)
                {
                    //The ip address is null because arp couldn't find the target mac address.
                    //So we sleep and search for it again.
                    Thread.Sleep(1000);
                    IPAddress.TryParse(Utils.GetIpByMac(addressBox.Text), out ipAddress);
                }
                catch (SocketException e)
                {
                    UpdateStatus(e.ToString(), Color.Red);
                    Thread.Sleep(2000);

                    client.Close();
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();

                    SetUserInfoConnecting();
                }
            }
        }

        private void DataListenOne()
        {
            ManualUpdate = true;
            byte[] bytes = new byte[600];
            int cnt = client.Receive(bytes);

            Title title = new Title(bytes);
            if (title.Magic == 0xCAFECAFE)
            {
                trayIcon.Icon = Resources.Connected;
                trayIcon.Text = "VitaPresence (Connected)";

                if (title.Index == 0)
                {
                    UpdateStatus("In LiveArea (Connected)", Color.Green);
                }
                else
                {
                    UpdateStatus("Playing [" + title.TitleID + "] (Connected)", Color.Green);
                }

                if (LastTitleID != title.TitleID)
                {
                    time = Timestamps.Now;
                }
                if ((LastTitleID != title.TitleID) || ManualUpdate)
                {
                    if (rpc != null)
                    {
                        if (checkMainMenu.Checked == false && title.Index == 0)
                            rpc.ClearPresence();
                        else
                            rpc.SetPresence(PresenceCommon.Utils.CreateDiscordPresence(title, time, stateBox.Text));
                    }
                    ManualUpdate = false;
                    LastTitleID = title.TitleID;
                }
            }
            else
            {
                UpdateStatus("Invalid magic!", Color.Red);

                if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                return;
            }
        }

        private void IpToMac()
        {
            string macAddress = Utils.GetMacByIp(ipAddress.ToString());
            if (macAddress != null)
                addressBox.Text = macAddress;
            else
                MessageBox.Show("Can't convert to MAC Address! Sorry!");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (File.Exists("Config.json"))
            {
                Config cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"));
                checkTime.Checked = cfg.DisplayTimer;
                addressBox.Text = cfg.IP;
                stateBox.Text = cfg.State;
                clientBox.Text = cfg.Client;
                updateIntervalBox.Text = cfg.UpdateInterval;
                checkTray.Checked = cfg.AllowTray;
                checkMainMenu.Checked = cfg.DisplayMainMenu;
                HasSeenMacPrompt = cfg.SeenAutoMacPrompt;
                UseMacDefault.Checked = cfg.AutoToMac;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            listenThread.Abort();
            if (rpc != null && !rpc.IsDisposed)
            {
                rpc.ClearPresence();
                rpc.Dispose();
            }

            if (client != null) client.Close();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && checkTray.Checked)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                if (timer != null) timer.Dispose();

                Config cfg = new Config()
                {
                    IP = addressBox.Text,
                    Client = clientBox.Text,
                    State = stateBox.Text,
                    UpdateInterval = updateIntervalBox.Text,
                    DisplayTimer = checkTime.Checked,
                    AllowTray = checkTray.Checked,
                    DisplayMainMenu = checkMainMenu.Checked,
                    SeenAutoMacPrompt = HasSeenMacPrompt,
                    AutoToMac = UseMacDefault.Checked
                };
                File.WriteAllText("Config.json", JsonConvert.SerializeObject(cfg, Formatting.Indented));
            }
        }

        private void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            Activate();
        }

        private void UpdateStatus(string text, Color color)
        {
            MethodInvoker inv = () =>
            {
                statusLabel.Text = text;
                statusLabel.ForeColor = color;
            };
            Invoke(inv);
        }

        private void CheckTime_CheckedChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void BigKeyBox_TextChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void SKeyBox_TextChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void StateBox_TextChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void BigTextBox_TextChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void TrayExitMenuItem_Click(object sender, EventArgs e) => Application.Exit();

        private void LinkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start($"https://discordapp.com/developers/applications/{clientBox.Text}");

        private void CheckMainMenu_CheckedChanged(object sender, EventArgs e) => ManualUpdate = true;

        private void UseMacDefault_CheckedChanged(object sender, EventArgs e) => HasSeenMacPrompt = true;
    }
}
