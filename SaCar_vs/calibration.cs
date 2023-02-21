using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sunny.UI;

namespace SaCar_vs
{
    public partial class calibration : UIForm
    {
        public event EventHandler<double[]> WindowClosed;
        double[] newnumbers = new double[10];//测试用

        public calibration(double[] numbers)
        {
            InitializeComponent();
            newnumbers = numbers;
            this.FormClosing += calibration_FormClosing;
        }

        private void CloseWindow()
        {
            double[] textBoxValues = new double[10];

            try
            {
                
                textBoxValues[0] = Convert.ToDouble(uiTextBox1.Text);
                textBoxValues[1] = Convert.ToDouble(uiTextBox2.Text);
                textBoxValues[2] = Convert.ToDouble(uiTextBox3.Text);
                textBoxValues[3] = Convert.ToDouble(uiTextBox4.Text);
                textBoxValues[4] = Convert.ToDouble(uiTextBox5.Text);
                textBoxValues[5] = Convert.ToDouble(uiTextBox6.Text);
                textBoxValues[6] = Convert.ToDouble(uiTextBox7.Text);
                textBoxValues[7] = Convert.ToDouble(uiTextBox8.Text);
                textBoxValues[8] = Convert.ToDouble(uiTextBox9.Text);
                textBoxValues[9] = Convert.ToDouble(uiTextBox10.Text);

                WindowClosed?.Invoke(this, textBoxValues);
                Console.WriteLine("CloseWindow done!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                
            }
            // Get the values of the ten TextBoxes and store them in the textBoxValues array
            
        }

        private void calibration_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseWindow();
        }

        private void uiButton1_Click(object sender, EventArgs e)
        {
            uiTextBox1.Text = uiLabel1.Text;
        }

        private void uiButton2_Click(object sender, EventArgs e)
        {
            uiTextBox1.Enabled = false;
        }

        private void uiButton3_Click(object sender, EventArgs e)
        {
            uiTextBox1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            uiLabel1.Text = Convert.ToString(newnumbers[0]);
            uiLabel2.Text = Convert.ToString(newnumbers[1]);
            uiLabel3.Text = Convert.ToString(newnumbers[2]);
            uiLabel4.Text = Convert.ToString(newnumbers[3]);
            uiLabel5.Text = Convert.ToString(newnumbers[4]);
            uiLabel6.Text = Convert.ToString(newnumbers[5]);
            uiLabel7.Text = Convert.ToString(newnumbers[6]);
            uiLabel8.Text = Convert.ToString(newnumbers[7]);
            uiLabel9.Text = Convert.ToString(newnumbers[8]);
            uiLabel10.Text = Convert.ToString(newnumbers[9]);

        }
    }
}
