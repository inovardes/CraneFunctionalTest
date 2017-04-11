using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HydroFunctionalTest
{
    public partial class Form1 : Form
    {
        UsbToGpio tmp = new UsbToGpio();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (tmp.InitializeGpio())
            {
                foreach (string s in tmp.gpioDeviceIds)
                {
                    textBoxFix1.AppendText(s + Environment.NewLine);
                }
            }
            else
                foreach(string s in tmp.gpioReturnData)
                {
                    textBoxFix1.AppendText(s + Environment.NewLine);                                        
                }

        }

        private void button1_Click(object sender, EventArgs e)
        {

            tmp.GpioWrite(tmp.gpioDeviceIds[1], 1, 0);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            tmp.GpioRead(tmp.gpioDeviceIds[1], 1).ToString();
            foreach (string s in tmp.gpioReturnData)
            {
                textBoxFix1.AppendText(s + Environment.NewLine);
            }
        }
    }
}
