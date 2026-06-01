namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement
{
    partial class OrderManagementForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelOrderSummary = new System.Windows.Forms.Panel();
            this.panelProductSelector = new System.Windows.Forms.Panel();
            this.panelActions = new System.Windows.Forms.Panel();
            this.lblCurrentTotal = new System.Windows.Forms.Label();
            this.btnClearOrder = new System.Windows.Forms.Button();
            this.btnSaveOrder = new System.Windows.Forms.Button();
            this.panelTop.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.panelMain.SuspendLayout();
            this.panelActions.SuspendLayout();
            this.SuspendLayout();
            //
            // panelTop
            //
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this.panelTop.Controls.Add(this.lblTitle);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(900, 60);
            this.panelTop.TabIndex = 0;
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(15, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(291, 32);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Order Management System";
            //
            // panelBottom
            //
            this.panelBottom.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.panelBottom.Controls.Add(this.lblStatus);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 560);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(900, 40);
            this.panelBottom.TabIndex = 1;
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(15, 13);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(86, 13);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Ready to take orders";
            //
            // panelMain
            //
            this.panelMain.Controls.Add(this.panelOrderSummary);
            this.panelMain.Controls.Add(this.panelProductSelector);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 60);
            this.panelMain.Name = "panelMain";
            this.panelMain.Padding = new System.Windows.Forms.Padding(10);
            this.panelMain.Size = new System.Drawing.Size(900, 420);
            this.panelMain.TabIndex = 2;
            //
            // panelOrderSummary
            //
            this.panelOrderSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelOrderSummary.Location = new System.Drawing.Point(380, 10);
            this.panelOrderSummary.Name = "panelOrderSummary";
            this.panelOrderSummary.Size = new System.Drawing.Size(510, 400);
            this.panelOrderSummary.TabIndex = 1;
            //
            // panelProductSelector
            //
            this.panelProductSelector.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelProductSelector.Location = new System.Drawing.Point(10, 10);
            this.panelProductSelector.Name = "panelProductSelector";
            this.panelProductSelector.Size = new System.Drawing.Size(370, 400);
            this.panelProductSelector.TabIndex = 0;
            //
            // panelActions
            //
            this.panelActions.Controls.Add(this.lblCurrentTotal);
            this.panelActions.Controls.Add(this.btnClearOrder);
            this.panelActions.Controls.Add(this.btnSaveOrder);
            this.panelActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelActions.Location = new System.Drawing.Point(0, 480);
            this.panelActions.Name = "panelActions";
            this.panelActions.Size = new System.Drawing.Size(900, 80);
            this.panelActions.TabIndex = 3;
            //
            // lblCurrentTotal
            //
            this.lblCurrentTotal.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrentTotal.Location = new System.Drawing.Point(20, 25);
            this.lblCurrentTotal.Name = "lblCurrentTotal";
            this.lblCurrentTotal.Size = new System.Drawing.Size(300, 30);
            this.lblCurrentTotal.TabIndex = 2;
            this.lblCurrentTotal.Text = "Total: $0.00";
            this.lblCurrentTotal.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnClearOrder
            //
            this.btnClearOrder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearOrder.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClearOrder.Location = new System.Drawing.Point(640, 20);
            this.btnClearOrder.Name = "btnClearOrder";
            this.btnClearOrder.Size = new System.Drawing.Size(120, 40);
            this.btnClearOrder.TabIndex = 1;
            this.btnClearOrder.Text = "Clear Order";
            this.btnClearOrder.UseVisualStyleBackColor = true;
            //
            // btnSaveOrder
            //
            this.btnSaveOrder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveOrder.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSaveOrder.Location = new System.Drawing.Point(770, 20);
            this.btnSaveOrder.Name = "btnSaveOrder";
            this.btnSaveOrder.Size = new System.Drawing.Size(120, 40);
            this.btnSaveOrder.TabIndex = 0;
            this.btnSaveOrder.Text = "Save Order";
            this.btnSaveOrder.UseVisualStyleBackColor = true;
            //
            // OrderManagementForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelActions);
            this.Controls.Add(this.panelBottom);
            this.Controls.Add(this.panelTop);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "OrderManagementForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Order Management System - Complex Interaction Demo";
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            this.panelMain.ResumeLayout(false);
            this.panelActions.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelOrderSummary;
        private System.Windows.Forms.Panel panelProductSelector;
        private System.Windows.Forms.Panel panelActions;
        private System.Windows.Forms.Label lblCurrentTotal;
        private System.Windows.Forms.Button btnClearOrder;
        private System.Windows.Forms.Button btnSaveOrder;
    }
}
