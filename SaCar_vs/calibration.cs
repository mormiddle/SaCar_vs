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

                textBoxValues[0] = Convert.ToDouble(uiTextBox1.Text) + 10;
                textBoxValues[1] = Convert.ToDouble(uiTextBox2.Text) + 10;
                textBoxValues[2] = Convert.ToDouble(uiTextBox3.Text) + 10;
                textBoxValues[3] = Convert.ToDouble(uiTextBox4.Text) + 10;
                textBoxValues[4] = Convert.ToDouble(uiTextBox5.Text) + 10;
                textBoxValues[5] = Convert.ToDouble(uiTextBox6.Text) + 10;
                textBoxValues[6] = Convert.ToDouble(uiTextBox7.Text) + 10;
                textBoxValues[7] = Convert.ToDouble(uiTextBox8.Text) + 10;
                textBoxValues[8] = Convert.ToDouble(uiTextBox9.Text) + 10;
                textBoxValues[9] = Convert.ToDouble(uiTextBox10.Text) + 10;

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
    }
}
