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
        UsbToGpio gpio1 = new UsbToGpio();
        Pcan pCan1 = new Pcan();
        Pcan pCan2 = new Pcan();


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (gpio1.ScanForDevs())
            {
                foreach (string s in gpio1.gpioDeviceIds)
                {
                    txtBxTst1.AppendText(s + Environment.NewLine);
                }
            }
            else
                foreach(string s in gpio1.gpioReturnData)
                {
                    txtBxTst1.AppendText(s + Environment.NewLine);                                        
                }

            
            pCan1.ScanForDev(1);
            PrintDataToTxtBoxTst(1, pCan1.pcanReturnData);


        }

        public void PrintDataToTxtBoxTst(int fixNum, List<string> dataToPrint)
        {
            if(fixNum == 1)
            {
                foreach (string s in dataToPrint)
                {
                    txtBxTst1.AppendText(s + Environment.NewLine);
                }
            }
            else if (fixNum == 2)
            {
                foreach (string s in dataToPrint)
                {
                    txtBxTst2.AppendText(s + Environment.NewLine);
                }
            }
            else
            {
                MessageBox.Show("Invalid method parameter"  + Environment.NewLine + "No such fixture: " + fixNum.ToString());
            }
            
        }

        private void btnAsgnCanId_Click(object sender, EventArgs e)
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
                        assignSuccessful = pCan1.SetDevId(1);
                    }
                    else
                    {
                        //assign fixture #2 to the attached device, Device ID #2
                        assignSuccessful = pCan1.SetDevId(2);
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
