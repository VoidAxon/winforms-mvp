namespace WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector
{
    partial class ProductSelectorView
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

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnAddToOrder = new System.Windows.Forms.Button();
            this.numQuantity = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.listProducts = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numQuantity)).BeginInit();
            this.SuspendLayout();
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.btnAddToOrder);
            this.groupBox1.Controls.Add(this.numQuantity);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.listProducts);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(350, 400);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Product Selection";
            //
            // btnAddToOrder
            //
            this.btnAddToOrder.Location = new System.Drawing.Point(190, 360);
            this.btnAddToOrder.Name = "btnAddToOrder";
            this.btnAddToOrder.Size = new System.Drawing.Size(140, 30);
            this.btnAddToOrder.TabIndex = 4;
            this.btnAddToOrder.Text = "Add to Order";
            this.btnAddToOrder.UseVisualStyleBackColor = true;
            //
            // numQuantity
            //
            this.numQuantity.Location = new System.Drawing.Point(90, 365);
            this.numQuantity.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numQuantity.Name = "numQuantity";
            this.numQuantity.Size = new System.Drawing.Size(80, 20);
            this.numQuantity.TabIndex = 3;
            this.numQuantity.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(20, 367);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Quantity:";
            //
            // listProducts
            //
            this.listProducts.FormattingEnabled = true;
            this.listProducts.Location = new System.Drawing.Point(20, 40);
            this.listProducts.Name = "listProducts";
            this.listProducts.Size = new System.Drawing.Size(310, 303);
            this.listProducts.TabIndex = 1;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(91, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Available Products:";
            //
            // ProductSelectorView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "ProductSelectorView";
            this.Size = new System.Drawing.Size(350, 400);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numQuantity)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnAddToOrder;
        private System.Windows.Forms.NumericUpDown numQuantity;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox listProducts;
        private System.Windows.Forms.Label label1;
    }
}
