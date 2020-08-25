namespace WalkerSim
{
	partial class FormMain
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.mapImage = new System.Windows.Forms.PictureBox();
            this.grpClient = new System.Windows.Forms.GroupBox();
            this.chkPOIs = new System.Windows.Forms.CheckBox();
            this.chkPlayers = new System.Windows.Forms.CheckBox();
            this.chkActive = new System.Windows.Forms.CheckBox();
            this.chkInactive = new System.Windows.Forms.CheckBox();
            this.chkGrid = new System.Windows.Forms.CheckBox();
            this.btConnect = new System.Windows.Forms.Button();
            this.txtRemote = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.mapImage)).BeginInit();
            this.grpClient.SuspendLayout();
            this.SuspendLayout();
            // 
            // mapImage
            // 
            this.mapImage.Location = new System.Drawing.Point(4, 80);
            this.mapImage.Name = "mapImage";
            this.mapImage.Size = new System.Drawing.Size(355, 296);
            this.mapImage.TabIndex = 0;
            this.mapImage.TabStop = false;
            // 
            // grpClient
            // 
            this.grpClient.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpClient.Controls.Add(this.chkPOIs);
            this.grpClient.Controls.Add(this.chkPlayers);
            this.grpClient.Controls.Add(this.chkActive);
            this.grpClient.Controls.Add(this.chkInactive);
            this.grpClient.Controls.Add(this.chkGrid);
            this.grpClient.Controls.Add(this.btConnect);
            this.grpClient.Controls.Add(this.txtRemote);
            this.grpClient.Location = new System.Drawing.Point(12, 6);
            this.grpClient.Name = "grpClient";
            this.grpClient.Size = new System.Drawing.Size(343, 69);
            this.grpClient.TabIndex = 3;
            this.grpClient.TabStop = false;
            this.grpClient.Text = "Client";
            // 
            // chkPOIs
            // 
            this.chkPOIs.AutoSize = true;
            this.chkPOIs.Checked = true;
            this.chkPOIs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPOIs.Location = new System.Drawing.Point(258, 44);
            this.chkPOIs.Name = "chkPOIs";
            this.chkPOIs.Size = new System.Drawing.Size(49, 17);
            this.chkPOIs.TabIndex = 9;
            this.chkPOIs.Text = "POIs";
            this.chkPOIs.UseVisualStyleBackColor = true;
            // 
            // chkPlayers
            // 
            this.chkPlayers.AutoSize = true;
            this.chkPlayers.Checked = true;
            this.chkPlayers.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPlayers.Location = new System.Drawing.Point(192, 44);
            this.chkPlayers.Name = "chkPlayers";
            this.chkPlayers.Size = new System.Drawing.Size(60, 17);
            this.chkPlayers.TabIndex = 8;
            this.chkPlayers.Text = "Players";
            this.chkPlayers.UseVisualStyleBackColor = true;
            // 
            // chkActive
            // 
            this.chkActive.AutoSize = true;
            this.chkActive.Checked = true;
            this.chkActive.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkActive.Location = new System.Drawing.Point(130, 44);
            this.chkActive.Name = "chkActive";
            this.chkActive.Size = new System.Drawing.Size(56, 17);
            this.chkActive.TabIndex = 7;
            this.chkActive.Text = "Active";
            this.chkActive.UseVisualStyleBackColor = true;
            // 
            // chkInactive
            // 
            this.chkInactive.AutoSize = true;
            this.chkInactive.Checked = true;
            this.chkInactive.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkInactive.Location = new System.Drawing.Point(60, 44);
            this.chkInactive.Name = "chkInactive";
            this.chkInactive.Size = new System.Drawing.Size(64, 17);
            this.chkInactive.TabIndex = 6;
            this.chkInactive.Text = "Inactive";
            this.chkInactive.UseVisualStyleBackColor = true;
            // 
            // chkGrid
            // 
            this.chkGrid.AutoSize = true;
            this.chkGrid.Location = new System.Drawing.Point(9, 45);
            this.chkGrid.Name = "chkGrid";
            this.chkGrid.Size = new System.Drawing.Size(45, 17);
            this.chkGrid.TabIndex = 5;
            this.chkGrid.Text = "Grid";
            this.chkGrid.UseVisualStyleBackColor = true;
            // 
            // btConnect
            // 
            this.btConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btConnect.Location = new System.Drawing.Point(223, 16);
            this.btConnect.Name = "btConnect";
            this.btConnect.Size = new System.Drawing.Size(114, 23);
            this.btConnect.TabIndex = 4;
            this.btConnect.Text = "Connect";
            this.btConnect.UseVisualStyleBackColor = true;
            this.btConnect.Click += new System.EventHandler(this.OnClick);
            // 
            // txtRemote
            // 
            this.txtRemote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRemote.Location = new System.Drawing.Point(9, 18);
            this.txtRemote.Name = "txtRemote";
            this.txtRemote.Size = new System.Drawing.Size(208, 20);
            this.txtRemote.TabIndex = 3;
            this.txtRemote.Text = "127.0.0.1:13632";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(367, 381);
            this.Controls.Add(this.grpClient);
            this.Controls.Add(this.mapImage);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "WalkerSimView";
            ((System.ComponentModel.ISupportInitialize)(this.mapImage)).EndInit();
            this.grpClient.ResumeLayout(false);
            this.grpClient.PerformLayout();
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.PictureBox mapImage;
		private System.Windows.Forms.GroupBox grpClient;
		private System.Windows.Forms.CheckBox chkPlayers;
		private System.Windows.Forms.CheckBox chkActive;
		private System.Windows.Forms.CheckBox chkInactive;
		private System.Windows.Forms.CheckBox chkGrid;
		private System.Windows.Forms.Button btConnect;
		private System.Windows.Forms.TextBox txtRemote;
		private System.Windows.Forms.CheckBox chkPOIs;
	}
}

