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
using System.IO.Ports;
using System.Diagnostics;

namespace HydroFunctionalTest
{
    public partial class Form1 : Form
    {
        #region Hardware Class objects
        /// <summary>
        /// 2 obj. instances for Read/Write from/to GPIO adapter.  No attempt to scan or connect to the gpio adapters 
        /// is done until a UUT test begins.  This is because the adapter is inside the bed of nails fixture and we
        /// have no knowledge of the adapter until a fixture is connected and the operator begins a test.
        /// When a scan for adapters is called, each instance obj. in the array is sent to the UUT object created at test time, depending on the fixture position
        /// </summary>
        UsbToGpio[] gpioObj = new UsbToGpio[2];
        /// <summary>
        /// 2 obj. instances for Receive/Transmit from/to PCAN adapter.  Initialization of the 2 adapters is done in the 'SetupHardware' method
        /// Must call "ScanForDev" to "Activate" a device and must call "DeactivateDevice" when the program closes 'Form1_FormClosing'
        /// </summary>
        Pcan[] pCanObj = new Pcan[2];
        /// <summary>
        /// 
        /// </summary>
        #endregion Hardware Class objects

        /// <summary>
        /// For indexing through the 2-D arrays that are associated with UUT1, specifically the pCanObj & gpioObj arrays
        /// </summary>
        const int uut1_index = 0;
        /// <summary>
        /// For indexing through the 2-D arrays that are associated with UUT2, specifically the pCanObj & gpioObj arrays
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
        /// Will be set to true upon successful connection to the DMM serial port.  Initially set to false when the program opens (Form1_Load)
        /// </summary>
        public bool foundDmm;
        /// <summary>
        /// Will be set to true upon successful connection to the ELoad serial port.  Initially set to false when the program opens (Form1_Load)
        /// </summary>
        public bool foundEload;
        /// <summary>
        /// Will be set to true upon successful connection to the power supply serial port.  Initially set to false when the program opens (Form1_Load)
        /// </summary>
        public bool foundPwrSup;
        /// <summary>
        /// Will be set to true upon successful connection to the PCAN device with device ID = 1.  Initially set to false when the program opens (Form1_Load)
        /// </summary>
        public bool foundPcanDev1 = false;
        /// <summary>
        /// Will be set to true upon successful connection to the PCAN device with device ID = 2.  Initially set to false when the program opens (Form1_Load)
        /// </summary>
        public bool foundPcanDev2 = false;
        public bool fix1TestInProgress = false;
        public bool fix2TestInProgress = false;

        private const String testPrgmPath = "C:\\CraneFunctionalTest\\";
        private const String progSourceFiles = testPrgmPath + "TestProgramSourceFiles\\";


        /// <summary>
        /// Byte value for Fixture IDs but only 4 bits (2-5) are used, all others must be masked (set to zero). Key = assembly name, value = 4 bit value using bits: P1.2, P1.3, P1.4, P2.5
        /// </summary>
        public Dictionary<string, int> fxtIDs = new Dictionary<string, int>
        {
            { "PSM_85307", 60 }, //1111 (P1.2, P1.3, P1.4, P2.5)
            { "SAM_55207", 56 }, //1110 (P1.2, P1.3, P1.4, P2.5)
            { "PCM_90707", 52 }, //1101 (P1.2, P1.3, P1.4, P2.5)
            { "AIM_90807", 48 }, //1100 (P1.2, P1.3, P1.4, P2.5)
            { "LUM_15607", 44 }, //1011 (P1.2, P1.3, P1.4, P2.5)
            { "PSM_85307_Gen3", 40 }, //1010 (P1.2, P1.3, P1.4, P2.5)
            { "SAM_55207_Gen3", 36 }, //1001 (P1.2, P1.3, P1.4, P2.5)
            { "PCM_90707_Gen3", 32 }, //1000 (P1.2, P1.3, P1.4, P2.5)
            { "AIM_90807_Gen3", 28 }, //0111 (P1.2, P1.3, P1.4, P2.5)
            { "LUM_15607_Gen3", 24 }, //0110 (P1.2, P1.3, P1.4, P2.5)
        };

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foundDmm = false;
            foundEload = false;
            foundPwrSup = false;
            foundPcanDev1 = false;
            foundPcanDev2 = false;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            SetupHardware();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            eqStsLbl.Text = "Closing Application...";
            eqStsLbl.Refresh();
            DisconnectHardw();
        }

        /// <summary>
        /// Setup the Power supply, DMM, ELoad, CAN adpts.  
        /// Update the GUI with equipment status
        /// </summary>
        /// <returns></returns>
        public bool SetupHardware()
        {
            eqStsLbl.Text = "Initializing Test Equipment...";
            eqStsLbl.ForeColor = System.Drawing.Color.Black;
            eqStsLbl.Refresh();
            //disconnect from any hardware before scanning for attached devices/equipment
            DisconnectHardw();

            //Scan and identify hardware/equipment that use the COM ports
            String[] tmpPortNames = SerialPort.GetPortNames();
            foreach (String s in tmpPortNames)
            {
                //if the comport is associated with device, stop searching 
                bool stopSearch = false;
                if (!stopSearch && !foundPwrSup)
                {
                    System.Threading.Thread.Sleep(250);
                    if (PwrSup.InitializePwrSup(s))
                    {
                        foundPwrSup = true;
                        stopSearch = true;
                        mainStsTxtBx.AppendText("Power Supply attached to: " + s + Environment.NewLine);
                        //turn DUT power outputs off and 5V output on
                        PwrSup.TurnOutputOnOff(1, false, 0, 0);
                        PwrSup.TurnOutputOnOff(2, false, 0, 0);
                        PwrSup.TurnOutputOnOff(3, true, 5, 1.5); //set to 5 volts and maximum current (3A)
                        if (PwrSup.OVP_Check())
                        {
                            mainStsTxtBx.AppendText("Power Supply Over Voltage\r\n");
                            PwrSup.ClearOVP();
                        }
                    }
                }
                if (!stopSearch && !foundDmm)
                {
                    System.Threading.Thread.Sleep(250);
                    if (Dmm.InitializeDmm(s))
                    {
                        foundDmm = true;
                        stopSearch = true;
                        mainStsTxtBx.AppendText("DMM attached to: " + s + Environment.NewLine);
                    }
                }
                if (!stopSearch && !foundEload)
                {
                    System.Threading.Thread.Sleep(250);
                    if (Eload.TalkToLoad(s, progSourceFiles))
                    {
                        foundEload = true;
                        stopSearch = true;
                        mainStsTxtBx.AppendText("Eload attached to: " + s + Environment.NewLine);
                        //turn Eload off
                        Eload.Toggle("off");
                        //Set Eload Max Voltage
                        Eload.SetMaxVoltage(30.0);
                    }
                }
            }
            if (!foundDmm)
                mainStsTxtBx.AppendText("Problem commun. w/ DMM\r\nVerify RS232 settings.\r\n" + Dmm.dmmSettings + "\r\n");
            if (!foundPwrSup)
                mainStsTxtBx.AppendText("Problem commun. w/ Power Supply\r\nVerify RS232 settings.\r\n" + PwrSup.pwrSupSettings + "\r\n");
            if (!foundEload)
                mainStsTxtBx.AppendText("Problem commun. w/ Electronic Load\r\n" + string.Join(" ", Eload.returnData.ToArray()));

            //Instantiate hardware object array elements
            for (int i = 0; i < 2; i++)
            {
                gpioObj[i] = new UsbToGpio();
                pCanObj[i] = new Pcan();
            }

            //Design flaw on the base station which allows only UUT2 GPIO to control connection to Eload and DMM
            //If the UUT2 GPIO adapter is connected, upon power up the device sets all I/O to high imp. causing all inputs to go high
            //this results in UUT2 GPIO setting the Eload and DMM connections to itself and UUT1 has no way of gaining control until
            //The GPIO I/O are set to there default values.
            if (gpioObj[uut1_index].ScanForDevs(fix1Designator))
                SetGpioInitValue(fix1Designator);

            if (gpioObj[uut2_index].ScanForDevs(fix2Designator))
                SetGpioInitValue(fix2Designator);

            //Scan for PCAN Devices (if found, the device has been activated)
            if (pCanObj[uut1_index].ScanForDev(fix1Designator))
            {
                foundPcanDev1 = true;
                foreach (String s in pCanObj[uut1_index].pcanReturnData)
                {
                    mainStsTxtBx.AppendText(s);
                }
                mainStsTxtBx.AppendText("--->PCAN-USB Active for Fixture #1");
            }
            else
            {
                foreach (String s in pCanObj[uut1_index].pcanReturnData)
                {
                    mainStsTxtBx.AppendText(s);
                }
                mainStsTxtBx.AppendText("--->Use the PCAN-USB Adapter Fixture Association Tool to resolve connection error with fixture #1.");
            }
            mainStsTxtBx.AppendText(Environment.NewLine);

            if (pCanObj[uut2_index].ScanForDev(fix2Designator))
            {
                foundPcanDev2 = true;
                foreach (String s in pCanObj[uut2_index].pcanReturnData)
                {
                    mainStsTxtBx.AppendText(s);
                }
                mainStsTxtBx.AppendText("--->PCAN-USB Active for Fixture #2");
            }
            else
            {
                foreach (String s in pCanObj[uut2_index].pcanReturnData)
                {
                    mainStsTxtBx.AppendText(s);
                }
                mainStsTxtBx.AppendText("--->Use the PCAN-USB Adapter Fixture Association Tool to resolve connection error with fixture #2.");
            }


            if (foundPwrSup & foundEload & foundDmm & foundPcanDev1 & foundPcanDev2)
            {
                eqStsLbl.Text = "Equipment Initialization Successful";
                eqStsLbl.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                eqStsLbl.Text = "Equipment Initialization Not Complete (see equip. status info in tools tab)\r\nOne or both fixtures may not operate without resolving.";
                eqStsLbl.ForeColor = System.Drawing.Color.Red;
            }
            return (foundPwrSup & foundEload & foundDmm & foundPcanDev1 & foundPcanDev2);
        }

        public void DisconnectHardw()
        {
            if (foundPcanDev1)
                pCanObj[uut1_index].DeactivateDevice();
            if (foundPcanDev2)
                pCanObj[uut2_index].DeactivateDevice();
            if (foundDmm)
                Dmm.CloseComport();
            if (foundPwrSup)
            {
                //turn all power outputs off
                PwrSup.TurnOutputOnOff(1, false, 0, 0);
                PwrSup.TurnOutputOnOff(2, false, 0, 0);
                PwrSup.TurnOutputOnOff(3, false, 0, 0);
                if (PwrSup.OVP_Check())
                    if (PwrSup.OVP_Check())
                        PwrSup.ClearOVP();
                PwrSup.CloseComport();
            }
            if (foundEload)
            {
                Eload.Toggle("off");
            }
            foundDmm = false;
            foundPwrSup = false;
            foundEload = false;
            foundPcanDev1 = false;
            foundPcanDev2 = false;
        }

        public void PrintDataToTxtBox(int fixPos, List<string> dataToPrint, string optString = "", bool clearTxtBox = false)
        {
            if (dataToPrint == null)
                dataToPrint = new List<string>();
            dataToPrint.Add(optString);

            TxtBxThreadCtrl(fixPos, true, dataToPrint, false);
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
                    "2)Click OK",
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

        private void btnStrTst1_Click(object sender, EventArgs e)
        {
            //clear the txtbox
            TxtBxThreadCtrl(fix1Designator);//clear the text box of old data
            //reset background color, any integer other than 0 or 1 will work
            GrpBxThreadCtrl(fix1Designator, -2);//make background neutral to start test

            if ((txtBxSerNum1.Text != "Enter 10 Digit Serial") && (txtBxSerNum1.Text.Length >= 10))
            {

                //check to be sure all necessary test equipment is active and begin checking for available GPIO devices

                bool foundGpio = gpioObj[uut1_index].ScanForDevs(fix1Designator);
                if (foundGpio & foundDmm & foundPwrSup & foundEload & foundPcanDev1)
                {
                    SetGpioInitValue(fix1Designator);
                    PrintDataToTxtBox(fix1Designator, null, "Fixture " + fix1Designator.ToString() + " connected to GPIO --> " + gpioObj[uut1_index].GetDeviceId());

                    //clear the PCAN adapter data buffers
                    pCanObj[uut1_index].ClearDataBuffers();

                    //check to see limit switch is activated
                    UInt32 limitSw = gpioObj[uut1_index].GpioRead(1, 1);
                    if (limitSw == 0)
                    {
                        GrpBxThreadCtrl(fix1Designator, -1);
                        PrintDataToTxtBox(fix1Designator, null, "\r\nLid Down Detected");
                        //begin testing UUT
                        fix1TestInProgress = true;
                        BeginTest(fix1Designator, txtBxSerNum1.Text, this.btnAbort1, this.btnStrTst1);
                        fix1TestInProgress = false;
                    }
                    else
                        PrintDataToTxtBox(fix1Designator, null, "Lid Down Not Detected");
                }
                else
                {
                    if (!foundGpio)
                        PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "Problem communicating with GPIO adapter");
                    else
                        PrintDataToTxtBox(fix1Designator, null, "Test Equipment initialization needs to be resolved before beginning test\r\nSee equipment status info in tools tab");
                }
            }
            else
            {
                txtBxSerNum1.Text = "Enter 10 Digit Serial";
                txtBxSerNum1.SelectAll();
                txtBxSerNum1.Focus();
            }
        }

        private void btnStrTst2_Click(object sender, EventArgs e)
        {
            //clear the txtbox
            TxtBxThreadCtrl(fix2Designator);//clear the text box of old data
            //reset background color, any integer other than 0 or 1 will work
            GrpBxThreadCtrl(fix2Designator, -2);//make background neutral to start test

            if ((txtBxSerNum2.Text != "Enter 10 Digit Serial") && (txtBxSerNum2.Text.Length >= 10))
            {
                //check to be sure all necessary test equipment is active and begin checking for available GPIO devices
                bool foundGpio = gpioObj[uut2_index].ScanForDevs(fix2Designator);
                if (foundGpio & foundDmm & foundPwrSup & foundEload & foundPcanDev2)
                {
                    SetGpioInitValue(fix2Designator);
                    PrintDataToTxtBox(fix2Designator, null, "Fixture " + fix2Designator.ToString() + "connected to GPIO --> " + gpioObj[uut2_index].GetDeviceId());

                    //clear the PCAN adapter data buffers
                    pCanObj[uut2_index].ClearDataBuffers();

                    //check to see limit switch is activated
                    UInt32 limitSw = gpioObj[uut2_index].GpioRead(1, 1);
                    if (limitSw == 0)
                    {
                        GrpBxThreadCtrl(fix2Designator, -1);
                        PrintDataToTxtBox(fix2Designator, null, "\r\nLid Down Detected");
                        //begin testing UUT
                        fix2TestInProgress = true;
                        BeginTest(fix2Designator, txtBxSerNum2.Text, this.btnAbort2, this.btnStrTst2);
                        fix2TestInProgress = false;
                    }
                    else
                        PrintDataToTxtBox(fix2Designator, null, "Lid Down Not Detected");
                }
                else
                {
                    if (!foundGpio)
                        PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "Problem communicating with GPIO adapter");
                    else
                        PrintDataToTxtBox(fix2Designator, null, "Test Equipment initialization needs to be resolved before beginning test\r\nSee equipment status info in tools tab");
                }
            }
            else
            {
                txtBxSerNum2.Text = "Enter 10 Digit Serial";
                txtBxSerNum2.SelectAll();
                txtBxSerNum2.Focus();
            }
        }

        private async void BeginTest(int fixPos, String tempSerialNum, System.Windows.Forms.Button softwAbortEvent, System.Windows.Forms.Button beginTestButton)
        {
            //Get the fixture ID (which asssembly is being tested)
            Byte tempFixID = FixtureID(fixPos);//returns 0 if error occurs  
            //check operator input options:
            bool tempSkipBootloader = false;
            bool tempSkipFirmware = false;
            bool tempIsRma = false;
            if (chkBxSkpBoot1.Checked | chkBxSkpBoot2.Checked) tempSkipBootloader = true;
            if (chkBxSkpFirm1.Checked | chkBxSkpFirm2.Checked) tempSkipFirmware = true;
            if (chkBxRma1.Checked | chkBxRma2.Checked) tempIsRma = true;
            bool tmpFoundFixture = false;
            foreach (var pair in fxtIDs)
            {
                //Depending on fixture ID, instantiate specific UUT in object array:
                if ((tempFixID == pair.Value))
                {
                    tmpFoundFixture = true;
                    //lock down the GUI so no other input can be received other than to cancel the task
                    BtnThreadCtrl(fixPos, false);
                    SerialBxThreadCtrl(fixPos, false);
                    cboBxDbgTstThreadCtrl(fixPos, true);
                    if (fxtIDs.ContainsKey("PSM_85307"))
                    {
                        //initialize the uut object
                        PSM_85307 uutObj = new PSM_85307(fixPos, tempSerialNum, gpioObj[fixPos-1], pCanObj[fixPos-1], tempSkipBootloader, tempSkipFirmware, tempIsRma);
                        //subscribe to uut events
                        uutObj.InformationAvailable += OnInformationAvailable;
                        uutObj.TestComplete += OnTestComplete;
                        uutObj.StartButtonText_Change += OnStartButtonText_Change;
                        //check to see if opertator has opted to run specific test
                        if ((chkBxTstSelectThreadCtrl(fixPos)) && (uutObj.testRoutineInformation.ContainsKey(cboBxDbgTstThreadCtrl(fixPos, false))))
                        {
                            //find the specific test the operator has selected to run
                            //if the test is found, unselect the other tests
                            String[] testRoutines = uutObj.testRoutineInformation.Keys.ToArray();
                            foreach (string s in testRoutines)
                            {
                                if (cboBxDbgTstThreadCtrl(fixPos, false) != s)
                                {
                                    //unselect the test by assigning a value of -2 (0 = failure, -1 = untested, anything greater than 1 is a pass, anything less than -1 is skipped.
                                    uutObj.testRoutineInformation[s] = -2;
                                }
                            }
                        }
                        //execute uut test(s)
                        await Task.Run(() => uutObj.RunTests(softwAbortEvent));
                        //unsubscribe from uut events
                        uutObj.InformationAvailable -= OnInformationAvailable;
                        uutObj.TestComplete -= OnTestComplete;
                        uutObj.StartButtonText_Change -= OnStartButtonText_Change;
                        //jump out of foreach loop
                        break;
                    }
                    else if (fxtIDs.ContainsKey("SAM_55207"))
                    {
                        SAM_55207 uutObj = new SAM_55207(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //subscribe to uut events
                        uutObj.InformationAvailable += OnInformationAvailable;
                        uutObj.TestComplete += OnTestComplete;
                        //uutObj.StartButtonText_Change += OnStartButtonText_Change;
                        //execute uut tests
                        await Task.Run(() => uutObj.RunTests(softwAbortEvent));
                        //unsubscribe from uut events
                        uutObj.InformationAvailable -= OnInformationAvailable;
                        uutObj.TestComplete -= OnTestComplete;
                        //uutObj.StartButtonText_Change -= OnStartButtonText_Change;
                        //jump out of foreach loop
                        break;
                    }
                    //else if (fxtIDs.ContainsKey("PCM_90707"))
                    //{
                    //    PCM_90707 uutObj = new PCM_90707(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("AIM_90807"))
                    //{
                    //    AIM_90807 uutObj = new AIM_90807(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("LUM_15607"))
                    //{
                    //    LUM_15607 uutObj = new LUM_15607(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("PSM_85307_Gen3"))
                    //{
                    //    PSM_85307_Gen3 uutObj = new PSM_85307_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("SAM_55207_Gen3"))
                    //{
                    //    SAM_55207_Gen3 uutObj = new SAM_55207_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("PCM_90707_Gen3"))
                    //{
                    //    PCM_90707_Gen3 uutObj = new PCM_90707_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("AIM_90807_Gen3"))
                    //{
                    //    AIM_90807_Gen3 uutObj = new AIM_90807_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    //else if (fxtIDs.ContainsKey("LUM_15607_Gen3"))
                    //{
                    //    LUM_15607_Gen3 uutObj = new LUM_15607_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                    //    //subscribe to uut events
                    //    uutObj.InformationAvailable += OnInformationAvailable;
                    //    uutObj.TestComplete += OnTestComplete;
                    //    //execute uut tests
                    //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                    //    //unsubscribe from uut events
                    //    uutObj.InformationAvailable -= OnInformationAvailable;
                    //    uutObj.TestComplete -= OnTestComplete;
                    //    //jump out of foreach loop
                    //    break;
                    //}
                    else
                        MessageBox.Show("Error in program.  Unable to match a fixture string to the dictionary values.");
                }
            }
            if (!tmpFoundFixture)
            {
                PrintDataToTxtBox(fixPos, null, "Unable to detect a fixture\r\nDMM measured: " + tempFixID.ToString() + " Volts.");
                List<String> tmpList = new List<string>();
                foreach (var couple in fxtIDs)
                {
                    tmpList.Add("\r\nExpecting measurements between: " + couple.Value.ToString() + " and " + couple.Value.ToString() + " Volts for the " + couple.Key + " fixture.");
                }
                PrintDataToTxtBox(fixPos, tmpList);
            }
            if (PwrSup.OVP_Check())
            {
                PrintDataToTxtBox(fixPos, null, "Power Supply Over Voltage");
                PwrSup.ClearOVP();
            }
        }

        /// <summary>
        /// Subscriber to the event which should be published in every UUT class
        /// This allows the UUT to update the main UI when the task is complete (test is done)
        /// </summary>
        private void OnTestComplete(object source, List<String> passFailtstSts, int fixPos, bool allTestsPass)
        {
            //Unsubscribe from events and check that task is complete, kill if still running
            //
            //unlock the GUI so no other input can be received
            BtnThreadCtrl(fixPos, true);
            SerialBxThreadCtrl(fixPos, true);
            EndofTestRoutine(fixPos, allTestsPass);
            PrintDataToTxtBox(fixPos, null, "\r\n*********Test Results*********");
            PrintDataToTxtBox(fixPos, passFailtstSts);
            TxtBxSerInputThreadCtrl(fixPos);
            AbortBtnThreadCtrl(fixPos, "Abort Test");

            if (PwrSup.OVP_Check())
                if (PwrSup.OVP_Check())
                {
                    PrintDataToTxtBox(fixPos, null, "Power Supply Over Voltage");
                    PwrSup.ClearOVP();
                }
        }

        /// <summary>
        /// Subscriber to the event which should be published in every UUT class
        /// This allows the UUT to update the main UI with necessary information
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testStatusInfo"></param>
        /// <param name="fixPos"></param>
        private void OnInformationAvailable(object source, List<String> testStatusInfo, int fixPos)
        {
            if ((fixPos == fix1Designator) || (fixPos == fix2Designator))
            {
                PrintDataToTxtBox(fixPos, testStatusInfo);
            }
            else
            {
                MessageBox.Show("Incorrect fixture number parameter sent to 'OnInformationAvailable()' method: " + fixPos.ToString());
            }
        }

        private void OnStartButtonText_Change(object source, string buttonText, int fixPos)
        {
            if ((fixPos == fix1Designator) || (fixPos == fix2Designator))
            {
                BtnThreadCtrl(fixPos, false, buttonText);
            }
            else
            {
                MessageBox.Show("Incorrect fixture number parameter sent to 'OnStartButtonText_Change()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="passFailClear"></param>
        delegate void grpBx_ThreadCtrl(int fixPos, int passFailClear);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="passFailClear"></param>
        public void GrpBxThreadCtrl(int fixPos, int passFailClear)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (grpBxTst1.InvokeRequired || grpBxTst1.InvokeRequired)
            {
                grpBx_ThreadCtrl btnDel = new grpBx_ThreadCtrl(GrpBxThreadCtrl);
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
                    else if (passFailClear == -1)
                        grpBxTst1.BackColor = System.Drawing.Color.Yellow;
                    else
                        grpBxTst1.BackColor = System.Drawing.Color.White;
                }
                else if (fixPos == fix2Designator)
                {
                    if (passFailClear == 1)
                        grpBxTst2.BackColor = System.Drawing.Color.Green;
                    else if (passFailClear == 0)
                        grpBxTst2.BackColor = System.Drawing.Color.Red;
                    else if (passFailClear == -1)
                        grpBxTst2.BackColor = System.Drawing.Color.Yellow;
                    else
                        grpBxTst2.BackColor = System.Drawing.Color.White;
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'GrpBxThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        delegate void btn_ThreadCtrl(int fixPos, bool en, String txtToDisplay);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        public void BtnThreadCtrl(int fixPos, bool en, String txtToDisplay = "Begin Test")
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (btnStrTst1.InvokeRequired || btnStrTst2.InvokeRequired)
            {
                btn_ThreadCtrl btnDel = new btn_ThreadCtrl(BtnThreadCtrl);
                this.Invoke(btnDel, new object[] { fixPos, en, txtToDisplay});
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (en)
                    {
                        btnStrTst1.Enabled = true;
                        btnStrTst1.Text = txtToDisplay;
                    }
                    else
                    {
                        btnStrTst1.Enabled = false;
                        btnStrTst1.Text = txtToDisplay;
                    }
                }
                else if (fixPos == fix2Designator)
                {
                    if (en)
                    {
                        btnStrTst2.Enabled = true;
                        btnStrTst2.Text = txtToDisplay;
                    }
                    else
                    {
                        btnStrTst2.Enabled = false;
                        btnStrTst2.Text = txtToDisplay;
                    }
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'BtnThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        delegate void abortBtn_ThreadCtrl(int fixPos, String buttonText);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        public void AbortBtnThreadCtrl(int fixPos, String buttonText)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (btnAbort1.InvokeRequired || btnAbort2.InvokeRequired)
            {
                abortBtn_ThreadCtrl btnDel = new abortBtn_ThreadCtrl(AbortBtnThreadCtrl);
                this.Invoke(btnDel, new object[] { fixPos, buttonText });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    btnAbort1.Text = buttonText;
                    btnAbort1.Refresh();
                }
                else if (fixPos == fix2Designator)
                {
                    btnAbort2.Text = buttonText;
                    btnAbort2.Refresh();
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'AbortBtnThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        delegate void serialBx_ThreadCtrl(int fixPos, bool en);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        public void SerialBxThreadCtrl(int fixPos, bool en)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (txtBxSerNum1.InvokeRequired || txtBxSerNum2.InvokeRequired)
            {
                serialBx_ThreadCtrl txtBxDel = new serialBx_ThreadCtrl(SerialBxThreadCtrl);
                this.Invoke(txtBxDel, new object[] { fixPos, en });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (en)
                        txtBxSerNum1.Enabled = true;
                    else
                        txtBxSerNum1.Enabled = false;
                }
                else if (fixPos == fix2Designator)
                {
                    if (en)
                        txtBxSerNum2.Enabled = true;
                    else
                        txtBxSerNum2.Enabled = false;
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'SerialBxThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="appendTxt"></param>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        delegate void txtBx_ThreadCtrl(int fixPos, bool appendTxt, List<String> txtList, bool clr);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="appendTxt"></param>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        public void TxtBxThreadCtrl(int fixPos, bool appendTxt = false, List<String> txtList = null, bool clr = true)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (txtBxTst1.InvokeRequired || txtBxTst2.InvokeRequired)
            {
                txtBx_ThreadCtrl d = new txtBx_ThreadCtrl(TxtBxThreadCtrl);
                this.Invoke(d, new object[] { fixPos, appendTxt, txtList, clr });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    if (appendTxt)
                    {
                        foreach (String s in txtList)
                        {
                            txtBxTst1.AppendText(s);
                        }
                        txtBxTst1.AppendText(Environment.NewLine);
                    }
                    if (clr)
                        txtBxTst1.Clear();
                    txtBxTst1.Refresh();
                }
                else if (fixPos == fix2Designator)
                {
                    if (appendTxt)
                    {
                        foreach (String s in txtList)
                        {
                            txtBxTst2.AppendText(s);
                        }
                        txtBxTst2.AppendText(Environment.NewLine);
                    }
                    if (clr)
                        txtBxTst2.Clear();
                    txtBxTst2.Refresh();
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'TxtBxThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="appendTxt"></param>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        delegate void txtBxSerInput_ThreadCtrl(int fixPos);
        public void TxtBxSerInputThreadCtrl(int fixPos)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (txtBxSerNum1.InvokeRequired || txtBxSerNum2.InvokeRequired)
            {
                txtBxSerInput_ThreadCtrl d = new txtBxSerInput_ThreadCtrl(TxtBxSerInputThreadCtrl);
                this.Invoke(d, new object[] { fixPos });
            }
            else
            {
                if (fixPos == fix1Designator)
                {
                    txtBxSerNum1.Text = "Enter 10 Digit Serial";
                    txtBxSerNum1.SelectAll();
                    txtBxSerNum1.Focus();
                }
                else if (fixPos == fix2Designator)
                {
                    txtBxSerNum2.Text = "Enter 10 Digit Serial";
                    txtBxSerNum2.SelectAll();
                    txtBxSerNum2.Focus();
                }
                else
                    MessageBox.Show("Incorrect fixture number parameter sent to 'TxtBxSerInput()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="appendTxt"></param>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        delegate String cboBxDbgTst_ThreadCtrl(int fixPos, bool disable);
        public String cboBxDbgTstThreadCtrl(int fixPos, bool disable)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (cboBxDbgTst1.InvokeRequired || cboBxDbgTst2.InvokeRequired)
            {
                cboBxDbgTst_ThreadCtrl d = new cboBxDbgTst_ThreadCtrl(cboBxDbgTstThreadCtrl);
                this.Invoke(d, new object[] { fixPos, disable });
            }
            else
            {
                if (disable)
                {
                    if (fixPos == fix1Designator)
                    {
                        cboBxDbgTst1.Enabled = false;
                        return cboBxDbgTst1.Text;
                    }
                    else
                    {
                        cboBxDbgTst2.Enabled = false;
                        return cboBxDbgTst2.Text;
                    }
                }
                else
                {
                    if (fixPos == fix1Designator)
                    {
                        cboBxDbgTst1.Enabled = true;
                        return cboBxDbgTst1.Text;
                    }
                    else
                    {
                        cboBxDbgTst2.Enabled = true;
                        return cboBxDbgTst2.Text;
                    }
                    }
            }
            return "";
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        delegate bool chkBxTstSelect_ThreadCtrl(int fixPos);
        public bool chkBxTstSelectThreadCtrl(int fixPos)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (chkBxTstSelect1.InvokeRequired || chkBxTstSelect2.InvokeRequired)
            {
                chkBxTstSelect_ThreadCtrl d = new chkBxTstSelect_ThreadCtrl(chkBxTstSelectThreadCtrl);
                this.Invoke(d, new object[] { fixPos });
            }
            else
            {
                if (fixPos == fix1Designator)
                    return chkBxTstSelect1.Checked;
                else
                    return chkBxTstSelect2.Checked;
            }
            return false;
        }

        /// <summary>
        /// Method for performing any common tasks or end of test cleanup work needed before the task ends and object is destroyed.
        /// e.g., enable GUI controls or otherwise setup the UI for user input
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="allTestsPass"></param>
        public void EndofTestRoutine(int fixPos, bool allTestsPass)
        {
            BtnThreadCtrl(fixPos, true);
            cboBxDbgTstThreadCtrl(fixPos, false);
            if (allTestsPass)
            {
                GrpBxThreadCtrl(fixPos, 1);
            }
            else
            {
                GrpBxThreadCtrl(fixPos, 0);
            }
        }

        private void btnAbort1_Click(object sender, EventArgs e)
        {
            if (btnStrTst1.Enabled)
                AbortBtnThreadCtrl(fix1Designator, "Abort Test");
            else
                AbortBtnThreadCtrl(fix1Designator, "Please Wait...");
        }

        private void btnAbort2_Click(object sender, EventArgs e)
        {
            if (btnStrTst2.Enabled)
                AbortBtnThreadCtrl(fix2Designator, "Abort Test");
            else
                AbortBtnThreadCtrl(fix2Designator, "Please Wait...");
        }

        private void rfshEquipBtn_Click(object sender, EventArgs e)
        {
            mainStsTxtBx.Clear();
            DisconnectHardw();
            SetupHardware();
        }

        private void SetGpioInitValue(int fixPos)
        {
            if (fixPos == 1)
            {
                //set all GPIO ports initially low except Port 1 which are all digital inputs
                gpioObj[uut1_index].GpioWrite(0, 0);
                gpioObj[uut1_index].GpioWrite(2, 5);  //Port 2 bits all low except for P2.0 & P2.2 connected to the MUX control lines which are active low.  P2.1(latch) should stay low
                //latch the input (rising edge of pin 11 (CP) of flip flops) P2.3 & P2.4 & P2.5
                gpioObj[uut1_index].GpioWrite(2, 61); //Set bits P2.3, P2.4 & P2.5 high, keep P2.0, P2.1 & P2.2 high and keep P2.1, P2.6 & P2.7 low 
                gpioObj[uut1_index].GpioWrite(2, 5);  //Return Port 2 to initial state
            }
            else if (fixPos == 2)
            {
                //set all GPIO ports initially low except Port 1 which are all digital inputs
                gpioObj[uut2_index].GpioWrite(0, 0);
                gpioObj[uut2_index].GpioWrite(2, 5);  //Port 2 bits all low except for P2.0 & P2.2 connected to the MUX control lines which are active low.  P2.1(latch) should stay low
                //latch the input (rising edge of pin 11 (CP) of flip flops) P2.3 & P2.4 & P2.5
                gpioObj[uut2_index].GpioWrite(2, 61); //Set bits P2.3, P2.4 & P2.5 high, keep P2.0 & P2.2 high and keep P2.1, P2.6 & P2.7 low 
                gpioObj[uut2_index].GpioWrite(2, 5);  //Return Port 2 to initial state
            }
            else
            {
                MessageBox.Show("Incorrect fixture number parameter sent to 'SetGpioInitValue()' method in main UI: " + fixPos.ToString());
            }
        }

        private Byte FixtureID(int fixPos)
        {
            Byte rtnData = 0;
            if (fixPos == 1)
            {
                //read the value of port 1 and mask the don't care bits (P1.0, P1.1, P1.6 & P1.7)
                UInt32 tmpRead = gpioObj[uut1_index].GpioRead(1);
                rtnData = (Byte)(60 & tmpRead);
            }
            else if (fixPos == 2)
            {
                //read the value of port 1 and mask the don't care bits (P1.0, P1.1, P1.6 & P1.7)
                UInt32 tmpRead = gpioObj[uut2_index].GpioRead(1);
                rtnData = (Byte)(60 & tmpRead);
            }
            else
            {
                MessageBox.Show("Incorrect fixture number parameter sent to 'FixtureID()' method: " + fixPos.ToString());
            }
            return rtnData;
        }

        private void txtBxSerNum1_MouseClick(object sender, MouseEventArgs e)
        {
            txtBxSerNum1.SelectAll();
        }

        private void txtBxSerNum2_MouseClick(object sender, MouseEventArgs e)
        {
            txtBxSerNum2.SelectAll();
        }

        private void cboBxDbgTst1_Click(object sender, EventArgs e)
        {
            cboBxDbgTst1.Items.Clear();
            PopulateDropDownBox(fix1Designator, txtBxSerNum1.Text);
        }

        private void cboBxDbgTst2_Click(object sender, EventArgs e)
        {
            cboBxDbgTst2.Items.Clear();
            PopulateDropDownBox(fix2Designator, txtBxSerNum2.Text);
        }

        private void PopulateDropDownBox(int fixPos, String tempSerialNum)
        {
            //check for available GPIO devices
            bool foundGpio = gpioObj[fixPos - 1].ScanForDevs(fixPos);
            //check operator input options:
            bool skipBootloader = false;
            bool skipFirmware = false;
            bool isRma = false;
            if (chkBxSkpBoot1.Checked | chkBxSkpBoot2.Checked) skipBootloader = true;
            if (chkBxSkpFirm1.Checked | chkBxSkpFirm2.Checked) skipFirmware = true;
            if (chkBxRma1.Checked | chkBxRma2.Checked) isRma = true;
            if (foundGpio)
            {
                SetGpioInitValue(fixPos);
                PrintDataToTxtBox(fixPos, null, "Fixture " + fixPos.ToString() + " connected to GPIO --> " + gpioObj[fixPos-1].GetDeviceId());
                Byte tempFixID = 60; // FixtureID(fixPos);//returns 0 if error occurs                    
                bool tmpFoundFixture = false;
                foreach (var pair in fxtIDs)
                {
                    //Depending on fixture ID, instantiate specific UUT in object array:
                    if ((tempFixID == pair.Value))
                    {
                        tmpFoundFixture = true;
                        if (fxtIDs.ContainsKey("PSM_85307"))
                        {
                            //initialize the uut object

                            PSM_85307 uutObj = new PSM_85307(fixPos, tempSerialNum, gpioObj[fixPos-1], pCanObj[fixPos-1], skipBootloader, skipFirmware, isRma);
                            foreach (var test in uutObj.testRoutineInformation)
                            {
                                if(fixPos == 1) cboBxDbgTst1.Items.Add(test.Key);
                                else cboBxDbgTst2.Items.Add(test.Key);
                            }
                            break;
                        }
                        else if (fxtIDs.ContainsKey("SAM_55207"))
                        {
                            SAM_55207 uutObj = new SAM_55207(fixPos, tempSerialNum, gpioObj[fixPos-1], pCanObj[fixPos-1]);
                            //foreach (var test in uutObj.testRoutineInformation)
                            //{
                            //    if(fixPos == 1) cboBxDbgTst1.Items.Add(test.Key);
                            //    else cboBxDbgTst2.Items.Add(test.Key);
                            //}
                            //break;
                        }
                        //else if (fxtIDs.ContainsKey("PCM_90707"))
                        //{
                        //    PCM_90707 uutObj = new PCM_90707(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("AIM_90807"))
                        //{
                        //    AIM_90807 uutObj = new AIM_90807(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("LUM_15607"))
                        //{
                        //    LUM_15607 uutObj = new LUM_15607(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("PSM_85307_Gen3"))
                        //{
                        //    PSM_85307_Gen3 uutObj = new PSM_85307_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("SAM_55207_Gen3"))
                        //{
                        //    SAM_55207_Gen3 uutObj = new SAM_55207_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("PCM_90707_Gen3"))
                        //{
                        //    PCM_90707_Gen3 uutObj = new PCM_90707_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("AIM_90807_Gen3"))
                        //{
                        //    AIM_90807_Gen3 uutObj = new AIM_90807_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        //else if (fxtIDs.ContainsKey("LUM_15607_Gen3"))
                        //{
                        //    LUM_15607_Gen3 uutObj = new LUM_15607_Gen3(fixPos, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                        //foreach (var test in uutObj.testDataCSV)
                        //{
                        //    cboBxDbgTst1.Items.Add(test);
                        //}
                        //}
                        else
                        {
                            MessageBox.Show("Error in program.  Unable to match a fixture string to the dictionary values.");
                        }
                    }
                }
                if (!tmpFoundFixture)
                {
                    PrintDataToTxtBox(fixPos, null, "Unable to detect a fixture\r\nDMM measured: " + tempFixID.ToString() + " Volts.");
                    List<String> tmpList = new List<string>();
                    foreach (var couple in fxtIDs)
                    {
                        tmpList.Add("\r\nExpecting measurements between: " + couple.Value.ToString() + " and " + couple.Value.ToString() + " Volts for the " + couple.Key + " fixture.");
                    }
                    PrintDataToTxtBox(fixPos, tmpList);
                }
            }
            else
            {
                PrintDataToTxtBox(fixPos, gpioObj[fixPos-1].gpioReturnData, "Problem communicating with GPIO adapter");
            }
        }
    }

}
