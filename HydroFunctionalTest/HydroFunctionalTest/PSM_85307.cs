﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;

namespace HydroFunctionalTest
{
    class PSM_85307
    {
        //Notes:
        //  Power supply class - must turn on/off correct port to fixture depending on the fixture position
        //  ELoad class - must connect equipment (enable relays) to fixture depending on the fixture position
        //  DMM class - must connect equipment (enable relays) to fixture depending on the fixture position
        //        

        #region Structures unique to this assembly
        /// <summary>
        /// Contains all the parameters needed to test the 5 adj auxillary outputs @28v.
        /// Includes the measurement tolerances and CAN frame commands
        /// </summary>
        private struct OutputTestParams
        {
            /// <summary>
            /// Measurement Limits for ELoad. Key = output name, value[] = High limit(amps), Low limit(amps), High limit(volts), Low limit(volts)
            /// </summary>
            public Dictionary<string, double[]> eLoadMeasLimits;
            /// <summary>
            /// Measurement Limits for PCBA. Key = output name, value[] = High limit(amps), Low limit(amps), High limit(volts), Low limit(volts)
            /// </summary>
            public Dictionary<string, double[]> pcbaMeasLimits;
            /// <summary>
            /// Create 4 enum values for indexing measurement limit dictionary elements: iHigh=0, iLow=1, vHigh=2, vLow=3
            /// </summary>
            public enum measLimitIndex : Int32
            {
                iHigh = 0, iLow = 1, vHigh = 2, vLow = 3
            };
            /// <summary>
            /// CAN Commands for adjustable output voltage settings.  Key = output name, value[] = CAN frame1, CAN frame2
            /// </summary>
            public Dictionary<string, string[]> canSetVoltCmd;         
        }
        #endregion Structures unique to this assembly

        #region Common Data Types required for all assemblies
        //Hardware & other Class objects instantiated in main UI that need to be passed to this class
        /// <summary>
        /// Provides control to the GPIO adapter in the fixture.  Initializes when the main UI passes the object to this class contructor
        /// </summary>
        UsbToGpio gpioObj;
        /// <summary>
        /// Provides control to the PCAN adapter in the base station.  Initializes when the main UI passes the object to this class contructor
        /// </summary>
        Pcan pCanObj;
        /// <summary>
        /// Saves pass fail info about overall test and subtests.  Key --> Name of test, Value = passed --> 1 or failed --> 0 or incomplete --> -1:
        /// This data is passed to the main UI upon the OnTestComplete event
        /// </summary>
        private Dictionary<String, int> passFailStatus;
        /// <summary>
        /// Stores the fixture position.  This must be done in the constructor when the object is created in the main UI
        /// </summary>
        private int fixPosition;
        /// <summary>
        /// "OK" should return when successfully sending a test record to the database
        /// </summary>
        private const string expectServerResponse = "OK";
        /// <summary>
        /// Initialize in this class constructor
        /// </summary>
        private string uutSerialNum;
        /// <summary>
        /// Store test data here that contains measurement and/or pass/fail data.  Will be sent to a file with specific formatting per the customer requirements
        /// </summary>
        public List<String> testDataCSV = new List<String>();
        /// <summary>
        /// Store test progress here to send back to the main UI to update the user of status
        /// </summary>
        public List<String> testStatusInfo = new List<String>();
        #endregion Common Data Types required for all assemblies

        #region Data types uniqe to this assembly only
        /// <summary>
        /// CAN commands to enable outputs.  Key = output name, Value[] = CAN frame1, CAN frame2
        /// </summary>
        public Dictionary<string, string[]> enOutput;
        /// <summary>
        /// CAN commands to disable outputs.  Key = output name, Value[] = CAN frame1, CAN frame2
        /// </summary>
        public Dictionary<string, string[]> nEnOutput;
        /// <summary>
        /// Byte to send to GPIO adapter that controls the mux.  Key = output name, value = byte value representing pins to set/clear
        /// </summary>
        public Dictionary<string, int> muxCtrlByte;
        /// <summary>
        /// UUT digital output High limit.
        /// </summary>
        public double uutDigOut_H;
        /// <summary>
        /// UUT digital output Low limit.
        /// </summary>
        public double uutDigOut_L;
        /// <summary>
        /// All Aux output off High Limits (Voltage & Current)
        /// </summary>
        public double auxOutOff_H;
        /// <summary>
        /// All Aux outputs off Low Limit (Voltage & Current)
        /// </summary>
        public double auxOutOff_L;
        #endregion Data types uniqe to this assembly only

        #region Contructor/Destructor
        /// <summary>
        /// Contructor.  Initializes program members.  Could optionally set these values using an external file.
        /// Hardware Objects must be sent from Main UI for this class to control and get status
        /// </summary>
        /// <param name="tmpGpio"></param>
        /// <param name="tmpPcan"></param>
        public PSM_85307(int tmpPos, string serNum, UsbToGpio tmpGpio, Pcan tmpPcan)
        {
            fixPosition = tmpPos;
            pCanObj = tmpPcan;
            gpioObj = tmpGpio;
            uutSerialNum = serNum;

            auxOutOff_H = .01;    //Current(amp) & Voltage(volt) High limit
            auxOutOff_L = -.01;   //Current(amp) & Voltage(volt) Low limit 
            uutDigOut_H = 3;
            uutDigOut_L = 2;

            //initialize structs containing information specific to 28v, 12v & 5v adjustable output voltage tests
            OutputTestParams auxOut28vTst = new OutputTestParams();
            OutputTestParams auxOut12vTst = new OutputTestParams();
            OutputTestParams auxOut5vTst = new OutputTestParams();

            #region Initialize variables holding UUT current & voltage limits
            //Measurement Limits for ELoad
            auxOut28vTst.eLoadMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
                { "Aux0", new double[] { .938, .898, 26.3, 25.2 } }, 
                { "Aux1", new double[] { .938, .898, 26.3, 25.2 } },
                { "Aux2", new double[] { .938, .898, 26.3, 25.2 } },
                { "Aux3", new double[] { .938, .898, 26.3, 25.2 } },
                { "Aux4", new double[] { .938, .898, 26.3, 25.2 } },
                { "Aux5", new double[] { 1.070, 1.030, 27.99, 26.86 } },
                { "28vSW", new double[] { .705, .670, 28.1, 27.0 } },
                { "5vaSW", new double[] { .035, .029, 5.29, 4.85 } },
            };
            auxOut12vTst.eLoadMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
                { "Aux0", new double[] { .436, .406, 12.22, 11.6 } },
                { "Aux1", new double[] { .436, .406, 12.22, 11.6 } },
                { "Aux2", new double[] { .436, .406, 12.22, 11.6 } },
                { "Aux3", new double[] { .436, .406, 12.22, 11.6 } },
                { "Aux4", new double[] { .436, .406, 12.22, 11.6 } },
            };
            auxOut5vTst.eLoadMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
                { "Aux0", new double[] { .191, .15, 5.35, 4.38 } },
                { "Aux1", new double[] { .191, .15, 5.35, 4.38 } },
                { "Aux2", new double[] { .191, .15, 5.35, 4.38 } },
                { "Aux3", new double[] { .191, .15, 5.35, 4.38 } },
                { "Aux4", new double[] { .191, .15, 5.35, 4.38 } },
            };

            //Measurement Limits for PCBA
            auxOut28vTst.pcbaMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(mA), 2nd Element-->Low limit(mA), 3rd Element-->High limit(mV), 4th Element-->Low limit(mV)
                { "Aux0", new double[] { 1018, 818, 27000, 25000 } }, 
                { "Aux1", new double[] { 1138, 718, 27000, 25000 } },
                { "Aux2", new double[] { 1018, 818, 27000, 25000 } },
                { "Aux3", new double[] { 1018, 818, 27000, 25000 } },
                { "Aux4", new double[] { 1018, 818, 27000, 25000 } },
                { "Aux5", new double[] { 1150, 1000, 28100, 27000 } },
            };
            auxOut12vTst.pcbaMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(mA), 2nd Element-->Low limit(mA), 3rd Element-->High limit(mV), 4th Element-->Low limit(mV)
                { "Aux0", new double[] { 501, 341, 1300, 1100 } },
                { "Aux1", new double[] { 524, 324, 1300, 1100 } },
                { "Aux2", new double[] { 501, 341, 1300, 1100 } },
                { "Aux3", new double[] { 501, 341, 1300, 1100 } },
                { "Aux4", new double[] { 501, 341, 1300, 1100 } },
            };
            auxOut5vTst.pcbaMeasLimits = new Dictionary<string, double[]>
            {
                //1st Element-->High limit(mA), 2nd Element-->Low limit(mA), 3rd Element-->High limit(mV), 4th Element-->Low limit(mV)
                { "Aux0", new double[] { 224, 124, 4400, 5500 } },
                { "Aux1", new double[] { 254, 94, 4400, 5500 } },
                { "Aux2", new double[] { 224, 124, 4400, 5500 } },
                { "Aux3", new double[] { 224, 124, 4400, 5500 } },
                { "Aux4", new double[] { 224, 124, 4400, 5500 } },
            };
            #endregion Initialize variables holding UUT current & voltage limits

            #region Initialize variables holding CAN data Frames for UUT output control
            //CAN frame data description:
            //1st Frame-->(CAN Message ID):(USB Message Length):(USB Message ID);(Source node ID);(Dest. Node ID);(Message length);(Enumeration);(arg 1-->Output ON=9 or OFF=8);(arg2-->output net);0
            //Followed by 2nd frame-->(CAN message ID):(USB Message Length):(arg3);0
            //
            enOutput = new Dictionary<string, string[]>
            {
                { "Aux0", new string[] { "413144064:8:74;129;0;5;0;9;2;0", "413143040:2:0;0" } },
                { "Aux1", new string[] { "413144064:8:74;129;0;5;0;9;3;0", "413143040:2:0;0" } },
                { "Aux2", new string[] { "413144064:8:74;129;0;5;0;9;4;0", "413143040:2:0;0" } },
                { "Aux3", new string[] { "413144064:8:74;129;0;5;0;9;5;0", "413143040:2:0;0" } },
                { "Aux4", new string[] { "413144064:8:74;129;0;5;0;9;6;0", "413143040:2:0;0" } },
                { "Aux5", new string[] { "413144064:8:74;129;0;5;0;9;7;0", "413143040:2:0;0" } },
                { "28vSW", new string[] { "413144064:8:74;129;0;5;0;9;16;0", "413143040:2:0;0" } },
                { "5vaSW", new string[] { "413144064:8:74;129;0;5;0;9;17;0", "413143040:2:0;0" } },
                { "digOut0", new string[] { "413144064:8:74;129;0;5;0;9;15;0", "413143040:2:0;0" } },
                { "digOut1", new string[] { "413144064:8:74;129;0;5;0;9;14;0", "413143040:2:0;0" } },
                { "digOut2", new string[] { "413144064:8:74;129;0;5;0;9;13;0", "413143040:2:0;0" } },
                { "digOut3", new string[] { "413144064:8:74;129;0;5;0;9;12;0", "413143040:2:0;0" } },
                { "digOut4", new string[] { "413144064:8:74;129;0;5;0;9;11;0", "413143040:2:0;0" } },
                { "digOut5", new string[] { "413144064:8:74;129;0;5;0;9;10;0", "413143040:2:0;0" } },
                { "digOut6", new string[] { "413144064:8:74;129;0;5;0;9;9;0", "413143040:2:0;0" } },
                { "digOut7", new string[] { "413144064:8:74;129;0;5;0;9;8;0", "413143040:2:0;0" } },
            };
            nEnOutput = new Dictionary<string, string[]>
            {
                { "Aux0", new string[] { "413144064:8:74;129;0;5;0;8;2;0", "413143040:2:0;0" } },
                { "Aux1", new string[] { "413144064:8:74;129;0;5;0;8;3;0", "413143040:2:0;0" } },
                { "Aux2", new string[] { "413144064:8:74;129;0;5;0;8;4;0", "413143040:2:0;0" } },
                { "Aux3", new string[] { "413144064:8:74;129;0;5;0;8;5;0", "413143040:2:0;0" } },
                { "Aux4", new string[] { "413144064:8:74;129;0;5;0;8;6;0", "413143040:2:0;0" } },
                { "Aux5", new string[] { "413144064:8:74;129;0;5;0;8;7;0", "413143040:2:0;0" } },
                { "28vSW", new string[] { "413144064:8:74;129;0;5;0;8;16;0", "413143040:2:0;0" } },
                { "5vaSW", new string[] { "413144064:8:74;129;0;5;0;8;17;0", "413143040:2:0;0" } },
                { "digOut0", new string[] { "413144064:8:74;129;0;5;0;8;15;0", "413143040:2:0;0" } },
                { "digOut1", new string[] { "413144064:8:74;129;0;5;0;8;14;0", "413143040:2:0;0" } },
                { "digOut2", new string[] { "413144064:8:74;129;0;5;0;8;13;0", "413143040:2:0;0" } },
                { "digOut3", new string[] { "413144064:8:74;129;0;5;0;8;12;0", "413143040:2:0;0" } },
                { "digOut4", new string[] { "413144064:8:74;129;0;5;0;8;11;0", "413143040:2:0;0" } },
                { "digOut5", new string[] { "413144064:8:74;129;0;5;0;8;10;0", "413143040:2:0;0" } },
                { "digOut6", new string[] { "413144064:8:74;129;0;5;0;8;9;0", "413143040:2:0;0" } },
                { "digOut7", new string[] { "413144064:8:74;129;0;5;0;8;8;0", "413143040:2:0;0" } },
            };
            auxOut28vTst.canSetVoltCmd = new Dictionary<string, string[]>
            {
                { "Aux0", new string[] { "413144064:8:74;129;0;5;0;22;2;0", "413143040:2:28;0" } },
                { "Aux1", new string[] { "413144064:8:74;129;0;5;0;22;3;0", "413143040:2:28;0" } },
                { "Aux2", new string[] { "413144064:8:74;129;0;5;0;22;4;0", "413143040:2:28;0" } },
                { "Aux3", new string[] { "413144064:8:74;129;0;5;0;22;5;0", "413143040:2:28;0" } },
                { "Aux4", new string[] { "413144064:8:74;129;0;5;0;22;6;0", "413143040:2:28;0" } },
            };
            auxOut12vTst.canSetVoltCmd = new Dictionary<string, string[]>
            {
                { "Aux0", new string[] { "413144064:8:74;129;0;5;0;22;2;0", "413143040:2:12;0" } },
                { "Aux1", new string[] { "413144064:8:74;129;0;5;0;22;3;0", "413143040:2:12;0" } },
                { "Aux2", new string[] { "413144064:8:74;129;0;5;0;22;4;0", "413143040:2:12;0" } },
                { "Aux3", new string[] { "413144064:8:74;129;0;5;0;22;5;0", "413143040:2:12;0" } },
                { "Aux4", new string[] { "413144064:8:74;129;0;5;0;22;6;0", "413143040:2:12;0" } },
            };
            auxOut5vTst.canSetVoltCmd = new Dictionary<string, string[]>
            {
                { "Aux0", new string[] { "413144064:8:74;129;0;5;0;22;2;0", "413143040:2:5;0" } },
                { "Aux1", new string[] { "413144064:8:74;129;0;5;0;22;3;0", "413143040:2:5;0" } },
                { "Aux2", new string[] { "413144064:8:74;129;0;5;0;22;4;0", "413143040:2:5;0" } },
                { "Aux3", new string[] { "413144064:8:74;129;0;5;0;22;5;0", "413143040:2:5;0" } },
                { "Aux4", new string[] { "413144064:8:74;129;0;5;0;22;6;0", "413143040:2:5;0" } },
            };
            #endregion Initialize variables holding CAN data Frames for UUT output control

            #region Initialize variables holding mux control bytes
            muxCtrlByte = new Dictionary<string, int>
            {
                //the int values are actually byte representation of GPIO pin set/clear values depending on which relay controls the UUT output
                { "Aux0", 1 },
                { "Aux1", 2 },
                { "Aux2", 3 },
                { "Aux3", 4 },
                { "Aux4", 5 },
                { "Aux5", 6 },
                { "28vSW", 7 },
                { "5vaSW", 8 },
                { "digOut0", 9 },
                { "digOut1", 10 },
                { "digOut2", 11 },
                { "digOut3", 12 },
                { "digOut4", 13 },
                { "digOut5", 14 },
                { "digOut6", 15 },
                { "digOut7", 16 },
            };
            #endregion Initialize variables holding mux control bytes

            #region Initialize Dictionary containing pass/fail status for all tests
            passFailStatus = new Dictionary<string, int>
            {
                { "All Tests", -1 },
                { "Power On", -1 },
                { "Power On Current", -1 },
                { "USB Communication Check", -1 },
                { "CAN Communication Check", -1 },
            };
            #endregion Initialize Dictionary containing all test pass fail status
        }
        #endregion Constructor/Destructor

        #region Event Publishers and Handlers required for all assemblies
        /// <summary>
        /// Delegates which the UI subscribes to and can be used to update the GUI when information for the user is available/needed
        /// Delegates determine the method signature (method return type & arguments) that subscribers will need to use in their methods
        /// </summary>
        /// <param name="source"></param>
        /// <param name="tstStsInfo"></param>
        /// <param name="fxPos"></param>
        public delegate void InformationAvailableEventHandler(object source, List<String> tstStsInfo, int fxPos);
        /// <summary>
        /// Create the actual event key word (pointer) that subscribers will use when subscribing to the event
        /// </summary>
        public event InformationAvailableEventHandler InformationAvailable;
        /// <summary>
        /// Event handler method - sends test status infor and fixture possition to the subscriber
        /// </summary>
        protected virtual void OnInformationAvailable()
        {
            if (InformationAvailable != null)
                InformationAvailable(this, this.testStatusInfo, this.fixPosition);
        }

        /// <summary>
        /// Delegates which the UI subscribes to and can be used to update the GUI when tests are complete
        /// Delegates determine the method signature (method return type & arguments) that subscribers will need to use in their methods
        /// </summary>
        /// <param name="source"></param>
        /// <param name="passFailtstSts"></param>
        /// <param name="fxPos"></param>
        /// <param name="allTestsPass"></param>
        public delegate void TestCompleteEventHandler(object source, List<String> passFailtstSts, int fxPos, bool allTestsPass);
        /// <summary>
        /// Create the actual event key word (pointer) that subscribers will use when subscribing to the event
        /// </summary>
        public event TestCompleteEventHandler TestComplete;
        /// <summary>
        /// Event handler method - Informs the subscriber that all tasks are complete and the object can be destroyed
        /// Sends the subscriber pass/fail data, fixture position and pass/fail boolean
        /// </summary>
        protected virtual void OnTestComplete()
        {
            //convert the passFailStatus dictionary to a string before sending to main UI
            List<String> tmpList = new List<string>();
            bool tmpAllTestsPass = true;
            foreach (var pair in this.passFailStatus)
            {
                if (pair.Value > 0)
                    tmpList.Add(pair.Key + "--> Pass");
                else
                {
                    if (pair.Value == 0)
                        tmpList.Add(pair.Key + "--> Fail");
                    else
                        tmpList.Add(pair.Key + "--> Incomplete");
                    tmpAllTestsPass = false;
                }
                //Register the passing or failing uut in Inovar's database for work center transfer validation
                TestResultToDatabase(tmpAllTestsPass);
            }
            if (TestComplete != null)
                TestComplete(this, tmpList, this.fixPosition, tmpAllTestsPass);
        }
        #endregion Event Publishers and Handlers required for all assemblies

        #region Common Methods required for all assemblies
        /// <summary>
        /// Call all test functions and execute all other test requirements.  This is the only function called from the main UI
        /// </summary>
        public void RunTests()
        {
            System.Threading.Thread.Sleep(3000);
            testStatusInfo.Add("asdf");
            OnInformationAvailable();
            System.Threading.Thread.Sleep(3000);
            OnTestComplete();
        }

        /// <summary>
        /// Communicates with database - sends the pass/fail status of UUT when test completes
        /// </summary>
        /// <param name="uutPassedAll"></param>
        private void TestResultToDatabase(bool uutPassedAll)
        {
            WebRequest request = WebRequest.Create("http://api.theino.net/custTest.asmx/testSaveWithWorkCenter?" + "serial=" + uutSerialNum + "&testResult=" + uutPassedAll.ToString() + "&failMode=" + "" + "&workcenter=Functional+Test");
            WebResponse response = request.GetResponse();
            String tmpServResponseStr = ((HttpWebResponse)response).StatusDescription.ToString();
            response.Close();
            if (tmpServResponseStr == expectServerResponse)
                testStatusInfo.Add("Successfully sent pass/fail status to database.\r\n"  + "Server Response: '" + tmpServResponseStr);            
            else
                testStatusInfo.Add("Response from server does not match expected string.\r\nExpected response: '" +  expectServerResponse + "'\r\nActual Server Response '" + tmpServResponseStr + "'");
            OnInformationAvailable();
        }
        #endregion Common Methods required for all assemblies

        #region Methods unique to this assembly only
        //Write methods here
        #endregion Methods unique to this assembly only
    }


}
