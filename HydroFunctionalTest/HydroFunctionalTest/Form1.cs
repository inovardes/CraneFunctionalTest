﻿using System;
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
        //Eload eLoadObj = new Eload;
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
        /// Measurement Limits for Fixture ID voltage divider circuit. Key = assembly name, value[] = High limit(volts), Low limit(volts)
        /// </summary>
        public Dictionary<string, double[]> fxtIDs = new Dictionary<string, double[]>
        {
            { "PSM_85307", new double[] { 5.2, 4.8 } },
            { "SAM_55207", new double[] { 4.2, 3.8 } },
            { "PCM_90707", new double[] { 3.2, 2.8 } },
            { "AIM_90807", new double[] { 2.2, 3.8 } },
            { "LUM_15607", new double[] { 1.2, .8 } },
            { "PSM_85307_Gen3", new double[] { 4.7, 4.3 } },
            { "SAM_55207_Gen3", new double[] { 3.7, 3.3 } },
            { "PCM_90707_Gen3", new double[] { 2.7, 2.3 } },
            { "AIM_90807_Gen3", new double[] { 1.7, 1.3 } },
            { "LUM_15607_Gen3", new double[] { .7, .3 } },
        };

        #region Flip Flop control commands
        /// <summary>
        /// Port/Pin info to send to GPIO adapter to set/clear pins.   Key = relay control net name per schematic, value[] = I/O port, I/O pin, control port, control pin
        /// pass these Dictionary values along with disable/enable command as method parameters
        /// </summary>
        Dictionary<string, int[]> flipFlopCtrlCmd = new Dictionary<string, int[]>
            {
                { "FIXTURE_ID_EN", new int[] { 0, 4, 2, 5 } },
            };
        #endregion Flip Flop control commands


        public Form1()
        {            
            InitializeComponent();            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foundDmm = false;
            foundEload = true;
            foundPwrSup = false;
            if (!SetupHardware())
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SetupHardware())
                        break;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //DeactivateDevice PCAN devices
            pCanObj[uut1_index].DeactivateDevice();
            pCanObj[uut2_index].DeactivateDevice();
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
            //Instantiate hardware object array elements
            for (int i = 0; i < 2; i++)
            {
                gpioObj[i] = new UsbToGpio();
                pCanObj[i] = new Pcan();
            }
                        
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

                }
            }
            if (!foundDmm)
                mainStsTxtBx.AppendText("Problem commun. w/ DMM\r\nVerify RS232 settings.\r\n" + Dmm.dmmSettings + "\r\n");
            if (!foundPwrSup)
                mainStsTxtBx.AppendText("Problem commun. w/ Power Supply\r\nVerify RS232 settings.\r\n" + PwrSup.pwrSupSettings + "\r\n");
            if (!foundEload)
                mainStsTxtBx.AppendText("Problem commun. w/ Electronic Load\r\n");

            if (foundPwrSup & foundEload & foundDmm)
            {
                eqStsLbl.Text = "Equipment Initialization Successful";
                eqStsLbl.ForeColor = System.Drawing.Color.Green;
            }                
            else
            {
                eqStsLbl.Text = "Equipment Initialization Failed";
                eqStsLbl.ForeColor = System.Drawing.Color.Red;
            }
            return (foundPwrSup & foundEload & foundDmm);
        }

        public void DisconnectHardw()
        {
            if (foundDmm)
                Dmm.CloseComport();
            if (foundPwrSup)
                PwrSup.CloseComport();
            if (foundEload)
            {

            }
            foundDmm = false;
            foundPwrSup = false;
            foundEload = true;
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
            TxtBxThreadCtrl(fix1Designator);//clear the text box of old data
            //reset background color, any integer other than 0 or 1 will work
            GrpBxThreadCtrl(fix1Designator, -1);//make background neutral to start test
            //check for available gpio device
            if (gpioObj[uut1_index].ScanForDevs(fix1Designator))
            {
                PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, " (Fixture " +
                    fix1Designator.ToString() + " connected to " + gpioObj[uut1_index].GetDeviceId() + ")");

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut1_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nLid Down Detected");
                    //Get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate specific UUT in object array:
                    double dmmMeasuredValue = 5; //read value from dmm
                    bool tmpFoundFixture = false;
                    foreach (var pair in fxtIDs)
                    {
                        if ((dmmMeasuredValue < pair.Value[0]) && (dmmMeasuredValue > pair.Value[1]))
                        {
                            tmpFoundFixture = true;
                            //lock down the GUI so no other input can be received other than to cancel the task
                            BtnThreadCtrl(fix1Designator, false);
                            SerialBxThreadCtrl(fix1Designator, false);
                            if (fxtIDs.ContainsKey("PSM_85307"))
                            {
                                //initialize the uut object
                                PSM_85307 uutObj = new PSM_85307(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                                //subscribe to uut events
                                uutObj.InformationAvailable += OnInformationAvailable;
                                uutObj.TestComplete += OnTestComplete;
                                //execute uut tests
                                await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                                //unsubscribe from uut events
                                uutObj.InformationAvailable -= OnInformationAvailable;
                                uutObj.TestComplete -= OnTestComplete;
                            }
                            else if (fxtIDs.ContainsKey("SAM_55207"))
                            {
                                SAM_55207 uutObj = new SAM_55207(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                                //subscribe to uut events
                                uutObj.InformationAvailable += OnInformationAvailable;
                                uutObj.TestComplete += OnTestComplete;
                                //execute uut tests
                                await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                                //unsubscribe from uut events
                                uutObj.InformationAvailable -= OnInformationAvailable;
                                uutObj.TestComplete -= OnTestComplete;
                            }
                            //else if (fxtIDs.ContainsKey("PCM_90707"))
                            //{
                            //    PCM_90707 uutObj = new PCM_90707(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("AIM_90807"))
                            //{
                            //    AIM_90807 uutObj = new AIM_90807(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("LUM_15607"))
                            //{
                            //    LUM_15607 uutObj = new LUM_15607(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("PSM_85307_Gen3"))
                            //{
                            //    PSM_85307_Gen3 uutObj = new PSM_85307_Gen3(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("SAM_55207_Gen3"))
                            //{
                            //    SAM_55207_Gen3 uutObj = new SAM_55207_Gen3(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //}
                            //else if (fxtIDs.ContainsKey("PCM_90707_Gen3"))
                            //{
                            //    PCM_90707_Gen3 uutObj = new PCM_90707_Gen3(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("AIM_90807_Gen3"))
                            //{
                            //    AIM_90807_Gen3 uutObj = new AIM_90807_Gen3(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("LUM_15607_Gen3"))
                            //{
                            //    LUM_15607_Gen3 uutObj = new LUM_15607_Gen3(fix1Designator, txtBxSerNum1.Text, gpioObj[uut1_index], pCanObj[uut1_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort1));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            else
                            {
                                MessageBox.Show("Error in program.  Unable to match a fixture string to the dictionary values.");
                            }
                        }                      
                    }
                    if (!tmpFoundFixture)
                    {
                        PrintDataToTxtBox(fix1Designator, null, "Unable to detect a fixture\r\nDMM measured: " + dmmMeasuredValue.ToString() + " Volts.");
                        List<String> tmpList = new List<string>();
                        foreach (var couple in fxtIDs)
                        {
                            tmpList.Add("Expecting measurements between: " + couple.Value[0].ToString() + " and " + couple.Value[1].ToString() + " Volts for the " + couple.Key + " fixture.");
                        }
                        PrintDataToTxtBox(fix1Designator, tmpList);
                    }
                }
                else
                    PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "Lid Down Not Detected");                
            }
            else
            {                
                PrintDataToTxtBox(fix1Designator, gpioObj[uut1_index].gpioReturnData, "\r\nFixture " + fix1Designator.ToString() + " not connected");
            }
        }

        private async void btnStrTst2_Click(object sender, EventArgs e)
        {
            //clear the txtbox
            TxtBxThreadCtrl(fix2Designator);//clear the text box of old data
            //reset background color, any integer other than 0 or 1 will work
            GrpBxThreadCtrl(fix2Designator, -1);//make background neutral to start test
            //check for available gpio device
            if (gpioObj[uut2_index].ScanForDevs(fix2Designator))
            {
                PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, " (Fixture " + fix2Designator.ToString() + " connected to " + gpioObj[uut2_index].GetDeviceId() + ")");

                //check to see limit switch is activated
                UInt32 limitSw = gpioObj[uut2_index].GpioRead(1, 1);
                if (limitSw == 1)
                {
                    PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nLid Down Detected");
                    //Get the fixture ID (which asssembly is being tested)
                    //Depending on fixture ID, instantiate specific UUT in object array:
                    double dmmMeasuredValue = 5; //read value from dmm


                    //Type classInstanceType = Type.GetType(fxtIDs.Keys.First());
                    bool tmpFoundFixture = false;
                    foreach (var pair in fxtIDs)
                    {
                        if ((dmmMeasuredValue < pair.Value[0]) && (dmmMeasuredValue > pair.Value[1]))
                        {
                            tmpFoundFixture = true;
                            //lock down the GUI so no other input can be received other than to cancel the task
                            BtnThreadCtrl(fix2Designator, false);
                            SerialBxThreadCtrl(fix2Designator, false);                            
                            if (fxtIDs.ContainsKey("PSM_85307"))
                            {
                                PSM_85307 uutObj = new PSM_85307(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                                //subscribe to uut events
                                uutObj.InformationAvailable += OnInformationAvailable;
                                uutObj.TestComplete += OnTestComplete;
                                //execute uut tests
                                await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                                //unsubscribe from uut events
                                uutObj.InformationAvailable -= OnInformationAvailable;
                                uutObj.TestComplete -= OnTestComplete;
                            }
                            else if (fxtIDs.ContainsKey("SAM_55207"))
                            {
                                SAM_55207 uutObj = new SAM_55207(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                                //subscribe to uut events
                                uutObj.InformationAvailable += OnInformationAvailable;
                                uutObj.TestComplete += OnTestComplete;
                                //execute uut tests
                                await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                                //unsubscribe from uut events
                                uutObj.InformationAvailable -= OnInformationAvailable;
                                uutObj.TestComplete -= OnTestComplete;
                            }
                            //else if (fxtIDs.ContainsKey("PCM_90707"))
                            //{
                            //    PCM_90707 uutObj = new PCM_90707(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("AIM_90807"))
                            //{
                            //    AIM_90807 uutObj = new AIM_90807(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("LUM_15607"))
                            //{
                            //    LUM_15607 uutObj = new LUM_15607(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("PSM_85307_Gen3"))
                            //{
                            //    PSM_85307_Gen3 uutObj = new PSM_85307_Gen3(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("SAM_55207_Gen3"))
                            //{
                            //    SAM_55207_Gen3 uut_Obj = new SAM_55207_Gen3(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("PCM_90707_Gen3"))
                            //{
                            //    PCM_90707_Gen3 uutObj = new PCM_90707_Gen3(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("AIM_90807_Gen3"))
                            //{
                            //    AIM_90807_Gen3 uutObj = new AIM_90807_Gen3(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            //else if (fxtIDs.ContainsKey("LUM_15607_Gen3"))
                            //{
                            //    LUM_15607_Gen3 uutObj = new LUM_15607_Gen3(fix2Designator, txtBxSerNum2.Text, gpioObj[uut2_index], pCanObj[uut2_index]);
                            //    //subscribe to uut events
                            //    uutObj.InformationAvailable += OnInformationAvailable;
                            //    uutObj.TestComplete += OnTestComplete;
                            //    //execute uut tests
                            //    await Task.Run(() => uutObj.RunTests(this.btnAbort2));
                            //    //unsubscribe from uut events
                            //    uutObj.InformationAvailable -= OnInformationAvailable;
                            //    uutObj.TestComplete -= OnTestComplete;
                            //}
                            else
                            {
                                MessageBox.Show("Error in program.  Unable to match a fixture string to the dictionary values.");
                            }
                        }
                    }
                    if (!tmpFoundFixture)
                    {
                        PrintDataToTxtBox(fix2Designator, null, "Unable to detect a fixture\r\nDMM measured: " + dmmMeasuredValue.ToString() + " Volts.");
                        List<String> tmpList = new List<string>();
                        foreach (var couple in fxtIDs)
                        {
                            tmpList.Add("Expecting measurements between: " + couple.Value[0].ToString() + " and " + couple.Value[1].ToString() + " Volts for the " + couple.Key + " fixture.");
                        }
                        PrintDataToTxtBox(fix2Designator, tmpList);
                    }
                }
                else
                    PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "Lid Down Not Detected");
            }
            else
            {
                PrintDataToTxtBox(fix2Designator, gpioObj[uut2_index].gpioReturnData, "\r\nFixture " + fix2Designator.ToString()  + " not connected");
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
            if (fixPos == fix1Designator)
            {
                
                //lock down the GUI so no other input can be received other than to cancel the task
                BtnThreadCtrl(fix1Designator, true);
                SerialBxThreadCtrl(fix1Designator, true);
                EndofTestRoutine(fixPos, allTestsPass);
                PrintDataToTxtBox(fixPos, null, "\r\n*********Test Results*********");                
                PrintDataToTxtBox(fixPos, passFailtstSts);
            }
            else if (fixPos == fix2Designator)
            {
                //lock down the GUI so no other input can be received other than to cancel the task
                BtnThreadCtrl(fix2Designator, true);
                SerialBxThreadCtrl(fix2Designator, true);
                EndofTestRoutine(fixPos, allTestsPass);
                PrintDataToTxtBox(fixPos, null, "\r\n***Test Results***");                
                PrintDataToTxtBox(fixPos, passFailtstSts);
            }
            else
            {
                //incorrect fixture number parameter sent to method
                MessageBox.Show("Incorrect fixture number parameter sent to 'OnTestComplete()' method: " + fixPos.ToString());
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
                    MessageBox.Show("Incorrect fixture number parameter sent to 'GrpBxThreadCtrl()' method: " + fixPos.ToString());
            }
        }

        /// <summary>
        /// Delegate for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        delegate void btn_ThreadCtrl(int fixPos, bool en);
        /// <summary>
        /// Method for controlling and updating Main GUI from threads other than the main form
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="en"></param>
        public void BtnThreadCtrl(int fixPos, bool en)
        {
            //If method caller comes from a thread other than main UI, access the main UI's members using 'Invoke'
            if (btnStrTst1.InvokeRequired || btnStrTst2.InvokeRequired)
            {
                btn_ThreadCtrl btnDel = new btn_ThreadCtrl(BtnThreadCtrl);
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
                    MessageBox.Show("Incorrect fixture number parameter sent to 'BtnThreadCtrl()' method: " + fixPos.ToString());
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
                        foreach(String s in txtList)
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
        /// Method for performing any common tasks or end of test cleanup work needed before the task ends and object is destroyed.
        /// e.g., enable GUI controls or otherwise setup the UI for user input
        /// </summary>
        /// <param name="fixPos"></param>
        /// <param name="allTestsPass"></param>
        public void EndofTestRoutine(int fixPos, bool allTestsPass)
        {
            BtnThreadCtrl(fixPos, true);
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

        }

        private void btnAbort2_Click(object sender, EventArgs e)
        {

        }

        private void rfshEqBtn_Click(object sender, EventArgs e)
        {
            mainStsTxtBx.Clear();
            DisconnectHardw();
            if (!SetupHardware())
            {
                for (int i = 0; i < 3; i++)
                {
                    DisconnectHardw();
                    if (SetupHardware())
                        break;
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        private bool NullCheck(String tmpStr)
        {
            if (tmpStr == null)
                return true;
            else
                return false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (PwrSup.IsBusy)
                MessageBox.Show("PWR SUP busy");
            PwrSup.SetPwrSupVoltLimits(28, 28, 5);
            foreach (String s in PwrSup.pwrSupReturnData)
            {
                txtBxTst1.AppendText(s);
                txtBxTst1.AppendText(Environment.NewLine);
            }
            PwrSup.TurnOutputOnOff(1, true, 1, 5);
            foreach(String s in PwrSup.pwrSupReturnData)
            {
                txtBxTst1.AppendText(s);
                txtBxTst1.AppendText(Environment.NewLine);
            }
            PwrSup.TurnOutputOnOff(1, false, 0, 0);
            foreach (String s in PwrSup.pwrSupReturnData)
            {
                txtBxTst1.AppendText(s);
                txtBxTst1.AppendText(Environment.NewLine);
            }


            //dmm
            //String tmpStr = dmmObj.Measure("meas:volt:dc?");
            //double tmp = double.Parse(dmmObj.Measure("meas:res?"));//*IDN?
            //MessageBox.Show(s);
        }
    }

}
