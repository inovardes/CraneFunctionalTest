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
        #region Hardware Class objects
        /// <summary>
        /// 2 obj. instances for Read/Write from/to GPIO adapter.
        /// </summary>
        UsbToGpio[] gpioObj = new UsbToGpio[2];
        /// <summary>
        /// 2 obj. instances for Receive/Transmit from/to PCAN adapter.
        /// Must call "DeactivateDevice" when the device is no longer needed
        /// </summary>
        Pcan[] pCanObj = new Pcan[2];
        /// <summary>
        /// 
        /// </summary>
        //PwrSup pwrSupObj = new PwrSup;
        /// <summary>
        /// 
        /// </summary>
        //Dmm dmmObj = new Dmm;
        /// <summary>
        /// 
        /// </summary>
        //Eload eLoadObj = new Eload;
        #endregion Hardware Class objects

        #region Fixture Class objects
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        PSM_85307[] PSM_uutObj = new PSM_85307[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>  
        //SAM_55207[] SAM_uutObj = new SAM_55207[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //PCM_90707[] PCM__uutObj = new PCM_90707[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //AIM_90807[] AIM_uutObj = new AIM_90807[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //LUM_15607[] LUM_uutObj = new LUM_15607[2];
        #endregion Fixture Class objects

        /// <summary>
        /// For indexing through the 2-D arrays that are associated with UUT1
        /// </summary>
        const int uut1_index = 0;
        /// <summary>
        /// For indexing through the 2-D arrays that are associated with UUT2
        /// </summary>
        const int uut2_index = 1;
        /// <summary>
        /// Constant for visual representation of Fixture 1 (easier to read code)
        /// </summary>
        const int fix1Desig = 1;
        /// <summary>
        /// Constant for visual representation of Fixture 2 (easier to read code)
        /// </summary>
        const int fix2Desig = 2;


        /// <summary>
        /// Measurement Limits for Fixture ID voltage divider circuit. Key = assembly name, value[] = High limit(volts), Low limit(volts)
        /// </summary>
        public Dictionary<string, double[]> fxtIDs = new Dictionary<string, double[]>
        {
            { "PSM_85307", new double[] { 5.2, 4.8 } },
            { "SAM_55207", new double[] { 4.2, 3.8 } },
            { "PCM_90707", new double[] { 3.2, 2.8 } },
            { "AIM_90807", new double[] { 2.2, 3.8 } },
            { "LUM_15607", new double[] { 1.2, .8 } },
        };

        #region Flip Flop control commands
        /// <summary>
        /// Port/Pin info to send to GPIO adapter to set/clear pins.   Key = relay control net name per schematic, value[] = I/O port, I/O pin, control port, control pin
        /// pass these Dictionary values along with disable/enable command as method parameters
        /// </summary>
        Dictionary<string, int[]> flipFlopCtrlCmd = new Dictionary<string, int[]>
            {
                { "FIXTR_ID_EN", new int[] { 0, 4, 2, 5 } },
            };
        #endregion Flip Flop control commands


        public Form1()
        {            
            InitializeComponent();
        }

        /// <summary>
        /// Setup the Power supply, DMM, ELoad, CAN adpts.  
        /// Update the GUI with equipment status
        /// </summary>
        /// <returns></returns>
        public bool SetupHardware()
        {
            //Instantiate hardware object array elements
            for (int i = 0; i < 2; i++)
            {
                gpioObj[i] = new UsbToGpio();
                pCanObj[i] = new Pcan();
            }
            return true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetupHardware();
        }

        public void PrintDataToTxtBoxTst(int fixNum, List<string> dataToPrint, string optString = "")
        {
            if(fixNum == 1)
            {
                if(dataToPrint != null)
                {
                    foreach (string s in dataToPrint)
                    {
                        txtBxTst1.AppendText(Environment.NewLine + s);
                    }
                    txtBxTst1.AppendText(optString);
                }
                else
                    txtBxTst1.AppendText(Environment.NewLine + optString);
            }
            else if (fixNum == 2)
            {
                if (dataToPrint != null)
                {
                    foreach (string s in dataToPrint)
                    {
                        txtBxTst2.AppendText(Environment.NewLine + s);
                    }
                    txtBxTst2.AppendText(optString);
                }
                else
                    txtBxTst2.AppendText(Environment.NewLine + optString);
            }
            else
            {
                MessageBox.Show("Invalid method parameter"  + Environment.NewLine + "No such fixture: " + fixNum.ToString());
            }
            
        }

        private void btnAsgnCanId_Click(object sender, EventArgs e)
        {
            UInt16 deviceID = 1;
            bool assignSuccessful = false;
            if (cboBxSlctFx.Text.Equals("Select Fixture"))
            {
                MessageBox.Show("Select a fixture from the drop down.");
            }
            else
            {
                DialogResult dr = MessageBox.Show("Instructions:" + Environment.NewLine +
                    "1)Connect only 1 PCAN-USB Adapter to the PC" + Environment.NewLine + 
                    "2)Click OK" ,
                    "PCAN-USB Adapter Device ID Assignment", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK)
                {
                    if (cboBxSlctFx.Text == "Fixture #1")
                    {
                        //assign fixture #1 to the attached device, Device ID #1
                        deviceID = 1;
                        assignSuccessful = pCanObj[uut1_index].SetDevId(deviceID);
                    }
                    else
                    {
                        //assign fixture #2 to the attached device, Device ID #2
                        deviceID = 2;
                        assignSuccessful = pCanObj[uut2_index].SetDevId(deviceID);
                    }

                    if (assignSuccessful)
                    {
                        assignSuccessful = true;
                        MessageBox.Show("Successfully associated PCAN-USB adapter to " + cboBxSlctFx.Text + Environment.NewLine +
                            "PCAN adapter device ID = " + deviceID.ToString() + Environment.NewLine + Environment.NewLine +
                            "Install the PCAN adapter into the correct position (UUT1 or UUT2) inside the base station (connector J13 or J21).  " +
                            "See Crane Test Fixture Base Station, Connector Interface, schematic for more detail");
                    }
                }
            }
        }

        private async void btnStrTst1_Click(object sender, EventArgs e)
        {
            //check for available gpio device
            if (gpioObj[uut1_index].ScanForDevs(fix1Desig))
            {
                PrintDataToTxtBoxTst(fix1Desig, gpioObj[uut1_index].gpioReturnData, "\r\nFixture " + fix1Desig.ToString() + " connected to " + gpioObj[uut1_index].GetDeviceId());

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut1_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBoxTst(fix1Desig, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Detected");
                    //Get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate UUT in object array:
                    ////////double fixtureIdValue = 5; //Read value from DMM

                    ////////double temp;
                    ////////foreach(var pair in fxtIDs)
                    ////////{
                    ////////    temp = pair.Value[0];
                    ////////}
                    ////////if (fixtureIdValue == fxtIDs[0])
                    ////////{

                    ////////}


                    PSM_uutObj[uut1_index] = new PSM_85307(gpioObj[uut1_index], pCanObj[uut1_index]);
                    PSM_uutObj[uut1_index].InformationAvailable += OnInformationAvailable;

                    await Task.Run(() => PSM_uutObj[uut1_index].TestLongRunningMethod());
                    //  SAM_uutObj[uut1_index] = new SAM_55207();
                    //  PCM_uutObj[uut1_index] = new PCM_90707();
                    //  AIM_uutObj[uut1_index] = new AIM_90807();
                    //  LUM_uutObj[uut1_index] = new LUM_15607();
                }
                else
                    PrintDataToTxtBoxTst(fix1Desig, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Not Detected");                
            }
            else
                PrintDataToTxtBoxTst(fix1Desig, gpioObj[uut1_index].gpioReturnData, "\r\nFixture 1 not connected");      
        }

        private void OnInformationAvailable(object source, EventArgs args)
        {
            MessageBox.Show("Information available Event!");
        }

        private async void btnStrTst2_Click(object sender, EventArgs e)
        {
            //check for available gpio device
            if (gpioObj[uut2_index].ScanForDevs(fix2Desig))
            {
                PrintDataToTxtBoxTst(fix2Desig, gpioObj[uut2_index].gpioReturnData, "\r\nFixture " + fix2Desig.ToString() + " connected to " + gpioObj[uut2_index].GetDeviceId());

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut2_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBoxTst(fix2Desig, gpioObj[uut2_index].gpioReturnData, "\r\nLid Down Detected");
                    //if limit switch is activated, get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate UUT in object array:
                    //  PSM_uutObj[uut1_index] = new PSM_85307();
                    //  SAM_uutObj[uut1_index] = new SAM_55207();
                    //  PCM_uutObj[uut1_index] = new PCM_90707();
                    //  AIM_uutObj[uut1_index] = new AIM_90807();
                    //  LUM_uutObj[uut1_index] = new LUM_15607();
                }
                else
                    PrintDataToTxtBoxTst(fix2Desig, gpioObj[uut2_index].gpioReturnData, "\r\nLid Down Not Detected");                
            }
            else
                PrintDataToTxtBoxTst(fix2Desig, gpioObj[uut2_index].gpioReturnData, "\r\nFixture 2 not connected");
        }

        /// <summary>
        /// Waits for Limit switch to activate.  When called by a task, Method runs continually if no UUT installed
        /// </summary>
        public void OnTestComplete()
        {

        }
    }

}
