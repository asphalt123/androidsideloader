﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class NewApps : Form
    {
        private bool mouseDown;
        private Point lastLocation;

        public NewApps()
        {
            InitializeComponent();
        }

        private void label2_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            lastLocation = e.Location;
        }

        private void label2_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.Location = new Point(
                    (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

                this.Update();
            }
        }

        private void label2_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }

        private void DonateButton_Click(object sender, EventArgs e)
        {
            string HWID = SideloaderUtilities.UUID();
            foreach (ListViewItem listItem in NewAppsListView.Items)
            {
                if (listItem.Checked)
                {
                    Properties.Settings.Default.NonAppPackages += listItem.SubItems[Donors.PackageNameIndex].Text + ";" + HWID + "\n";
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.AppPackages += listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
                    Properties.Settings.Default.Save();
                }
            }
            MainForm.newpackageupload();
            this.Close();
        }

        private void NewApps_Load(object sender, EventArgs e)
        {
            NewAppsListView.Items.Clear();
            Donors.initNewApps();
            List<ListViewItem> NewAppList = new List<ListViewItem>();
            foreach (string[] release in Donors.newApps)
            {
                ListViewItem NGame = new ListViewItem(release);
                if (!NewAppList.Contains(NGame))
                NewAppList.Add(NGame);
            }
            ListViewItem[] arr = NewAppList.ToArray();
            NewAppsListView.BeginUpdate();
            NewAppsListView.Items.Clear();
            NewAppsListView.Items.AddRange(arr);
            NewAppsListView.EndUpdate();
        }
    }
}
