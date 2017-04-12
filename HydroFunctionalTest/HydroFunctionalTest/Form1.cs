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
        Pcan tmpPcan = new Pcan();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (tmp.ScanForDevs())
            {
                foreach (string s in tmp.gpioDeviceIds)
                {
                    txtBxTst1.AppendText(s + Environment.NewLine);
                }
            }
            else
                foreach(string s in tmp.gpioReturnData)
                {
                    txtBxTst1.AppendText(s + Environment.NewLine);                                        
                }

            
            if (tmpPcan.ScanForDev(1))
            {
                txtBxTst1.AppendText("Scan for PCAN-USB Adapters:" + Environment.NewLine + "Number of devices found: " + tmpPcan.pcanDevInfo.Count.ToString() + Environment.NewLine);
            }
            else
                txtBxTst1.AppendText("No PCAN-USB devices found.");

            foreach (string s in tmpPcan.pcanReturnData)
            {
                txtBxTst1.AppendText(s + Environment.NewLine);
            }

        }

        private void buttonAssignCanId_Click(object sender, EventArgs e)
        {
            bool assignSuccessful = false;
            if (cboBxDevId.Text.Equals(""))
            {
                MessageBox.Show("Select a fixture from the drop down.");
            }
            else
            {
                DialogResult dr = MessageBox.Show("Instructions:\r\n1)Connect only 1 PCAN-USB Adapter to the PC\r\n2)Click OK\r\n3)When complete, install the adapter to the correct location in the base station", "PCAN-USB Adapter Device ID Assignment", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK)
                {
                    if (cboBxDevId.Text == "Fixture #1")
                    {
                        //assign fixture #1 to the attached device, Device ID #1
                        assignSuccessful = tmpPcan.SetDevId(1);
                        tmpPcan.
                    }
                    else
                    {
                        //assign fixture #2 to the attached device, Device ID #2
                        assignSuccessful = tmpPcan.SetDevId(2);
                    }

                    if (assignSuccessful)
                    {
                        assignSuccessful = true;
                        MessageBox.Show("Successfully associated PCAN-USB adapter to " + cboBxDevId.Text);
                    }
                }
            }
            
        }
    }
}
