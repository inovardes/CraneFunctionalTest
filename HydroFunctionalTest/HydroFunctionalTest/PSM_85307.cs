using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Reflection;

namespace HydroFunctionalTest
{
    class PSM_85307
    {
        //Notes:
        //  Power supply class - must turn on/off correct port to fixture depending on the fixture position
        //  ELoad class - must connect equipment (enable relays) to fixture depending on the fixture position
        //  DMM class - must connect equipment (enable relays) to fixture depending on the fixture position
        //  Abort check - check toggle clamp (limit switch, if DGIO 1.1 goes high - abort test) or use DMM to measure power is still connected 
        //  File output - setup the CSV dictionary to be common among all assemblies but the dictionary contents are specific to the assembly.  This makes output universal
        //  LED indicator
        //  Load firmware
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

        //GPIO port and pin names for code readability
        private struct gpioConst
        {
            public const UInt32 port2InitStateByte = 63; //set P2.7 & P2.6 low, everything else goes high
            public const UInt32 port0 = 0;
            public const UInt32 port1 = 1;
            public const UInt32 port2 = 2;
            public const UInt32 bit0 = 1;
            public const UInt32 bit1 = 2;
            public const UInt32 bit2 = 4;
            public const UInt32 bit3 = 8;
            public const UInt32 bit4 = 16;
            public const UInt32 bit5 = 32;
            public const UInt32 bit6 = 64;
            public const UInt32 bit7 = 128;
            public const bool set = true;
            public const bool clear = false;
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
        private Dictionary<String, int> testRoutineInformation;
        /// <summary>
        /// Provides the main UI with control to abort test via the cancel button
        /// Value is changed in the event handler function btnAbort1_Click
        /// Its value can also be changed by any method that needs to indicate to the
        /// 'RunTests' method that the test should be aborted
        /// </summary>
        private bool softwAbort = false;
        /// <summary>
        /// Provides information when one of two hardware abort indicators are activated: 1) Limit switch released, 2) Fixture button is depressed
        /// </summary>
        private bool hardwAbort = false;
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
        /// Store test data here. Key = Test name, value[] = High limit, Low limit, Result
        /// </summary>
        public List<String> testDataCSV = new List<string>();
        private const string testPrgmPath = "C:\\Crane Functional Test\\";
        private const string testDataFilePath = testPrgmPath + "Test Data\\";
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
        /// static variable that holds U2 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public UInt32 u2FlipFlopInputByte;
        /// <summary>
        /// static variable that holds U3 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public UInt32 u3FlipFlopInputByte;
        /// <summary>
        /// static variable that holds U4 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public UInt32 u4FlipFlopInputByte;
        /// <summary>
        /// static variable that holds the GPIO port2 desired output.  Port 2 is the control logic for all flip flops, Mux and connection to common test equipment (Eload & DMM)
        /// </summary>
        public UInt32 port2CtrlByte;
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
            uutDigOut_H = 3;      //Voltage Out High limit for UUT digital outputs
            uutDigOut_L = 2;      //Voltage Out Low limit for UUT digital outputs

            //deactivate all relays/controls, i.e., assert GPIO lines to put relays/controls in passive or non-active state
            GpioInitState();

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
            testRoutineInformation = new Dictionary<string, int>
            {
                //{ "Power On", -1 },
                //{ "Power On Current", -1 },
                //{ "USB Communication Check", -1 },
                //{ "CAN Communication Check", -1 },
                //{ "All Tests", -1 },
                { "PowerUp", -1 },
                { "CurrentLimitCheck", -1 },
                { "FlashBootloader", -1 },
                { "LoadFirmware", -1 },

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
            bool tmpAllTestsPass = false;
            int howManyFailures = testRoutineInformation.Count();
            foreach (var pair in this.testRoutineInformation)
            {
                if (pair.Value > 0)
                {
                    tmpList.Add(pair.Key + "--> Pass");
                    howManyFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                }
                else
                {
                    if (pair.Value == 0)
                        tmpList.Add(pair.Key + "--> Fail");
                    else
                        tmpList.Add(pair.Key + "--> Incomplete");
                }
                tmpList.Add(Environment.NewLine);
            }
            if (howManyFailures <= 0)
                tmpAllTestsPass = true;

            //update the main UI if any abort event occured
            if(softwAbort)
                testStatusInfo.Add("***Software Aborted Test***");
            else if (hardwAbort)
                testStatusInfo.Add("***Operator Aborted Test***");

            //turn on the LED to indicate pass or fail
            SetClearLEDs(false, tmpAllTestsPass);

            //Register the passing or failing uut in Inovar's database for work center transfer validation
            TestResultToDatabase(tmpAllTestsPass);
            //Generate the file containing test results in the testDataCSV list
            OutputTestResultsToFile(tmpAllTestsPass);
            //Follow the abort routine: Power down UUT, command all inputs/outputs to high impedence (via software and hardware commands)

            //event handler checks for any subscribers
            if (TestComplete != null)
                TestComplete(this, tmpList, this.fixPosition, tmpAllTestsPass);
        }
        #endregion Event Publishers and Handlers required for all assemblies

        #region Common Methods required for all assemblies
        /// <summary>
        /// Call all test functions and execute all other test requirements.  This is the only function called from the main UI
        /// The main UI must provide the UUT with the cancel button object to subscribe to the button event.
        /// </summary>
        public void RunTests(System.Windows.Forms.Button softwAbortEvent)
        {
            //subscribe to the main UI abort button event
            softwAbortEvent.Click += new System.EventHandler(btnAbort_Click);
            SetClearLEDs();//turn the pass/fail indictator LEDs off
            bool allTestsDone = false;
            softwAbort = false;
            hardwAbort = false;
            testStatusInfo.Add("\r\n**********Begin Testing " + this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "**********\r\n");
            //loop until all tests have been complete or user aborts test.
            while (!allTestsDone && (!AbortCheck()))
            {
                if (!testRoutineInformation.ContainsValue(-1))
                    allTestsDone = true;//testing completed
                else
                {
                    //get the test names of all tests needing to be executed and invoke them
                    //the keys of the testRoutineInformation match the name of the function
                    List<String> keys = new List<String>(testRoutineInformation.Keys);
                    foreach (String key in keys)
                    {
                        //break out of the loop if user has aborted the test
                        if (AbortCheck())
                            break;
                        if (testRoutineInformation[key] == -1)
                        {                            
                            testStatusInfo.Add(key.ToString() + "..."); //update the GUI with the next test to run
                            OnInformationAvailable();
                            testStatusInfo.Clear();
                            //run the tests (call functions) by invoking them
                            //each test method needs to be listed in the testRoutineInformation List
                            Type thisType = this.GetType();//gets this instance type which then has access to the functions
                            MethodInfo theMethod = thisType.GetMethod(key);
                            //delay before calling next test
                            System.Threading.Thread.Sleep(1000);
                            theMethod.Invoke(this, new object[0]);
                            //Fire the OnInformationAvailable event to update the main UI with test status from the testStatusInfo List
                            //Each function puts test status in the List --> testStatusInfo.Add("test status")
                            OnInformationAvailable();
                            testStatusInfo.Clear();
                            if (testRoutineInformation[key] == -1)
                                testStatusInfo.Add("Resource busy, jumping to next test...\r\n");
                        }
                    }                      
                }
            }
            //unsubscribe from the main UI abort button event
            softwAbortEvent.Click -= new System.EventHandler(btnAbort_Click);            
            testStatusInfo.Add("*****************Testing Ended*****************\r\n");
            //Fire the OnTestComplete event to update the main UI and end the test
            OnInformationAvailable();
            testStatusInfo.Clear();
            OnTestComplete();//calls routines for program cleanup/wrap-up and triggers an event telling subscriber (main UI) that the test is complete
        }

        /// <summary>
        /// Checks the status of the class variable 'softwAbort' as well as checks two different hardware abort triggers: 1) Limit switch not activated, 2) Button on fixture activated.  
        /// This method is checked within a while loop inside the 'RunTests' method.  If AbortCheck returns false, the 'RunTests' method will break out of the loop and end the test
        /// softwAbort can be set by any test method if the test encounters a failure that requires the test to end.
        /// </summary>
        /// <returns></returns>
        private bool AbortCheck()
        {
            bool rtnResult = false;
            //check to see limit switch is activated
            UInt32 limitSw = gpioObj.GpioRead(1, 1);
            //check to see if abort button has been enabled
            String tmpDmmStr = Dmm.Measure("meas:volt:dc?");
            double dmmMeas = 0;//initial value will force failure if DMM measurement fails
            if (tmpDmmStr != null)
            {
                dmmMeas = double.Parse(tmpDmmStr);                
            }
            else
            {
                softwAbort = true;
                testStatusInfo.Add("Unable to get voltage measurement from DMM in the 'AbortCheck' Method\r\nTest Aborted");
            }

            if (softwAbort)
            {                
                rtnResult = true;
            }            
            else if (limitSw == 0)
            {
                testStatusInfo.Add("Lid Down Not Detected\r\nTest Aborted\r\n");
                hardwAbort = rtnResult = true;
            }
            else if (dmmMeas < 26 )//if DMM measures +28V not present, abort test
            {
                testStatusInfo.Add("+28V main power not detected\r\nTest Aborted\r\n");
                hardwAbort = rtnResult = true;
            }
            return rtnResult;
        }

        /// <summary>
        /// Outputs any information held in the testDataCSV List.  This list contains any information needed for outputting to a file in the required customer format
        /// </summary>
        /// <returns></returns>
        private void OutputTestResultsToFile(bool tmpAllTestsPass)
        {
            String dateTime =  (System.DateTime.Now.ToString().Replace("/","_")).Replace(":",".");
            String fileName = this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\\" + uutSerialNum.Remove(5) + 
                "\\" + this.ToString().Substring(this.ToString().IndexOf(".") + 1) + 
                "~SN_" + uutSerialNum.ToString() + "~" + dateTime + "~" + tmpAllTestsPass.ToString() + ".txt";

            //add information about the UUT at the top of the file
            String fileString = "";
            if (softwAbort)
                fileString = "--->\tSoftware Aborted Test\t<---\r\n\r\n";
            else if (hardwAbort)
                fileString = "--->\tOperator Aborted Test\t<---\r\n\r\n";

            //add information about the PCA
            fileString = fileString + "PCBA Part Number: " + this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\r\nPCBA Serial Number: " + uutSerialNum.ToString()
                + "\r\nDate of Test: " + dateTime + "\r\nWork Order: " + uutSerialNum.Remove(5) + Environment.NewLine;            

            //add informatoin about the overall test results
            fileString = fileString + "\r\n****\tTest Results Overview\t****\r\n";
            foreach (var pair in this.testRoutineInformation)
            {
                fileString = fileString + "\t";
                if (pair.Value > 0)
                    fileString = fileString + pair.Key + "--> Pass\r\n";
                    //testDataCSV.Add(pair.Key + "--> Pass");
                else
                {
                    if (pair.Value == 0)
                        fileString = fileString + pair.Key + "--> Fail\r\n";
                        //testDataCSV.Add(pair.Key + "--> Fail");
                    else
                        fileString = fileString + pair.Key + "--> Incomplete\r\n";
                        //testDataCSV.Add(pair.Key + "--> Incomplete");
                }
            }            
            //add a header for the test data to follow
            fileString = fileString + "\r\nTest Description,High Limit,Low Limit,Measurement,Result";
            testDataCSV.Add("asdfasdf");

            try
            {
                System.IO.DirectoryInfo tmp = System.IO.Directory.CreateDirectory(testDataFilePath +
                    this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\\" + uutSerialNum.Remove(5));

                testStatusInfo.Add("Sending test data to txt file:\r\n" + testDataFilePath + fileName);
                OnInformationAvailable();
                testStatusInfo.Clear();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(testDataFilePath + fileName))
                {
                    file.WriteLine(fileString);
                    foreach (var s in testDataCSV)
                    {
                        file.WriteLine(s);
                    }
                }
            }
            catch (Exception ex)
            {
                testStatusInfo.Add("File operation error.\r\n" + ex.Message +  "\r\nFailed to send test data to txt file:\r\n" + testDataFilePath + fileName);
                OnInformationAvailable();
                testStatusInfo.Clear();
            }
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
                testStatusInfo.Add("Successfully sent pass/fail status to database.\r\n"  + "Server Response: '" + tmpServResponseStr + "'");            
            else
                testStatusInfo.Add("Response from server does not match expected string.\r\nExpected response: '" +  expectServerResponse + "'\r\nActual Server Response '" + tmpServResponseStr + "'");
            OnInformationAvailable();
            testStatusInfo.Clear();
        }

        /// <summary>
        /// Event handling function fires when user clicks the GUI cancel button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbort_Click(object sender, EventArgs e)
        {
            softwAbort = true;
        }

        private void FlashBootloader()
        {
            //check to see if resource is busy before enabling the JTAG usb port
            while (ProgrammingControl.JtagIsBusy)
            {
                testStatusInfo.Add("Waiting for other JTAG bootloader process to finish...");
                OnInformationAvailable();
                testStatusInfo.Clear();
                System.Threading.Thread.Sleep(1000);
            }

            //enable JTAG USB port
            u2FlipFlopInputByte = SetBits(u2FlipFlopInputByte, gpioConst.bit2);
            gpioObj.GpioWrite(gpioConst.port0, u2FlipFlopInputByte);
            //latch the flip flop input (rising edge of pin 11 (CP)) P2.3
            gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit3));
            gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit3));
            //the return data should be a dictionary with only one element, the key as a bool and value a string containing any pertinent information
            Dictionary<bool, String> tmpDict = ProgrammingControl.ProgramBootloader(testPrgmPath, this, gpioObj);
            //disable JTAG USB port
            u2FlipFlopInputByte = ClearBits(u2FlipFlopInputByte, gpioConst.bit2);
            gpioObj.GpioWrite(gpioConst.port0, u2FlipFlopInputByte);
            //latch the flip flop input (rising edge of pin 11 (CP)) P2.3
            gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit3));
            gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit3));
            if (tmpDict.Keys.First())
                testRoutineInformation["FlashBootloader"] = 1; // Successfully flashed bootloader
            else
            {
                testRoutineInformation["FlashBootloader"] = 0; // Failed to program bootloader
                //discontinue test by setting softwAbort to true;
                softwAbort = true;
            }
                


        }

        private void LoadFirmware()
        {
            //check to see if resource is busy (optional, probably won't want to wait since no other test will run without first programming)
            //if (!ProgrammingControl.AutoItIsBusy)
            //{

            //}

            //the return data should be a dictionary with only one element, the key as a bool and value a string containing any pertinent information
            Dictionary<bool, String> tmpDict =  ProgrammingControl.LoadFirmwareViaAutoit(testPrgmPath, this);
            if (tmpDict.Keys.First())
                testRoutineInformation["LoadFirmware"] = 1;  //Successfully programmed
            else
            {
                testRoutineInformation["LoadFirmware"] = 0;
                //discontinue test by setting softwAbort to true;
                softwAbort = true;
            }

        }

        private void SetClearLEDs(bool ledsOff = true, bool passedAllTests = false)
        {
            if (ledsOff)
            {
                u4FlipFlopInputByte = SetBits(u4FlipFlopInputByte, 6);
                gpioObj.GpioWrite(gpioConst.port0, u4FlipFlopInputByte);
                //latch the flip flop input (rising edge of pin 11 (CP)) P2.5
                gpioObj.GpioWrite(gpioConst.port2, ClearBits(u4FlipFlopInputByte, gpioConst.bit5));
                gpioObj.GpioWrite(gpioConst.port2, SetBits(u4FlipFlopInputByte, gpioConst.bit5));
            }
            else
            {
                if (passedAllTests)
                {
                    u4FlipFlopInputByte = ClearBits(u4FlipFlopInputByte, gpioConst.bit1);
                    gpioObj.GpioWrite(gpioConst.port0, u4FlipFlopInputByte);
                    //latch the flip flop input (rising edge of pin 11 (CP)) P2.5
                    gpioObj.GpioWrite(gpioConst.port2, ClearBits(u4FlipFlopInputByte, gpioConst.bit5));
                    gpioObj.GpioWrite(gpioConst.port2, SetBits(u4FlipFlopInputByte, gpioConst.bit5));
                }
                else
                {
                    u4FlipFlopInputByte = ClearBits(u4FlipFlopInputByte, gpioConst.bit2);
                    gpioObj.GpioWrite(gpioConst.port0, u4FlipFlopInputByte);
                    //latch the flip flop input (rising edge of pin 11 (CP)) P2.5
                    gpioObj.GpioWrite(gpioConst.port2, ClearBits(u4FlipFlopInputByte, gpioConst.bit5));
                    gpioObj.GpioWrite(gpioConst.port2, SetBits(u4FlipFlopInputByte, gpioConst.bit5));
                }
            }
        }

        /// <summary>
        /// Safe way to keep track of the flip flop latched output status variable.  Send in the old value that keeps track of the flip flop status 
        /// and the bit or bits that need to be cleared.  For example, if bit 3 needs to be cleared send in a value of '8', or, if bits 2 & 5
        ///  need to be cleared send in a value of '34'.
        /// </summary>
        /// <param name="oldByte"></param>
        /// <param name="newByte"></param>
        /// <returns></returns>
        public UInt32 ClearBits(UInt32 oldByte, UInt32 newByte)
        {
            newByte = (byte)((~newByte) & oldByte);
            return newByte;
        }

        /// <summary>
        /// Safe way to keep track of the flip flop latched output status variable.  Send in the old value that keeps track of the flip flop status 
        /// and the bit or bits that need to be set.  For example, if bit 3 needs to be set send in a value of '8', or, if bits 2 & 5
        ///  need to be set send in a value of '34'.
        /// </summary>
        /// <param name="oldByte"></param>
        /// <param name="newByte"></param>
        /// <returns></returns>
        public UInt32 SetBits(UInt32 oldByte, UInt32 newByte)
        {
            newByte = (byte)(oldByte | newByte);
            return newByte;
        }

        #endregion Common Methods required for all assemblies

        #region Methods unique to this assembly only
        //Write methods here
        /// <summary>
        /// Determines the state of all GPIO lines to the desired state when test begins or ends.
        /// </summary>
        private void GpioInitState()
        {
            //assert initial GPIO values to put relays / controls in passive or non - active state
            //(command all outputs to go high impedence)

            //Mux - assert the control pins to disable the output(P2.0, P2.1, P2.3)

            //set port 2 control pins all high except for P2.6 & P2.7 that control the Eload and DMM
            port2CtrlByte = gpioConst.port2InitStateByte;
            gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);

            //U2 Flip Flop
            u2FlipFlopInputByte = 0; //all outputs low
            gpioObj.GpioWrite(gpioConst.port0, u2FlipFlopInputByte);
            //latch the flip flop input (rising edge of pin 11 (CP)) P2.3
            gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit3));
            gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit3));

            //U3 Flip Flop
            u3FlipFlopInputByte = 0; //all outputs low
            gpioObj.GpioWrite(gpioConst.port0, u3FlipFlopInputByte);
            //latch the input (rising edge of pin 11 (CP) of flip flop) P2.4
            gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit4));
            gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit4));

            //U4 Flip Flop
            u4FlipFlopInputByte = 6;//all outputs low except ones controlling pass/fail LED
            gpioObj.GpioWrite(gpioConst.port0, u4FlipFlopInputByte);
            //latch the input (rising edge of pin 11 (CP) of flip flop) P2.5
            gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit5));
            gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit5));

        }

        public void PowerUp()
        {
            //testRoutineInformation["PowerUp"] = 1;
            //ensure outputs are all disconnected
            //set current limit to 1.5 amps
            //set voltage limit to 28 Volts

            if (PwrSup.IsBusy)
                MessageBox.Show("PWR SUP busy");
            PwrSup.SetPwrSupVoltLimits(28, 28, 5);
            PwrSup.TurnOutputOnOff(1, true, 1, 5);
            PwrSup.TurnOutputOnOff(1, false, 0, 0);

        }

        public void CurrentLimitCheck()
        {
            testRoutineInformation["CurrentLimitCheck"] = 1;
            //check current limit hasn't exceeded
        }

        #endregion Methods unique to this assembly only
    }


}
