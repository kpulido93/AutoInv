using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoInventario
{
    partial class ConfigForm
    {
        private System.ComponentModel.IContainer components = null;
        private ComboBox comboClients;
        private Button btnSave;
        private Button btnCancel;
        private Label lblTitle;
        private TableLayoutPanel layoutPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigForm));
            layoutPanel = new TableLayoutPanel();
            lblTitle = new Label();
            comboClients = new ComboBox();
            btnSave = new Button();
            btnCancel = new Button();
            layoutPanel.SuspendLayout();
            SuspendLayout();
            //
            // layoutPanel
            //
            layoutPanel.ColumnCount = 2;
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutPanel.Controls.Add(lblTitle, 0, 0);
            layoutPanel.Controls.Add(comboClients, 0, 1);
            layoutPanel.Controls.Add(btnSave, 0, 2);
            layoutPanel.Controls.Add(btnCancel, 1, 2);
            layoutPanel.Dock = DockStyle.Fill;
            layoutPanel.Location = new Point(0, 0);
            layoutPanel.Margin = new Padding(3, 2, 3, 2);
            layoutPanel.Name = "layoutPanel";
            layoutPanel.Padding = new Padding(18, 15, 18, 15);
            layoutPanel.RowCount = 3;
            layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layoutPanel.Size = new Size(438, 188);
            layoutPanel.TabIndex = 0;
            //
            // lblTitle
            //
            layoutPanel.SetColumnSpan(lblTitle, 2);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblTitle.Location = new Point(21, 15);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(396, 30);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Seleccione el cliente:";
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // comboClients
            //
            layoutPanel.SetColumnSpan(comboClients, 2);
            comboClients.Dock = DockStyle.Fill;
            comboClients.DropDownStyle = ComboBoxStyle.DropDownList;
            comboClients.Font = new Font("Segoe UI", 10F);
            comboClients.Location = new Point(21, 47);
            comboClients.Margin = new Padding(3, 2, 3, 2);
            comboClients.Name = "comboClients";
            comboClients.Size = new Size(396, 25);
            comboClients.TabIndex = 1;
            //
            // btnSave
            //
            btnSave.Dock = DockStyle.Fill;
            btnSave.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSave.Location = new Point(21, 137);
            btnSave.Margin = new Padding(3, 2, 3, 2);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(195, 34);
            btnSave.TabIndex = 2;
            btnSave.Text = "Guardar";
            btnSave.Click += btnSave_Click;
            //
            // btnCancel
            //
            btnCancel.Dock = DockStyle.Fill;
            btnCancel.Font = new Font("Segoe UI", 10F);
            btnCancel.Location = new Point(222, 137);
            btnCancel.Margin = new Padding(3, 2, 3, 2);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(195, 34);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancelar";
            btnCancel.Click += btnClose_Click;
            //
            // ConfigForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(438, 188);
            Controls.Add(layoutPanel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 2, 3, 2);
            MinimumSize = new Size(352, 160);
            Name = "ConfigForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Configuración de AutoInventario";
            Load += ConfigForm_Load;
            layoutPanel.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
