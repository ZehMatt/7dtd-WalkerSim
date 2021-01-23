using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WalkerSim
{
    public partial class FormMain : Form
    {
        private System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private ViewerClient _client = new ViewerClient();

        public FormMain()
        {
            InitializeComponent();

            _timer.Interval = 1;
            _timer.Tick += new EventHandler(OnTick);
            _timer.Start();
        }

        int Scale(int v)
        {
            return (int)((float)v * 1.5);
        }

        Bitmap GetBitmap(State state)
        {
            int topBorderSize = 14;

            var worldInfo = state.worldInfo;
            var worldZones = state.worldZones;
            var poiZones = state.poiZones;
            var playerZones = state.playerZones;
            var active = state.active;
            var inactive = state.inactive;

            if (worldInfo.w == 0 || worldInfo.h == 0)
                return null;

            Bitmap bm = new Bitmap(Scale(worldInfo.w), Scale(worldInfo.h) + topBorderSize);
            using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(bm))
            {
                gr.SmoothingMode = SmoothingMode.None;
                gr.Clear(System.Drawing.Color.Black);

                // World Zones
                if (chkGrid.Checked && worldZones.zones != null)
                {
                    foreach (var zone in worldZones.zones)
                    {
                        gr.DrawRectangle(Pens.Gray, Scale(zone.x1), Scale(zone.y1), Scale(zone.x2 - zone.x1), Scale(zone.y2 - zone.y1));
                    }
                }

                // POIS
                if (chkPOIs.Checked && poiZones.zones != null)
                {
                    foreach (var poi in poiZones.zones)
                    {
                        // Zone
                        gr.DrawRectangle(Pens.DarkGray, Scale(poi.x1), Scale(poi.y1), Scale(poi.x2 - poi.x1), Scale(poi.y2 - poi.y1));
                    }
                }

                // Draw inactive.
                if (chkInactive.Checked && inactive.list != null)
                {
                    foreach (var zombie in inactive.list)
                    {
                        gr.FillEllipse(Brushes.Red, Scale(zombie.x), Scale(zombie.y), 2, 2);
                    }
                }

                // Active
                if (chkActive.Checked && active.list != null)
                {
                    foreach (var zombie in active.list)
                    {
                        gr.FillEllipse(Brushes.Blue, Scale(zombie.x), Scale(zombie.y), 2, 2);
                    }
                }

                // Visible zones.
                if (chkPlayers.Checked && playerZones.zones != null)
                {
                    foreach (var zone in playerZones.zones)
                    {
                        // Zone
                        gr.DrawRectangle(Pens.Green, Scale(zone.x1), Scale(zone.y1), Scale(zone.x2 - zone.x1), Scale(zone.y2 - zone.y1));

                        // Spawn Block.
                        gr.DrawRectangle(Pens.Yellow, Scale(zone.x3), Scale(zone.y3), Scale(zone.x4 - zone.x3), Scale(zone.y4 - zone.y3));
                    }
                }

                // Stats
                {
                    gr.FillRectangle(Brushes.Black, 0, 0, worldInfo.mapW, 20);

                    float x = 4.0f;

                    gr.DrawString(string.Format("Speed: {0}x", worldInfo.timescale), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 70.0f;

                    gr.DrawString(string.Format("Map: {0}x{1}", worldInfo.mapW, worldInfo.mapH), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Density: {0}/km²", worldInfo.density), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Inactive: {0}", inactive.list == null ? 0 : inactive.list.Count), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Active: {0}", active.list == null ? 0 : active.list.Count), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 70.0f;

                    gr.DrawString(string.Format("Zombie Speed: {0}", worldInfo.zombieSpeed), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;
                }
            }
            return bm;
        }

        private void OnTick(Object myObject, EventArgs myEventArgs)
        {
            mapImage.Visible = _client.IsConnected();

            if (_client.IsConnecting())
            {
                btConnect.Text = "Cancel";
                return;
            }

            if (!_client.IsConnected())
            {
                btConnect.Text = "Connect";
                if (mapImage.Image != null)
                {
                    mapImage.Image.Dispose();
                    mapImage.Image = null;
                }
                return;
            }
            else
            {
                btConnect.Text = "Disconnect";
            }

            var state = _client.GetMapData();
            if (state != null)
            {
                Bitmap bm = GetBitmap(state);
                if (mapImage.Image != null)
                    mapImage.Image.Dispose();
                mapImage.Image = bm;
                mapImage.Width = bm.Width;
                mapImage.Visible = true;
                mapImage.Height = bm.Height;
            }
            else
            {
                if (mapImage.Image != null)
                {
                    mapImage.Image.Dispose();
                    mapImage.Image = null;
                }
            }
        }

        private void OnClick(object sender, EventArgs e)
        {
            if (btConnect.Text == "Connect")
            {
                try
                {
                    string[] args = txtRemote.Text.Split(':');
                    if (args.Length != 2)
                    {
                        MessageBox.Show("Invalid remote host, format is: ip:port");
                        return;
                    }
                    string host = args[0];
                    int port = int.Parse(args[1]);

                    _client.Connect(host, port);
                }
                catch (Exception)
                {

                    throw;
                }
            }
            else if (btConnect.Text == "Cancel" || btConnect.Text == "Disconnect")
            {
                _client.Disconnect();
                btConnect.Text = "Connect";
            }
        }
    }
}
