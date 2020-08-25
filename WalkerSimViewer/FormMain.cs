using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

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

        Bitmap GetBitmap(Viewer.MapData mapData)
        {
            int topBorderSize = 14;

            Bitmap bm = new Bitmap(Scale(mapData.w), Scale(mapData.h) + topBorderSize);
            using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(bm))
            {
                gr.SmoothingMode = SmoothingMode.None;
                gr.Clear(System.Drawing.Color.Black);

                // World Zones
                if (chkGrid.Checked)
                {
                    foreach (var zone in mapData.worldZones)
                    {
                        gr.DrawRectangle(Pens.Gray, Scale(zone.x1), Scale(zone.y1), Scale(zone.x2 - zone.x1), Scale(zone.y2 - zone.y1));
                    }
                }

                // POIS
                if (chkPOIs.Checked)
                {
                    foreach (var poi in mapData.poiZones)
                    {
                        // Zone
                        gr.DrawRectangle(Pens.Yellow, Scale(poi.x1), Scale(poi.y1), Scale(poi.x2 - poi.x1), Scale(poi.y2 - poi.y1));
                    }
                }

                // Draw inactive.
                if (chkInactive.Checked)
                {
                    foreach (var zombie in mapData.inactive)
                    {
                        gr.FillEllipse(Brushes.Red, Scale(zombie.x), Scale(zombie.y), 2, 2);
                    }
                }

                // Active
                if (chkActive.Checked)
                {
                    foreach (var zombie in mapData.active)
                    {
                        gr.FillEllipse(Brushes.Blue, Scale(zombie.x), Scale(zombie.y), 2, 2);
                    }
                }

                // Visible zones.
                if (chkPlayers.Checked)
                {
                    foreach (var zone in mapData.playerZones)
                    {
                        // Zone
                        gr.DrawRectangle(Pens.Green, Scale(zone.x1), Scale(zone.y1), Scale(zone.x2 - zone.x1), Scale(zone.y2 - zone.y1));

                        // Spawn Block.
                        gr.DrawRectangle(Pens.Yellow, Scale(zone.x3), Scale(zone.y3), Scale(zone.x4 - zone.x3), Scale(zone.y4 - zone.y3));
                    }
                }

                // Stats
                {
                    gr.FillRectangle(Brushes.Black, 0, 0, mapData.mapW, 20);

                    float x = 4.0f;

                    gr.DrawString(string.Format("Speed: {0}x", mapData.timescale), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 70.0f;

                    gr.DrawString(string.Format("Map: {0}x{1}", mapData.mapW, mapData.mapH), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Density: {0}/km²", mapData.density), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Inactive: {0}", mapData.inactive.Count), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 100.0f;

                    gr.DrawString(string.Format("Active: {0}", mapData.active.Count), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
                    x += 70.0f;

                    gr.DrawString(string.Format("Zombie Speed: {0}", mapData.zombieSpeed), SystemFonts.DefaultFont, Brushes.Green, x, 4.0f);
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
                return;
            }
            else
            {
                btConnect.Text = "Disconnect";
            }

            Viewer.MapData mapData = _client.GetMapData();
            if (mapData != null)
            {
                Bitmap bm = GetBitmap(mapData);
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
