using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

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
        /// Represents array containing two object instances, UUT1 & UUT2.
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //PSM_85307[] PSM_uutObj = new PSM_85307[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2.
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>  
        //SAM_55207[] SAM_uutObj = new SAM_55207[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2.
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //PCM_90707[] PCM__uutObj = new PCM_90707[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2.
        /// UUT1 or UUT2 objects should only be instantiated if the fixture is connected.
        /// </summary>
        //AIM_90807[] AIM_uutObj = new AIM_90807[2];
        /// <summary>
        /// Represents array containing two object instances, UUT1 & UUT2.
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
        const int fix1Designator = 1;
        /// <summary>
        /// Constant for visual representation of Fixture 2 (easier to read code)
        /// </summary>
        const int fix2Designator = 2;


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
     
        public void PrintDataToTxtBox(int fixPos, List<string> dataToPrint, string optString = "", bool clearTxtBox = false)
        {
            String tmpStr = "";
            if (dataToPrint != null)
            {
                dataToPrint.Add(optString);
                if (fixPos == fix1Designator)
                {
                    foreach (string s in dataToPrint)
                    {
                        tmpStr = tmpStr + Environment.NewLine + s;
                    }
                    TxtBxCtrl(fixPos, true, tmpStr, false);
                }
                else if (fixPos == fix2Designator)
                {
                    foreach (string s in dataToPrint)
                    {
                        tmpStr = tmpStr + Environment.NewLine + s;
                    }
                    TxtBxCtrl(fixPos, true, tmpStr, false);
                }
                else
                {
                    MessageBox.Show("Invalid method parameter" + Environment.NewLine + "No such fixture: " + fixPos.ToString());
                }
            }
            else
            {
                if (fixPos == fix1Designator)
                    TxtBxCtrl(fixPos, true, "No data returned", false);
                else if (fixPos == fix2Designator)
                    TxtBxCtrl(fixPos, true, "No data returned", false);
                else
                    MessageBox.Show("Invalid method parameter" + Environment.NewLine + "No such fixture: " + fixPos.ToString());
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
            //clear the txtbox
            TxtBxCtrl(fix1Designator);//clear the text box of old data
            //reset background color, any integer other than 0 or 1 will work
            GrpBxCtrl(fix1Designator, -1);//make background neutral to start test
            //check for available gpio device
            if (gpioObj[uut1_index].ScanForDevs(fix1Designator))
            {
                PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nFixture " + fix1Designator.ToString() + " connected to " + gpioObj[uut1_index].GetDeviceId());

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut1_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Detected");
                    //Get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate specific UUT in object array:
                    double dmmMeasuredValue = 5; //read value from dmm


                    //Type classInstanceType = Type.GetType(fxtIDs.Keys.First());

                    foreach (var pair in fxtIDs)
                    {
                        if ((dmmMeasuredValue < pair.Value[0]) && (dmmMeasuredValue > pair.Value[1]))
                        {
                            //classInstanceType = Type.GetType(pair.Key);
                            if (fxtIDs.ContainsKey("PSM_85307"))
                            {
                                PSM_85307 PSM_Obj = new PSM_85307(fix1Designator, gpioObj[uut1_index], pCanObj[uut1_index]);
                                PSM_Obj.InformationAvailable += OnInformationAvailable;
                                PSM_Obj.TestComplete += OnTestComplete;
                                //lock down the GUI so no other input can be received other than to cancel the task
                                BtnCtrl(fix1Designator, false);                                
                                await Task.Run(() => PSM_Obj.TestLongRunningMethod());
                            }
                            else if (fxtIDs.ContainsKey("SAM_55207"))
                            {
                            }
                            else if (fxtIDs.ContainsKey("PCM_90707"))
                            {
                            }
                            else if (fxtIDs.ContainsKey("AIM_90807"))
                            {
                            }
                            else if (fxtIDs.ContainsKey("LUM_15607"))
                            {
                            }
                            else
                            {
                                PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Detected");
                            }
                        }
                    }
                    //object uut1Obj = Activator.CreateInstance(classInstanceType, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //EventInfo infoAvailEvent = classInstanceType.GetEvent("InformationAvailable");
                    //Type tDelegate = infoAvailEvent.EventHandlerType;
                    //MethodInfo miHandler = classInstanceType.GetMethod("OnInformationAvailable", BindingFlags.NonPublic | BindingFlags.Instance);
                    //Delegate d = Delegate.CreateDelegate(tDelegate, uut1Obj, miHandler);
                    //MethodInfo addHandler = infoAvailEvent.GetAddMethod();
                    //object[] addHandlerArgs = { d };
                    //addHandler.Invoke(uut1Obj, addHandlerArgs);

                    ////Start the test
                    //MethodInfo startTest = classInstanceType.GetMethod("TestLongRunningMethod");
                    //await Task.Run(() => startTest.Invoke(uut1Obj, new object[0]));

                }
                else
                    PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Not Detected");                
            }
            else
                PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nFixture 1 not connected");      
        }

        private async void btnStrTst2_Click(object sender, EventArgs e)
        {
            //clear the txtbox
            TxtBxCtrl(fix2Designator);//clear the text box of old data
            //check for available gpio device
            if (gpioObj[uut2_index].ScanForDevs(fix2Designator))
            {
                PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nFixture " + fix2Designator.ToString() + " connected to " + gpioObj[uut2_index].GetDeviceId());

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut2_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nLid Down Detected");
                    //if limit switch is activated, get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate UUT in object array:
                    //  PSM_uutObj[uut1_index] = new PSM_85307();
                    //  SAM_uutObj[uut1_index] = new SAM_55207();
                    //  PCM_uutObj[uut1_index] = new PCM_90707();
                    //  AIM_uutObj[uut1_index] = new AIM_90807();
                    //  LUM_uutObj[uut1_index] = new LUM_15607();
                }
                else
                    PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nLid Down Not Detected");                
            }
            else
                PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nFixture 2 not connected");
        }

        /// <summary>
        /// Waits for Limit switch to activate.  When called by a task, Method runs continually if no UUT installed
        /// </summary>
        private void OnTestComplete(object source, Dictionary<String, int> passFailtstSts, int fixPos)
        {
            //Unsubscribe from events
            //
            if (fixPos == fix1Designator)
            {                
                //Convert the dictionary to a string to output to text box
                List<String> dictToString = new List<string>();
                foreach (var pair in passFailtstSts)
                {
                    dictToString.Add(pair.Key);
                    dictToString.Add(pair.Value.ToString());
                }
                PrintDataToTxtBox(fixPos, dictToString, "Testing Complete");
                EndofTestRoutine(fixPos, true);
                    
            }
            else if (fixPos == fix2Designator)
            {
                //Convert the dictionary to a string to output to text box
                List<String> dictToString = new List<string>();
                foreach (var pair in passFailtstSts)
                {
                    dictToString.Add(pair.Key);
                    dictToString.Add(pair.Value.ToString());
                }
                PrintDataToTxtBox(fixPos, dictToString, "Testing Complete");
            }
            else
            {
                //incorrect fixture number parameter sent to method
                MessageBox.Show("Incorrect fixture number parameter sent to 'OnTestComplete()' method: " + fixPos.ToString());
            }
        }

        private void OnInformationAvailable(object source, List<String> testStatusInfo, int fixPos)
        {
            //Unsubscribe from events
            //
            if ((fixPos == fix1Designator) || (fixPos == fix2Designator))
            {
                PrintDataToTxtBox(fixPos, testStatusInfo);
            }
            else
            {
                MessageBox.Show("Incorrect fixture number parameter sent to 'OnInformationAvailable()' method: " + fixPos.ToString());
            }
        }

        delegate void GrpBx_ThreadCtrl(int fixPos, int passFailClear);
        public void GrpBxCtrl(int fixPos, int passFailClear)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (grpBxTst1.InvokeRequired || grpBxTst1.InvokeRequired)
            {
                GrpBx_ThreadCtrl btnDel = new GrpBx_ThreadCtrl(GrpBxCtrl);
                this.Invoke(btnDel, new object[] { fixPos, passFailClear });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (passFailClear == 1)
                        grpBxTst1.BackColor = System.Drawing.Color.Green;
                    else if (passFailClear == 0)
                        grpBxTst1.BackColor = System.Drawing.Color.Red;
                    else
                        grpBxTst1.BackColor = System.Drawing.Color.White;
                }
                else if (fixPos == fix2Designator)
                {
                    if (passFailClear == 1)
                        grpBxTst2.BackColor = System.Drawing.Color.Green;
                    else if (passFailClear == 0)
                        grpBxTst2.BackColor = System.Drawing.Color.Red;
                    else
                        grpBxTst2.BackColor = System.Drawing.Color.White;
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'GrpBxCtrl()' method: " + fixPos.ToString());
            }
        }

 

        delegate void Btn_ThreadCtrl(int fixPos, bool en);
        public void BtnCtrl(int fixPos, bool en)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (btnStrTst1.InvokeRequired || btnStrTst2.InvokeRequired)
            {
                Btn_ThreadCtrl btnDel = new Btn_ThreadCtrl(BtnCtrl);
                this.Invoke(btnDel, new object[] { fixPos, en });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (en)
                        btnStrTst1.Enabled = true;
                    else
                        btnStrTst1.Enabled = false;
                }
                else if (fixPos == fix2Designator)
                {
                    if (en)
                        btnStrTst2.Enabled = true;
                    else
                        btnStrTst2.Enabled = false;
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'BtnCtrl()' method: " + fixPos.ToString());
            }
        }

        delegate void TxtBx_ThreadCtrl(int fixPos, bool appendTxt, String txt, bool clr);
        public void TxtBxCtrl(int fixPos, bool appendTxt = false, String txt = null, bool clr = true)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (txtBxTst1.InvokeRequired || txtBxTst2.InvokeRequired)
            {
                TxtBx_ThreadCtrl d = new TxtBx_ThreadCtrl(TxtBxCtrl);
                this.Invoke(d, new object[] { fixPos, appendTxt, txt, clr });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (appendTxt)
                        txtBxTst1.AppendText(txt);
                    if (clr)
                        txtBxTst1.Clear();
                    txtBxTst1.Refresh();
                }
                else if (fixPos == fix2Designator)
                {
                    if (appendTxt)
                        txtBxTst2.AppendText(txt);
                    if (clr)
                        txtBxTst2.Clear();
                    txtBxTst2.Refresh();
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'TxtBxCtrl()' method: " + fixPos.ToString());
            }
        }

        public void EndofTestRoutine(int fixPos, bool allTestsPass)
        {
            BtnCtrl(fixPos, true);
            if (allTestsPass)
            {
                GrpBxCtrl(fixPos, 1);
            }
            else
            {
                GrpBxCtrl(fixPos, 0);
            }
        }
            
    }

}
