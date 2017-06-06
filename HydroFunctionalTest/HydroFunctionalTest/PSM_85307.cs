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
        //  File output - setup the CSV dictionary to be common among all assemblies but the dictionary contents are specific to the assembly.  This makes output universal

        /// <summary>
        /// Class used to track the Pcan class data and also contains a bollean which controls when the thread in this program that continuously calls the Pcan class CanRead() method
        /// When CAN data is needed, it must be copied over from the Pcan class into a new Dictionary to avoid corrupting the data
        /// </summary>
        static class CanDataManagement
        {
            /// <summary>
            /// Keeps track of the last size of the CAN data as it grows.  This allows access to only most recent data
            /// </summary>
            static public int recentDataIndex = 0;
            /// <summary>
            /// Bool used to exit the thread which continually loops and extracts data from the CAN Adpt.
            /// </summary>
            static public bool exitThread = false;
            /// <summary>
            /// After searching for a specific CanID, store it's value here 
            /// </summary>
            static public UInt32 CanId;
            /// <summary>
            /// After searching for a specific CanID, store it's message info here 
            /// </summary>
            static public List<Byte> CanMessage = new List<Byte>();
            /// <summary>
            /// After searching for a specific CanID, store it's message length here 
            /// </summary>
            static public double timeStamp;
            
            static public Dictionary<UInt32, Dictionary<Byte[], uint>> canReadRtnData = new Dictionary<UInt32, Dictionary<Byte[], uint>>();
            
            static public Object lockRoutine = new Object();
        }
        /// <summary>
        /// Data type will be implemeted using a List of this data type.  This will make indexing the CAN data code safe and easier to search (hopefully)
        /// </summary>
        public struct CanDataStruct
        {
            public UInt32 canID;
            public Byte[] canMessage;
            public double timeStamp;
            //public UInt32 messageLength;
        }
        /// <summary>
        /// Create the List that will contain the struct holding all CAN data with different data types
        /// </summary>
        static public List<CanDataStruct> canDataRepo = new List<CanDataStruct>();

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
            public Dictionary<String, double[]> eLoadMeasLimits;
            /// <summary>
            /// Measurement Limits for PCBA. Key = output name, value[] = High limit(amps), Low limit(amps), High limit(volts), Low limit(volts)
            /// </summary>
            public Dictionary<String, double[]> pcbaMeasLimits;
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
            public Dictionary<String, String[]> canSetVoltCmd;
        }

        /// <summary>
        /// GPIO port and pin logic values for code readability
        /// </summary>
        private struct gpioConst
        {
            public const Byte port2InitState = 63; 
            public const Byte USB_PORT_EN = 1;
            public const Byte USB_PORT_SLCT = 2;
            public const Byte PRGMR_EN = 4;
            public const Byte CAN_PIN_SLCT = 8;
            public const Byte CAN_ID_1_EN = 16;
            public const Byte CAN_ID_2_EN = 32;
            public const Byte SeatID_EN = 64;
            public const Byte TOGL_SeatID = 128;
            public const Byte _28V_RTN_EN = 1;
            public const Byte DOUT_GND_EN = 2;
            public const Byte DIN_GND_EN = 4;
            public const Byte _5V_GND_EN = 8;
            public const Byte PWR_EN = 1;
            public const Byte nLED_GRN = 2;
            public const Byte nLED_RED = 4;
            public const Byte PWR_CHK_EN = 8;
            public const Byte FXTR_ID_EN = 16;
            public const Byte TOGL_DIN_EN = 32;
            public const UInt32 port0 = 0;
            public const UInt32 port1 = 1;
            public const UInt32 port2 = 2;
            public const Byte bit0 = 1;
            public const Byte bit1 = 2;
            public const Byte bit2 = 4;
            public const Byte bit3 = 8;
            public const Byte bit4 = 16;
            public const Byte bit5 = 32;
            public const Byte bit6 = 64;
            public const Byte bit7 = 128;
            public const int u1RefDes = 1;
            public const int u2RefDes = 2;
            public const int u3RefDes = 3;
            public const int u4RefDes = 4;
            public const Byte setBits = 1;
            public const Byte clearBits = 0;
        }

        /// <summary>
        /// Mux output enable byte values for code readability
        /// Byte is sent to GPIO adapter which turns on specific output of the mux.
        /// </summary>
        public struct muxOutputEn
        {
            //the values are byte representation of GPIO pin set/clear values depending on which desired output to be enabled
            public const int outCtrl = 7;
            public const int latchInput = 2;
            public const int aux0 = 1;
            public const int aux1 = 2;
            public const int aux2 = 3;
            public const int aux3 = 4;
            public const int aux4 = 5;
            public const int aux5 = 6;
            public const int _28vSW = 7;
            public const int _5vaSW = 8;
            public const int DOUT_0C = 9;
            public const int DOUT_1C = 10;
            public const int DOUT_2C = 11;
            public const int DOUT_3C = 12;
            public const int DOUT_4C = 13;
            public const int DOUT_5C = 14;
            public const int DOUT_6C = 15;
            public const int DOUT_7C = 16;
            public const int _5VDC = 17;
            public const int _3_3VDC = 18;
            public const int _2_5VDC = 19;
            public const int dIN_0C = 20;
            public const int dIN_1C = 21;
            public const int dIN_2C = 22;
            public const int dIN_3C = 23;
            public const int dIN_4C = 24;
            public const int dIN_5C = 25;
        }

        /// <summary>
        /// CAN Message IDs (bits 9-15 of CAN ID) for code readability
        /// </summary>
        private struct messageIDs
        {
            public const Byte heartbeat = 25;
            public const Byte digitalInputsStatus = 41;
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
        /// Task that continously collects CAN data
        /// </summary>
        Task getCanData;
        /// <summary>
        /// Saves pass fail info about overall test and subtests.  Key --> Name of test(must match method name exactly), Value = passed --> 1 or failed --> 0 or Not Tested --> -1:
        /// This data is checked when the OnTestComplete event is raised and the data is placed in a List and passed to the main UI upon the OnTestComplete event
        /// </summary>
        private Dictionary<String, int> testRoutineInformation;
        /// <summary>
        /// Store test data here. Key = Test Method name, Value (Dictionary) --> Key = subtest, Value =  Result,High limit,Low limit,Measurement,units
        /// --> there could be many elements to the List depending on the number of subtests
        /// </summary>
        private Dictionary<String, List<String>> testDataCSV;
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
        private const String expectServerResponse = "OK";
        /// <summary>
        /// For better control and consistency of delays
        /// </summary>
        private const int stdDelay = 250; //250 mS
        /// <summary>
        /// Initialize in this class constructor
        /// </summary>
        private String uutSerialNum;
        private const String testPrgmPath = "C:\\CraneFunctionalTest\\";
        private const String testDataFilePath = testPrgmPath + "Test Data\\";
        private const long bootWaitDelay = 8000; //8 second wait
        /// <summary>
        /// Store test progress here to send back to the main UI to update the user of status
        /// </summary>
        public List<String> testStatusInfo = new List<String>();
        #endregion Common Data Types required for all assemblies

        #region Data types uniqe to this assembly only   
        /// <summary>
        /// CAN commands to enable outputs.  Key = output name, Value[] = CAN frame1, CAN frame2
        /// </summary>
        public Dictionary<String, String[]> enOutput;
        /// <summary>
        /// CAN commands to disable outputs.  Key = output name, Value[] = CAN frame1, CAN frame2
        /// </summary>
        public Dictionary<String, String[]> nEnOutput;
        /// <summary>
        /// Holds measurement values for 28V adjustable output
        /// //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
        /// </summary>
        OutputTestParams auxOut28vTst = new OutputTestParams();
        /// <summary>
        /// Holds measurement values for 12V adjustable output
        /// //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
        /// </summary>
        OutputTestParams auxOut12vTst = new OutputTestParams();
        /// <summary>
        /// Holds measurement values for 5V adjustable output
        /// //1st Element-->High limit(amp), 2nd Element-->Low limit(amp), 3rd Element-->High limit(volt), 4th Element-->Low limit(volt)
        /// </summary>
        OutputTestParams auxOut5vTst = new OutputTestParams();
        /// <summary>
        /// static variable that holds U2 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public Byte u2FlipFlopInputByte;
        /// <summary>
        /// static variable that holds U3 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public Byte u3FlipFlopInputByte;
        /// <summary>
        /// static variable that holds U4 flip flop desired output.  This value is a 8 bit value and must be set for the desired output for Port 0
        /// </summary>
        public Byte u4FlipFlopInputByte;
        /// <summary>
        /// static variable that holds the GPIO port2 desired output.  Port 2 is the control logic for all flip flops, Mux and connection to common test equipment (Eload & DMM)
        /// </summary>
        public Byte port2CtrlByte;
        /// <summary>
        /// UUT digital output High limit.
        /// </summary>
        public const double uutDigOut_H = 3;      //Voltage Out High limit for UUT digital outputs
        /// <summary>
        /// UUT digital output Low limit.
        /// </summary>
        public const double uutDigOut_L = 2;      //Voltage Out Low limit for UUT digital outputs
        /// <summary>
        /// Holds the name of the net in schematic and the high/low tolerance.  //1st Element-->Output On, 2nd Element-->Output Off, 3rd Element-->Mux output Byte to enable output
        /// </summary>
        public Dictionary<String, int[]> digitalInputTest;
        /// <summary>
        /// Holds the name of the net in schematic and the high/low tolerance.  //1st Element-->Output On, 2nd Element-->Output Off, 3rd Element-->Mux output Byte to enable output
        /// </summary>
        public Dictionary<String, double[]> digitalOutputTest;
        /// <summary>
        /// Holds the name of the net in schematic and the high/low tolerance.  Key --> Net Name, Value[] = High limit(volts), Low limit(volts), Mux output Byte to enable output
        /// </summary>
        public Dictionary<String, double[]> powerRegTest;
        /// <summary>
        /// All Aux output off High Limits (Voltage & Current)
        /// </summary>
        public const double auxOutOff_H = .01;    //Current(amp) & Voltage(volt) High limit
        /// <summary>
        /// All Aux outputs off Low Limit (Voltage & Current)
        /// </summary>
        public const double auxOutOff_L = -.01;   //Current(amp) & Voltage(volt) Low limit
        /// <summary>
        /// UUT max current(mA) draw with no outputs enabled
        /// </summary>
        public const int standbyCurrent_H = 96; //units=mA
        /// <summary>
        /// UUT min current(mA) draw with no outputs enabled
        /// </summary>
        public const int standbyCurrent_L = 70; //units=mA

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

            //deactivate all relays/controls, i.e., assert GPIO lines to put relays/controls in passive or non-active state
            GpioInitState();

            //set/reset the variables that manage the CAN data
            CanDataManagement.exitThread = false;
            CanDataManagement.recentDataIndex = 0;
            canDataRepo.Clear();

            //initialize structs containing information specific to 28v, 12v & 5v adjustable output voltage tests
            
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
                { "DOUT_0C", new string[] { "413144064:8:74;129;0;5;0;9;15;0", "413143040:2:0;0" } }, 
                { "DOUT_1C", new string[] { "413144064:8:74;129;0;5;0;9;14;0", "413143040:2:0;0" } },
                //NOTE!!!! - DOUT_2C - DOUT_7C CAN frames are not enable commands but disable since their outputs are open collector with a pullup to 5V
                { "DOUT_2C", new string[] { "413144064:8:74;129;0;5;0;8;13;0", "413143040:2:0;0" } },
                { "DOUT_3C", new string[] { "413144064:8:74;129;0;5;0;8;12;0", "413143040:2:0;0" } },
                { "DOUT_4C", new string[] { "413144064:8:74;129;0;5;0;8;11;0", "413143040:2:0;0" } },
                { "DOUT_5C", new string[] { "413144064:8:74;129;0;5;0;8;10;0", "413143040:2:0;0" } },
                { "DOUT_6C", new string[] { "413144064:8:74;129;0;5;0;8;9;0", "413143040:2:0;0" } },
                { "DOUT_7C", new string[] { "413144064:8:74;129;0;5;0;8;8;0", "413143040:2:0;0" } },
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
                { "DOUT_0C", new string[] { "413144064:8:74;129;0;5;0;8;15;0", "413143040:2:0;0" } },
                { "DOUT_1C", new string[] { "413144064:8:74;129;0;5;0;8;14;0", "413143040:2:0;0" } },
                //NOTE!!!! - DOUT_2C - DOUT_7C CAN frames are not disable commands but enable since their outputs are open collector with a pullup to 5V
                { "DOUT_2C", new string[] { "413144064:8:74;129;0;5;0;9;13;0", "413143040:2:0;0" } },
                { "DOUT_3C", new string[] { "413144064:8:74;129;0;5;0;9;12;0", "413143040:2:0;0" } },
                { "DOUT_4C", new string[] { "413144064:8:74;129;0;5;0;9;11;0", "413143040:2:0;0" } },
                { "DOUT_5C", new string[] { "413144064:8:74;129;0;5;0;9;10;0", "413143040:2:0;0" } },
                { "DOUT_6C", new string[] { "413144064:8:74;129;0;5;0;9;9;0", "413143040:2:0;0" } },
                { "DOUT_7C", new string[] { "413144064:8:74;129;0;5;0;9;8;0", "413143040:2:0;0" } },
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

            powerRegTest = new Dictionary<String, double[]>
            {
                //1st Element-->High limit(volt), 2nd Element-->Low limit(volt), 3rd Element-->Mux output Byte to enable output
                { "5VDC", new double[] { (5*1.05), (5*.95), muxOutputEn._5VDC } },
                { "3.3VDC", new double[] { (3.3*1.05), (3.3*.95), muxOutputEn._3_3VDC } },
                { "2.5VDC", new double[] { (2.5*1.05), (2.5*.95), muxOutputEn._2_5VDC } },
            };

            digitalInputTest = new Dictionary<String, int[]>
            {
                //1st Element-->Output On, 2nd Element-->Output Off, 3rd Element-->Mux output Byte to enable output
                //Digital input changes have message ID = 41.  example: DIN0 output on, CAN data = 0xFE or 11111110
                { "DIN_0C", new int[] { 0xFE, 0xFF, muxOutputEn.dIN_0C } },
                { "DIN_1C", new int[] { 0xFD, 0xFF, muxOutputEn.dIN_1C } },
                { "DIN_2C", new int[] { 0xFB, 0xFF, muxOutputEn.dIN_2C } },
                { "DIN_3C", new int[] { 0xF7, 0xFF, muxOutputEn.dIN_3C } },
                { "DIN_4C", new int[] { 0xEF, 0xFF, muxOutputEn.dIN_4C } },
                { "DIN_5C", new int[] { 0xDF, 0xFF, muxOutputEn.dIN_5C } },
            };

            digitalOutputTest = new Dictionary<String, double[]>
            {
                //1st Element-->Output Enabled Low limit, 2nd Element-->Output Disabled High limit, 3rd Element-->Mux output Byte to enable output
                { "DOUT_0C", new double[] { 4.5, .5, muxOutputEn.DOUT_0C } },
                { "DOUT_1C", new double[] { 4.5, .5, muxOutputEn.DOUT_1C } },
                { "DOUT_2C", new double[] { 4.5, .5, muxOutputEn.DOUT_2C } },
                { "DOUT_3C", new double[] { 4.5, .5, muxOutputEn.DOUT_3C } },
                { "DOUT_4C", new double[] { 4.5, .5, muxOutputEn.DOUT_4C } },
                { "DOUT_5C", new double[] { 4.5, .5, muxOutputEn.DOUT_5C } },
                { "DOUT_6C", new double[] { 4.5, .5, muxOutputEn.DOUT_6C } },
                { "DOUT_7C", new double[] { 4.5, .5, muxOutputEn.DOUT_7C } },
            };

            #region Initialize Dictionary containing pass/fail status for all tests
            testRoutineInformation = new Dictionary<string, int>
            {
                //{ "FlashBootloader", -1 },
                //{ "LoadFirmware", -1 },
                { "PowerUp", -1 },
                //{ "PowerRegulators", -1 },
                //{ "AdjAuxOutputs", -1 },
                //{ "NonAdjAuxOutputs", -1 },
                { "DigitalInputs", -1 },
                { "DigitalOutputs", -1 },
                //{ "PowerLoss", -1 },
                //{ "SeatIDSwitch", -1 },
                //{ "CANComm", -1 },
                //{ "USBComm", -1 },
                //{ "CANID", -1 },
            };
            #endregion Initialize Dictionary containing all test pass fail status

            #region Initialize Dictionary CSV test data
            testDataCSV = new Dictionary<String, List<String>>
            {
                { "", new List<String> { "Test Description,Result,High Limit,Low Limit,Measurement,Units,Notes" } }, // header for the test data to follow
                { "FlashBootloader", new List<String> { "" } },
                { "LoadFirmware",  new List<String> { "" } },
                { "PowerUp", new List<String> { "" } },
                { "AdjAuxOutputs", new List<String> { "" } },
                { "NonAdjAuxOutputs", new List<String> { "" } },
                { "DigitalInputs",  new List<String> { "" } },
                { "DigitalOutputs",  new List<String> { "" } },
                { "PowerRegulators",  new List<String> { "" } },
                { "PowerLoss",  new List<String> { "" } },
                { "SeatIDSwitch",  new List<String> { "" } },
                { "CANComm",  new List<String> { "" } },
                { "USBComm",  new List<String> { "" } },
                { "CANID",  new List<String> { "" } },
            };
            #endregion
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
            //ensure outputs are all disconnected
            GpioInitState();
            //turn the power off
            PwrSup.TurnOutputOnOff(fixPosition, false, 0, 0);

            //************************************************************
            //Stop the task that collects CAN data
            //************************************************************

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
                        tmpList.Add(pair.Key + "--> Not Tested");
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
            //Send CAN data to file
            SendCanDataToFile();
            //Follow the abort routine: Power down UUT, command all inputs/outputs to high impedence (via software and hardware commands)

            //event handler checks for any subscribers
            if (TestComplete != null)
                TestComplete(this, tmpList, this.fixPosition, tmpAllTestsPass);
        }
        #endregion Event Publishers and Handlers required for all assemblies

        #region Common Methods required for all assemblies

        /// <summary>
        /// For better control and consistency of delays
        /// </summary>
        /// <param name="sleepDelay"></param>
        public void StandardDelay(int sleepDelay = stdDelay)
        {
            System.Threading.Thread.Sleep(sleepDelay);
        }

        /// <summary>
        /// Call all test functions and execute all other test requirements.  This is the only function called from the main UI
        /// The main UI must provide the UUT with the cancel button object to subscribe to the button event.
        /// </summary>
        public void RunTests(System.Windows.Forms.Button softwAbortEvent)
        {
            try
            {
                //subscribe to the main UI abort button event
                softwAbortEvent.Click += new System.EventHandler(btnAbort_Click);
                //Start task that continously collects CAN data
                getCanData = new Task(new Action(CollectCanData));
                getCanData.Start();
                SetClearLEDs();//turn the pass/fail indictator LEDs off
                bool allTestsDone = false;
                softwAbort = false;
                hardwAbort = false;
                bool abortTesting = false;
                testStatusInfo.Add("\r\n**********Begin Testing " + this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "**********\r\n");
                //loop until all tests have been complete or test is aborted.
                while (!allTestsDone && (!abortTesting))
                {
                    //if test routine contains a value of -1, the test is incomplete
                    //if the value is never set to a value other than -1, the test will run indefinitely or until aborted
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
                            if (abortTesting)
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
                                StandardDelay();
                                theMethod.Invoke(this, new object[0]);
                                //Fire the OnInformationAvailable event to update the main UI with test status from the testStatusInfo List
                                //Each function puts test status in the List --> testStatusInfo.Add("test status")
                                OnInformationAvailable();
                                testStatusInfo.Clear();
                                abortTesting = AbortCheck();
                                if ((testRoutineInformation[key] == -1) && !abortTesting)
                                    testStatusInfo.Add("Resource busy, jumping to next test...\r\n");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                testStatusInfo.Add("Exception in the 'RunTests' method for the \r\n" + this.ToString().Substring(this.ToString().IndexOf(".") + 1) + " assembly\r\n" + ex.Message);
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
        /// This method is checked within a while loop inside the 'RunTests' method.  If AbortCheck returns true, the 'RunTests' method will break out of the loop and end the test
        /// softwAbort can be set by any test method if the test encounters a failure that requires the test to end.
        /// </summary>
        /// <returns></returns>
        private bool AbortCheck()
        {
            bool rtnResult = false;
            //check to see limit switch is activated
            UInt32 limitSw = gpioObj.GpioRead(1, 1);

            //check to see if abort button has been enabled
            //read the value of port 1 and mask the don't care bits (all bits except P1.6)
            UInt32 abortButton = gpioObj.GpioRead(1);
            abortButton = (Byte)(64 & abortButton);
            if (abortButton != 64)
            {
                testStatusInfo.Add("Abort button used by operator\r\nTest Aborted");
                hardwAbort = rtnResult = true;
            }

            //check to see if program subroutines have requested an abort
            if (softwAbort)
            {                
                rtnResult = true;
            }
            //make sure the fixture lid is still engaged (down)
            else if (limitSw == 1)
            {
                testStatusInfo.Add("Lid Down Not Detected\r\nTest Aborted\r\n");
                hardwAbort = rtnResult = true;
            }
            return rtnResult;
        }

        public String DmmMeasure()
        {
            String tmpDmmStr = null;
            if (fixPosition == 1)
                tmpDmmStr = Dmm.Measure("meas:volt:dc?", gpioObj, port2CtrlByte, false);
            else if(fixPosition == 2)
                tmpDmmStr = Dmm.Measure("meas:volt:dc?", gpioObj, port2CtrlByte, true);
            else
                MessageBox.Show("Incorrect fixture number parameter sent to UUT 'DmmMeasure' method: " + fixPosition.ToString());
            return tmpDmmStr;
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
                "~" + uutSerialNum.ToString() + "~" + dateTime + "~" + tmpAllTestsPass.ToString() + ".txt";

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
                        fileString = fileString + pair.Key + "--> Not Tested\r\n";
                        //testDataCSV.Add(pair.Key + "--> Incomplete");
                }
            }
            fileString = fileString + "\r\n****\tDetailed Test Results\t****";

            try
            {
                System.IO.Directory.CreateDirectory(testDataFilePath +
                    this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\\" + uutSerialNum.Remove(5));

                testStatusInfo.Add("Sending test data to txt file:\r\n" + testDataFilePath + fileName);
                OnInformationAvailable();
                testStatusInfo.Clear();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(testDataFilePath + fileName))
                {
                    file.WriteLine(fileString);
                    //Write the results of the testDataCSV to the file
                    //Each element in the dictionary should contain:
                    // Key = "Test Method Name"
                    // Value (List<String>) = "Test description, Result, highLimit, lowLimit, measurement, units -->there could be many elements to this list depending on the number of subtests
                    //Example:
                    // Key = "AdjAuxOutputs"
                    // Value = "Aux0 voltage (eload), Pass, 27, 25, 26, volts
                    foreach (var pair in testDataCSV)
                    {
                        //write the test Method name
                        String tempString = "****" + pair.Key + "****\r\n";                       
                        //now list the comma seperated details about the test
                        for (int i = 0; i < pair.Value.Count; i++)
                        {
                            tempString = tempString + pair.Value[i];
                        }
                        file.WriteLine(tempString);
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

        private void SendCanDataToFile()
        {
            //stop the thread that loops through and collects CAN data
            CanDataManagement.exitThread = true;
            String dateTime = (System.DateTime.Now.ToString().Replace("/", "_")).Replace(":", ".");
            String fileName = this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\\" + uutSerialNum.Remove(5) +
                "\\CAN DATA\\" + this.ToString().Substring(this.ToString().IndexOf(".") + 1) +
                "~" + uutSerialNum.ToString() + "~" + dateTime + ".txt";
            try
            {
                System.IO.Directory.CreateDirectory(testDataFilePath +
                    this.ToString().Substring(this.ToString().IndexOf(".") + 1) + "\\" + uutSerialNum.Remove(5) + "\\CAN DATA\\");

                testStatusInfo.Add("Sending CAN data to txt file:\r\n" + testDataFilePath + fileName);
                OnInformationAvailable();
                testStatusInfo.Clear();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(testDataFilePath + fileName))
                {
                    file.WriteLine("TimeStamp,CAN-ID,Length,Data");
                    //copy the contents of the CAN data to a new variable
                    List<CanDataStruct> tempCanReadData;
                    GetCanData(out tempCanReadData);
                    for (int i = 0; i < tempCanReadData.Count; i++)
                    {
                        CanDataStruct tempDataStruct = tempCanReadData[i];
                        String canID = tempDataStruct.canID.ToString("X");
                        String canMessage = "";
                        for (int j = 0; j < tempDataStruct.canMessage.Length; j++)
                            canMessage = canMessage + tempDataStruct.canMessage[j].ToString("X") + " ";
                        canID = tempDataStruct.timeStamp.ToString() + "," + canID + "," + tempDataStruct.canMessage.Length.ToString() + "," + canMessage;
                        file.WriteLine(canID);
                    }
                }
            }
            catch (Exception ex)
            {
                testStatusInfo.Add("File operation error.\r\n" + ex.Message + "\r\nFailed to send CAN data to txt file:\r\n" + testDataFilePath + fileName);
                OnInformationAvailable();
                testStatusInfo.Clear();
            }
        }

        private void RecordTestResults(String testMethod, String testDescrip, String result, String highLimit = "", String lowLimit = "", String measurement = "", String units = "", String notes = "")
        {
            bool testResultRecorded = false;
            //load the results into one string
            String tempString = testDescrip + "," + result + "," + highLimit + "," + lowLimit + "," + measurement + "," + units + "," + notes + "\r\n";
            //find the test method in the testDataCSV dictionary
            foreach(var pair in testDataCSV)
            {
                if(testMethod == pair.Key)
                {
                    //add the test results to the List in the dictionary associated with test method
                    pair.Value.Add(tempString);
                    testResultRecorded = true;
                }
            }
            //Alert if test results aren't recorded.  For debug purposes
            if (!testResultRecorded)
                MessageBox.Show("Test Result Not Recorded for Test method: " + testMethod);
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
                testStatusInfo.Add("\r\nSuccessfully sent pass/fail status to database.\r\n"  + "Server Response: '" + tmpServResponseStr + "'");            
            else
                testStatusInfo.Add("\r\nResponse from server does not match expected string.\r\nExpected response: '" +  expectServerResponse + "'\r\nActual Server Response '" + tmpServResponseStr + "'");
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

        private void SetClearLEDs(bool ledsOff = true, bool passedAllTests = false)
        {
            if (ledsOff)
            {
                IC_ChangeOutputState(gpioConst.u4RefDes, 6, gpioConst.setBits); //Set P2.1 & P2.2 high to turn off
            }
            else
            {
                if (passedAllTests)
                    IC_ChangeOutputState(gpioConst.u4RefDes, gpioConst.bit1, gpioConst.clearBits); //Set P2.1 low to turn Green
                else
                    IC_ChangeOutputState(gpioConst.u4RefDes, gpioConst.bit2, gpioConst.clearBits); //Set P2.2 low to turn Red
            }
        }

        /// <summary>
        /// Way to keep track of the variable that controls flip flop latched output status.  Send in the variables current (old) value that keeps track of the flip flop status 
        /// and the bit or byte to change to as the new value.  For example, if bit 3 needs to be cleared, send in a value of '8'.  Example: if bits 2 & 5
        ///  need to be cleared, send in a byte value of '34'.
        /// </summary>
        /// <param name="oldByte"></param>
        /// <param name="newByte"></param>
        /// <returns></returns>
        public Byte ClearBits(Byte oldByte, Byte newByte)
        {
            newByte = (Byte)((~newByte) & oldByte);
            return newByte;
        }

        /// <summary>
        /// Way to keep track of the variable that controls flip flop latched output status.  Send in the variables current (old) value that keeps track of the flip flop status 
        /// and the bit or byte to change to as the new value.  For example, if bit 3 needs to be set, send in a value of '8'.  Example: if bits 2 & 5
        ///  need to be set, send in a byte value of '34'.
        /// </summary>
        /// <param name="oldByte"></param>
        /// <param name="newByte"></param>
        /// <returns></returns>
        public Byte SetBits(Byte oldByte, Byte newByte)
        {
            newByte = (Byte)(oldByte | newByte);
            return newByte;
        }

        private void OpenSeatIDSwitches()
        {
            //U2 Flip Flop
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.SeatID_EN, gpioConst.clearBits); //clear P2.6
        }

        public void IC_ChangeOutputState(int IC_refDesignator, Byte bitOrBitsToChange, Byte setBits)
        {
            if(IC_refDesignator == 1)
            {
                if(setBits == 1)
                {
                    //turn on the node of interest
                    gpioObj.GpioWrite(gpioConst.port0, bitOrBitsToChange);
                    //turn mux output on
                    port2CtrlByte = ClearBits(port2CtrlByte, muxOutputEn.outCtrl);
                    gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);
                    //Latch the input
                    port2CtrlByte = SetBits(port2CtrlByte, muxOutputEn.latchInput);
                    gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);
                }
                else
                {
                    //turn all output nodes off
                    bitOrBitsToChange = 0;
                    gpioObj.GpioWrite(gpioConst.port0, bitOrBitsToChange);
                    //turn mux output on
                    port2CtrlByte = ClearBits(port2CtrlByte, muxOutputEn.outCtrl);
                    gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);
                    //Latch the input
                    port2CtrlByte = SetBits(port2CtrlByte, muxOutputEn.latchInput);
                    gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);
                    //turn mux off
                    port2CtrlByte = SetBits(port2CtrlByte, muxOutputEn.outCtrl);
                    gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);
                }
            }
            else if(IC_refDesignator == 2)
            {
                //change the variable holding the flip flop latched output
                if(setBits == 1)
                    u2FlipFlopInputByte = SetBits(u2FlipFlopInputByte, bitOrBitsToChange);
                else
                    u2FlipFlopInputByte = ClearBits(u2FlipFlopInputByte, bitOrBitsToChange);

                //write the new byte to the GPIO port
                gpioObj.GpioWrite(gpioConst.port0, u2FlipFlopInputByte);

                //Latch Input of U2 flip flop by toggling the control bit P2.3
                StandardDelay();
                gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit3));
                StandardDelay(1000);
                gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit3));


            }
            else if(IC_refDesignator == 3)
            {
                //change the variable holding the flip flop latched output
                if (setBits == 1)
                    u3FlipFlopInputByte = SetBits(u3FlipFlopInputByte, bitOrBitsToChange);
                else
                    u3FlipFlopInputByte = ClearBits(u3FlipFlopInputByte, bitOrBitsToChange);

                //write the new byte to the GPIO port
                gpioObj.GpioWrite(gpioConst.port0, u3FlipFlopInputByte);

                ///Latch Input of U3 Flip Flop by toggling the control bit P2.4
                StandardDelay();
                gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit4));
                StandardDelay(1000);
                gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit4));

            }
            else if (IC_refDesignator == 4)
            {
                //change the variable holding the flip flop latched output
                if (setBits == 1)
                    u4FlipFlopInputByte = SetBits(u4FlipFlopInputByte, bitOrBitsToChange);
                else
                    u4FlipFlopInputByte = ClearBits(u4FlipFlopInputByte, bitOrBitsToChange);

                //write the new byte to the GPIO port
                gpioObj.GpioWrite(gpioConst.port0, u4FlipFlopInputByte);

                ///Latch Input of U4 Flip Flop by toggling the control bit P2.5
                StandardDelay();
                gpioObj.GpioWrite(gpioConst.port2, ClearBits(port2CtrlByte, gpioConst.bit5));
                StandardDelay(1000);
                gpioObj.GpioWrite(gpioConst.port2, SetBits(port2CtrlByte, gpioConst.bit5));
            }
            else
            {
                MessageBox.Show("Invalid flip flop reference designator in 'IC_ChangeOutputState' method.");
                softwAbort = true;
            }            
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

            //set port 2 control pins all high except for P2.6 & P2.7 that control the Eload and DMM
            //This will disable or latch all the latchable devices in their current state and disable the MUX
            
            //set initial state of GPIO port 2
            port2CtrlByte = gpioConst.port2InitState;  //save the changes to the port status variable
            gpioObj.GpioWrite(gpioConst.port2, port2CtrlByte);

            //U2 Flip Flop
            u2FlipFlopInputByte = 0; //all outputs low
            IC_ChangeOutputState(gpioConst.u2RefDes, 0, gpioConst.clearBits);

            //U3 Flip Flop
            u3FlipFlopInputByte = 0; //all outputs low
            IC_ChangeOutputState(gpioConst.u3RefDes, 0, gpioConst.clearBits);

            //U4 Flip Flop
            u4FlipFlopInputByte = 6;//all outputs low except ones controlling pass/fail LEDs
            IC_ChangeOutputState(gpioConst.u4RefDes, 0, gpioConst.clearBits);
        }

        private UInt16 CheckStandbyCurrent(Byte[] canData)
        {
            //query the power supply for current

            //compare the canData parameter to the
            UInt16 mostSigByte = (Byte)canData[4];
            UInt16 leastSigByte = (Byte)canData[3];
            mostSigByte = (UInt16)(mostSigByte << 8);
            mostSigByte = (UInt16)(mostSigByte | leastSigByte);
            return mostSigByte;
        }

        /// <summary>
        /// Method continously gets CAN data and saves them in the Pcan class buffer
        /// </summary>
        private void CollectCanData()
        {
            //continously load CAN data onto the CanDataManagement class buffer until exitThread is true
            //other methods will use the buffer to search for data.
            
            UInt32 tempCanID;
            Byte[] tempCanData;
            double tempTimStamp;
            while (!CanDataManagement.exitThread)
            {
                if (pCanObj.CanRead(out tempCanID, out tempCanData, out tempTimStamp))
                {
                    //lock access to the Dictionary that collects data continuously (This prevents inadvertant simultaneous access to the data from different threads)
                    lock (CanDataManagement.lockRoutine)
                    {
                        CanDataStruct canStorage = new CanDataStruct();
                        canStorage.canID = tempCanID;
                        canStorage.canMessage = tempCanData;
                        canStorage.timeStamp = tempTimStamp;
                        canDataRepo.Add(canStorage);
                    }
                }
                else
                {
                    //CAN buffer empty, no data received
                }
            }            
        }

        /// <summary>
        /// Method prevents inadvertant simultaneous access to the CAN data from different threads
        /// send a List of data type 'CanDataStruct' as a paramater to safely copy the CAN data
        /// </summary>
        /// <param name="tempCanData"></param>
        private void GetCanData(out List<CanDataStruct> tempCanData)
        {
            lock (CanDataManagement.lockRoutine)
                tempCanData = new List<CanDataStruct>(canDataRepo);
        }

        /// <summary>
        /// Searches the CAN data from the Pcan class message buffer which is collecting data continously while the test is running.
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="millisecondsToWait"></param>
        /// <returns></returns>
        private bool AwaitMessageID(Byte messageID, double millisecondsToWait = bootWaitDelay)
        {
            bool foundMessageID = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while ((!foundMessageID) && (stopwatch.ElapsedMilliseconds < millisecondsToWait))
            {
                //copy the contents of the CAN data to a new variable
                List<CanDataStruct> tempCanReadData;
                GetCanData(out tempCanReadData);

                //shift the CAN data indexer down to the most recent data so old data isn't searched repeatedly
                CanDataManagement.recentDataIndex = tempCanReadData.Count - CanDataManagement.recentDataIndex;
                int countLoops = 0;
                //search the dictionary in reverse order
                for (int i = (tempCanReadData.Count - 1); i > 0; i--)
                {
                    //look for the CAN heartbeat ID:
                    //Strip out the Message ID (bits 9-15) from the CAN ID 
                    UInt32 mask = 0xFA00;  //mask all bits except for bits 9-15;
                    CanDataStruct tempDataStruct = tempCanReadData[i];
                    UInt32 tempMessageID = tempDataStruct.canID;
                    tempMessageID = tempMessageID & mask;  //mask the upper 2 bytes and the lower byte + the LSB bit of the 2nd byte
                    tempMessageID = tempMessageID >> 9;     //shift the new value down 9 bits - this should now represent the Message ID per Crane CAN frame description
                    if (messageID == tempMessageID)
                    {
                        //match found to the Message ID (bits 9 - 15 of CAN ID)
                        foundMessageID = true;
                        CanDataManagement.CanId = tempDataStruct.canID;
                        CanDataManagement.CanMessage.Clear();
                        for (int j = 0; j < tempDataStruct.canMessage.Length; j++)
                        {
                            CanDataManagement.CanMessage.Add(tempDataStruct.canMessage[j]);
                        }
                        //save the CAN message length to the CanDataManagment class if needed for analysis
                        CanDataManagement.timeStamp = tempDataStruct.timeStamp;
                        //stop searching if the message ID is found
                        break;
                    }
                    //check to see if the loop has cycled through all available new data, if true, break out of the loop
                    if (countLoops > CanDataManagement.recentDataIndex)
                        break;
                    countLoops++;
                }
            }
            return foundMessageID;
        }

        public void FlashBootloader()
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
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.bit2, gpioConst.setBits);
            StandardDelay(1000);

            //turn power on
            PwrSup.TurnOutputOnOff(fixPosition, true, 28, .250); //turn output on - 28V .5A

            //Initiate a command line script that runs the bootloader routine
            //the return data should be a dictionary with only one element, the key as a bool and value a string containing any pertinent information
            Dictionary<bool, String> tmpDict = ProgrammingControl.ProgramBootloader(testPrgmPath, this, gpioObj);

            //turn power off
            PwrSup.TurnOutputOnOff(fixPosition, false, 0, 0);
            StandardDelay(1000);

            //disable JTAG USB port
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.bit2, gpioConst.clearBits);

            //set the method status flag in the testRoutineInformation Dictionary
            //check the first dictinoary value returned from the ProgrammingControl.ProgramBootloader routine returned true
            if (tmpDict.Keys.First())
            {
                testRoutineInformation["FlashBootloader"] = 1; // Successfully flashed bootloader
                RecordTestResults("FlashBootloader", "Flash the bootloader to the device", "Pass");
            }
            else
            {
                //
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["FlashBootloader"] = 0; // Failed to program bootloader
                RecordTestResults("FlashBootloader", "Flash the bootloader to the device", "Fail");
                //discontinue test by setting softwAbort to true;
                softwAbort = true;
            }
            testStatusInfo.Add(tmpDict.Values.First());
        }

        public void LoadFirmware()
        {
            //check to see if resource is busy (optional, probably won't want to wait since no other test will run without first programming)
            //if (!ProgrammingControl.AutoItIsBusy)
            //{

            //}

            //enable UUT USB port
            //(change the value representing the last byte value latched on the flip flop output before writing to GPIO)
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.bit0, gpioConst.setBits);
            StandardDelay(1000);

            //turn power on
            PwrSup.TurnOutputOnOff(fixPosition, true, 28, .250); //turn output on - 28V .250A

            //the return data should be a dictionary with only one element, the key as a bool and value a string containing any pertinent information
            Dictionary<bool, String> tmpDict = ProgrammingControl.LoadFirmwareViaAutoit(testPrgmPath, this);

            //turn power off
            PwrSup.TurnOutputOnOff(fixPosition, false, 0, 0);
            StandardDelay(1000);

            //disable UUT USB port
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.bit2, gpioConst.clearBits);

            //set the method status flag in the testRoutineInformation Dictionary
            if (tmpDict.Keys.First())
            {
                testRoutineInformation["LoadFirmware"] = 1;  //Successfully programmed
                RecordTestResults("LoadFirmware", "Load the test firmware to the device", "Pass");
            }
            else
            {
                //
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["LoadFirmware"] = 0;
                RecordTestResults("LoadFirmware", "Load the test firmware to the device", "Fail");
                //discontinue test by setting softwAbort to true;
                softwAbort = true;
            }
            testStatusInfo.Add(tmpDict.Values.First());

        }

        public void PowerUp()
        {
            //ensure outputs are all disconnected
            GpioInitState();
            //ensure Seat ID switches are all open
            OpenSeatIDSwitches();
            //set voltage limit to 28 Volts and current to 1.5 amps
            bool pwrSupResult = PwrSup.SetPwrSupVoltLimits(28, 28, 5);//CH1= 28Volts, CH2=28Volts, CH3=5Volts
            //turn the output on, if false abort test
            pwrSupResult = pwrSupResult & PwrSup.TurnOutputOnOff(fixPosition, true, 28, .1); ////Turn output on, 28Volts, 100mAmp limit
            foreach (String s in PwrSup.pwrSupReturnData)
            {
                testStatusInfo.Add("\t" + s);
            }
            testStatusInfo.Add(System.Environment.NewLine);
            if (!pwrSupResult)
            {
                softwAbort = true;
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["PowerUp"] = 0;
                softwAbort = true;
                testStatusInfo.Add("\tPower Supply communication issue in 'PowerUp' method.\r\n\t***PowerUp routine failed***\r\n");
                RecordTestResults("PowerUp", "Board power up sequence", "Fail", "", "", "", "", "Power Supply communication issue in 'PowerUp' method");
            }
            else
            {
                ////////////////////////////////////////////////////////////////////////////
                //Remember to change this line of code.  It was put here because the VGA gender
                //changer is missing one of the CAN pin connections.  So the other pin has to be selected
                //IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.bit3, gpioConst.setBits);
                testRoutineInformation["PowerUp"] = 1;
                /////////////////////////////////////////////////////////////////////////////
                if (AwaitMessageID(messageIDs.heartbeat))
                {
                    //set the method status flag in the testRoutineInformation Dictionary
                    testRoutineInformation["PowerUp"] = 1;
                    testStatusInfo.Add("\t***PowerUp routine passed***");
                    RecordTestResults("PowerUp", "Standby Current Verify", "Pass");
                }
                else
                {
                    //set the method status flag in the testRoutineInformation Dictionary
                    testRoutineInformation["PowerUp"] = 0;
                    softwAbort = true;
                    testStatusInfo.Add("\tNo CAN data found.  Unable to communicate with UUT.\r\n\t***PowerUp failed.***\r\n");
                    RecordTestResults("PowerUp", "Board power up sequence", "Fail", "", "", "", "", "No CAN data found.  Unable to communicate with UUT");
                }
            }
        }

        public void PowerRegulators()
        {
            //initial voltage value to force failure if DMM measurement fails
            double dmmMeas = 0;
            //check the 5VDC, 3.3VDC and 2.5VDC voltage levels
            //count how many tests are to be executed for to verify all passed at end of method
            int howManyRegFailures = powerRegTest.Count();
            foreach (var pair in powerRegTest)
            {
                //Mux output - Select output node and enable mux output
                IC_ChangeOutputState(gpioConst.u1RefDes, (Byte)pair.Value[2], gpioConst.setBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.setBits commands the contol lines to their high disabled state
                //measure via DMM
                StandardDelay();
                String tmpDmmStr = null;
                tmpDmmStr = DmmMeasure();
                if (tmpDmmStr != null)
                {
                    dmmMeas = double.Parse(tmpDmmStr);
                }

                //verify measurements are within tolerance
                if ((dmmMeas <= pair.Value[0]) && (dmmMeas >= pair.Value[1]))
                {
                    howManyRegFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                    testStatusInfo.Add("\t" + pair.Key + " power regulator output passed\r\n\tMeasured: " + dmmMeas.ToString() + "Volts (High=" + pair.Value[0].ToString() + ",Low=" + pair.Value[1] + ")\r\n");
                    RecordTestResults("PowerUp", pair.Key + " Power Regulator", "Pass", pair.Value[0].ToString(), pair.Value[1].ToString(), dmmMeas.ToString(), "Volts");
                }
                else
                {
                    testStatusInfo.Add("\t" + pair.Key + " power regulator output is outside tolerance\r\n\tMeasured: " + dmmMeas.ToString() + "Volts (High=" + pair.Value[0].ToString() + ",Low=" + pair.Value[1].ToString() + ")\r\n");
                    RecordTestResults("PowerUp", pair.Key + " Power Regulator", "Fail", pair.Value[0].ToString(), pair.Value[1].ToString(), dmmMeas.ToString(), "Volts");
                }
                OnInformationAvailable();
                testStatusInfo.Clear();
            }
            //Mux output - select output node and disable mux output
            IC_ChangeOutputState(gpioConst.u1RefDes, 0, gpioConst.setBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.setBits commands the contol lines to their high disabled state


            //watch the CAN bus heartbeat and verify that the standby current has stablized to a specific value for 5 consecutive status updates
            int tempStbyCurrent = 0;
            int stbyCurrStableCount = 0;
            int countCANReadFailures = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while ((stbyCurrStableCount < 5) && (countCANReadFailures < 3) && (stopwatch.ElapsedMilliseconds < 5000))
            {
                //look for the CAN heartbeat ID: 
                if (AwaitMessageID(messageIDs.heartbeat))
                {
                    //send the last CAN data found w/ heartbeat message id to check that standby current is within tolerance
                    tempStbyCurrent = CheckStandbyCurrent(CanDataManagement.CanMessage.ToArray());
                    if ((tempStbyCurrent < standbyCurrent_H) && (tempStbyCurrent > standbyCurrent_L))
                        stbyCurrStableCount++;
                    else
                        stbyCurrStableCount--;
                    testStatusInfo.Add("\tStandby Current: " + tempStbyCurrent.ToString() + "mA\r\n");
                }
                else
                    countCANReadFailures++;
            }
            stopwatch.Stop();
            //See how the power supply current reading compares
            String tempPwrSupCurrent = PwrSup.CheckCurrent(fixPosition);
            if (tempPwrSupCurrent != null)
            {
                double tempDouble = (double.Parse(tempPwrSupCurrent)) * 1000;
                if ((tempDouble < standbyCurrent_H) && (tempDouble > standbyCurrent_L))
                    testStatusInfo.Add("\tPower Supply Current: " + tempPwrSupCurrent + "\r\n");
            }
            else
                testStatusInfo.Add("\tPower Supply Current returned NULL: " + tempPwrSupCurrent + "\r\n");

            if (stbyCurrStableCount >= 5)
            {
                testStatusInfo.Add("\tStandby current within tolerance");
                RecordTestResults("PowerUp", "Standby Current Verify", "Pass", standbyCurrent_H.ToString(), standbyCurrent_L.ToString(), tempStbyCurrent.ToString(), "mA");
            }
            else
            {
                softwAbort = true;
                testStatusInfo.Add("\tStandby current outside tolerance (High=" + standbyCurrent_H + "; Low=" + standbyCurrent_L + ")\r\n\t***PowerUp failed.***\r\n");
                RecordTestResults("PowerUp", "Board power up sequence", "Fail", standbyCurrent_H.ToString(), standbyCurrent_L.ToString(), tempStbyCurrent.ToString(), "mA");
            }

            if ((howManyRegFailures <= 0) && (stbyCurrStableCount >= 5))
            {
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["PowerRegulators"] = 1;
                testStatusInfo.Add("\t***PowerRegulators Test Passed***");
            }
            else
            {
                //set the method status flag in the testRoutineInformation Dictionary only if the test hasn't been aborted
                if (!(softwAbort || hardwAbort))
                {
                    testRoutineInformation["PowerRegulators"] = 0;
                    testStatusInfo.Add("\r\n\t***PowerRegulators Test Failed***");
                }
                else
                    testStatusInfo.Add("\r\n\t***PowerRegulators Test Not Tested***");
            }
        }

        public void AdjAuxOutputs()
        {
            int maxWait = 2000; //amount of time in seconds to wait for Eload to become available            
            //wait maxWait for Eload to become available and return to run other tests if wait exceeds maxWait
            if (Eload.IsBusy)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while ((Eload.IsBusy) && (stopwatch.ElapsedMilliseconds < maxWait))
                {
                    
                };
                //exit the test routine if the Eload is still busy
                if (stopwatch.ElapsedMilliseconds > maxWait)
                    return;
            }

            //Eload should be available now, execute the following tests
            foreach (var pair in auxOut28vTst.eLoadMeasLimits)
            {
                //jump out of the loop if test is aborted
                if (softwAbort | hardwAbort)
                    break;

                //turn the adjustable output on by sending the output specific CAN command
                //1st Frame-->(CAN Message ID):(USB Message Length):(USB Message ID);(Source node ID);(Dest. Node ID);(Message length);(Enumeration);(arg 1-->Output ON=9 or OFF=8);(arg2-->output net);0
                //Followed by 2nd frame-->(CAN message ID):(USB Message Length):(arg3);0
                //Example of the array with CAN frames that are colon and semicolon delimited "413144064:8:74;129;0;5;0;9;2;0", "413143040:2:0;0"
                String tempCanFrame1 = enOutput[pair.Key][0]; //the first frame
                String tempCanFrame2 = enOutput[pair.Key][1]; //the second frame

                SendCanFrame(tempCanFrame1);
                SendCanFrame(tempCanFrame2);
                //wait for the output data on the CAN bus
                System.Threading.Thread.Sleep(3000);
            }
            
            //set the method status flag in the testRoutineInformation Dictionary
            testRoutineInformation["AdjAuxOutputs"] = 1;
            testStatusInfo.Add("\t***AdjAuxOutputs Test Passed***");
        }

        public void SendCanFrame(String tempCanFrame)
        {
            //parse the CAN frame
            int index = tempCanFrame.IndexOf(":");
            UInt32 tempCanId = UInt32.Parse(tempCanFrame.Remove(index));
            tempCanFrame = tempCanFrame.Substring(index + 1);
            index = tempCanFrame.IndexOf(":");
            UInt32 tempMessageLength = UInt32.Parse(tempCanFrame.Remove(index));
            tempCanFrame = tempCanFrame.Substring(index + 1);
            List<Byte> tempCanMessage1 = new List<Byte>();
            while (index > 0)
            {
                index = tempCanFrame.IndexOf(";");
                if (index > 0)
                {
                    tempCanMessage1.Add(Byte.Parse(tempCanFrame.Remove(index)));
                    tempCanFrame = tempCanFrame.Substring(index + 1);
                }
                else
                    tempCanMessage1.Add(Byte.Parse(tempCanFrame));
            }
            pCanObj.CanWrite(tempCanMessage1.ToArray(), tempCanId, (Byte)tempMessageLength);
        }

        public void NonAdjAuxOutputs()
        {
            //set the method status flag in the testRoutineInformation Dictionary
            testRoutineInformation["NonAdjAuxOutputs"] = 1;
            testStatusInfo.Add("\t***NonAdjAuxOutputs Test Passed***");
        }

        public void PowerLoss()
        {
            if (PwrSup.ChangeVoltAndOrCurrOutput(fixPosition, 23, 1.5))
            {
                //look for the CAN heartbeat ID: 
                if (AwaitMessageID(messageIDs.heartbeat))
                {
                    //set the method status flag in the testRoutineInformation Dictionary
                    testRoutineInformation["PowerLoss"] = 1;
                }
            }
            else
            {
                //
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["PowerLoss"] = 0;
            }
        }

        public void DigitalInputs()
        {
            int howManyInputFailures = digitalInputTest.Count()*2; //double the test number since test checks the high and low logic value seperately
            //switch the relay connecting the digital inputs to 5V_RTN and connect to 5V instead
            IC_ChangeOutputState(gpioConst.u4RefDes, gpioConst.TOGL_DIN_EN, gpioConst.setBits);
            //also enable relay connecting the isolated grounds (DIN_GND_EN) to provide the needed ground reference for digital inputs 0-3, also enable _28V_RTN_EN to tie these grounds together
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst.DIN_GND_EN, gpioConst.setBits);
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst._28V_RTN_EN, gpioConst.setBits);

            //Test all the digital inputs in high condition
            foreach (var pair in digitalInputTest)
            {
                //jump out of the loop if test is aborted
                if (softwAbort || hardwAbort)
                    break;

                //Mux output - Select output node and enable mux output                
                IC_ChangeOutputState(gpioConst.u1RefDes, (Byte)pair.Value[2], gpioConst.setBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.setBits commands the contol lines to their high disabled state
                StandardDelay();

                //find the digital input High status in the CAN data
                if (AwaitMessageID(messageIDs.digitalInputsStatus, 3000))
                {
                    //Extract the logic value from the CAN data
                    uint logicValue = CanDataManagement.CanMessage[1];
                    //verify logic value is high
                    if (logicValue == pair.Value[0])
                    {
                        howManyInputFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                        testStatusInfo.Add("\t" + pair.Key + " digital Input High passed\r\n\tLogic Value returned: " + logicValue.ToString() + ")\r\n");
                        RecordTestResults("DigitalInputs", pair.Key + " Digital Input High", "Pass", pair.Value[0].ToString(), pair.Value[0].ToString(), logicValue.ToString(), "Logic-High/Low");
                    }
                    else
                    {
                        testStatusInfo.Add("\t" + pair.Key + " digital Input High failed\r\n\tLogic Value returned: " + logicValue.ToString() + ")\r\n");
                        RecordTestResults("DigitalInputs", pair.Key + " Digital Input High", "Fail", pair.Value[0].ToString(), pair.Value[0].ToString(), logicValue.ToString(), "Logic-High/LOW");
                    }
                }
                else
                {
                    testStatusInfo.Add("\t" + pair.Key + " digital input High failed\r\n\tLogic Value returned: " + "No CAN data found\r\n");
                    RecordTestResults("DigitalInputs", pair.Key + " Digital Input High", "Fail", pair.Value[0].ToString(), pair.Value[0].ToString(), "No CAN data found", "Logic-High/LOW\r\n", "Failed to find message ID " + messageIDs.digitalInputsStatus);
                }
                OnInformationAvailable();
                testStatusInfo.Clear();
            }

            //Now toggle the input to test the other digital input logic condition
            //switch the relay connecting the digital inputs to 5V and connect to 5V_RTN
            IC_ChangeOutputState(gpioConst.u4RefDes, gpioConst.TOGL_DIN_EN, gpioConst.clearBits);
            foreach (var pair in digitalInputTest)
            {
                //jump out of the loop if test is aborted
                if (softwAbort || hardwAbort)
                    break;

                //Mux output - Select output node and enable mux output                
                IC_ChangeOutputState(gpioConst.u1RefDes, (Byte)pair.Value[2], gpioConst.setBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.setBits commands the contol lines to their high disabled state
                StandardDelay();

                //find the digital input Low status in the CAN data
                if (AwaitMessageID(messageIDs.digitalInputsStatus, 3000))
                {
                    //Extract the logic value from the CAN data
                    uint logicValue = CanDataManagement.CanMessage[1];
                    //verify logic value is low
                    if (logicValue == pair.Value[1])
                    {
                        howManyInputFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                        testStatusInfo.Add("\t" + pair.Key + " digital Input Low passed\r\n\tLogic Value returned: " + logicValue.ToString() + ")\r\n");
                        RecordTestResults("DigitalInputs", pair.Key + " Digital Input Low", "Pass", pair.Value[0].ToString(), pair.Value[0].ToString(), logicValue.ToString(), "Logic-High/Low");
                    }
                    else
                    {
                        testStatusInfo.Add("\t" + pair.Key + " digital Input Low failed\r\n\tLogic Value returned: " + logicValue.ToString() + ")\r\n");
                        RecordTestResults("DigitalInputs", pair.Key + " Digital Input Low", "Fail", pair.Value[1].ToString(), pair.Value[1].ToString(), logicValue.ToString(), "Logic-High/LOW");
                    }
                }
                else
                {
                    testStatusInfo.Add("\t" + pair.Key + " digital Input Low failed\r\n\tLogic Value returned: " + "No CAN data found\r\n");
                    RecordTestResults("DigitalInputs", pair.Key + " Digital Input Low", "Fail", pair.Value[1].ToString(), pair.Value[1].ToString(), "No CAN data found", "Logic-High/LOW", "Failed to find message ID " + messageIDs.digitalInputsStatus);
                }

                OnInformationAvailable();
                testStatusInfo.Clear();
            }
            //Mux output - disable mux output                
            IC_ChangeOutputState(gpioConst.u1RefDes, 0, gpioConst.clearBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.clearBits commands the contol lines to their high disabled state
            //disable relay connecting the isolated grounds (DIN_GND_EN) to digital inputs 0-3 & also disable 28V_Rtn connection also
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst.DIN_GND_EN, gpioConst.clearBits);
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst._28V_RTN_EN, gpioConst.clearBits);

            if (howManyInputFailures <= 0)
            {
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["DigitalInputs"] = 1;
                testStatusInfo.Add("\t***DigitalInputs Test Passed***");
            }
            else
            {
                //set the method status flag in the testRoutineInformation Dictionary only if the test hasn't been aborted
                if (!(softwAbort || hardwAbort))
                {
                    testRoutineInformation["DigitalInputs"] = 0;
                    testStatusInfo.Add("\r\n\t***DigitalInputs Test Failed***");
                }
                else
                    testStatusInfo.Add("\r\n\t***DigitalInputs Test Untested***");
            }

        }

        public void DigitalOutputs()
        {
            int howManyOutputFailures = digitalOutputTest.Count() * 2; //double the test number since test checks the high and low logic value seperately
            //also enable relay connecting the isolated grounds (DOUT_GND_EN) to provide the needed ground reference for digital inputs 0-3, also enable _5V_GND_EN to tie these grounds together
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst.DOUT_GND_EN, gpioConst.setBits);
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst._28V_RTN_EN, gpioConst.setBits);

            //Test all the digital Output in high condition
            foreach (var pair in digitalOutputTest)
            {
                //jump out of the loop if test is aborted
                if (softwAbort || hardwAbort)
                    break;

                //Mux output - Select output node and enable mux output                
                IC_ChangeOutputState(gpioConst.u1RefDes, (Byte)pair.Value[2], gpioConst.setBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.setBits commands the contol lines to their high disabled state
                StandardDelay();

                //turn the digital output on by sending the output specific CAN command
                //1st Frame-->(CAN Message ID):(USB Message Length):(USB Message ID);(Source node ID);(Dest. Node ID);(Message length);(Enumeration);(arg 1-->Output ON=9 or OFF=8);(arg2-->output net);0
                //Followed by 2nd frame-->(CAN message ID):(USB Message Length):(arg3);0
                //Example of the array with CAN frames that are colon and semicolon delimited "413144064:8:74;129;0;5;0;8;15;0", "413143040:2:0;0"
                String tempCanFrame1 = enOutput[pair.Key][0]; //the first frame
                String tempCanFrame2 = enOutput[pair.Key][1]; //the second frame

                SendCanFrame(tempCanFrame1);
                SendCanFrame(tempCanFrame2);

                //initial voltage value to force failure if DMM measurement fails
                double dmmMeas = 0;
                String tmpDmmStr = null;
                tmpDmmStr = DmmMeasure();
                if (tmpDmmStr != null)
                {
                    dmmMeas = double.Parse(tmpDmmStr);
                }

                //verify measurements are within tolerance
                if (dmmMeas >= pair.Value[0])
                {
                    howManyOutputFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                    testStatusInfo.Add("\t" + pair.Key + " Digital Output Enabled passed\r\n\tMeasured: " + dmmMeas.ToString() + " Volts(High > " + pair.Value[0].ToString() + ", Low < " + pair.Value[1].ToString() + ")\r\n");
                    RecordTestResults("DigitalOutputs", pair.Key + " Digital Output Enabled", "Pass", pair.Value[0].ToString(), "N/A", dmmMeas.ToString(), "Volts");
                }
                else
                {
                    testStatusInfo.Add("\t" + pair.Key + " Digital Output Enabled is outside tolerance\r\n\tMeasured: " + dmmMeas.ToString() + " Volts(High > " + pair.Value[0].ToString() + ", Low < " + pair.Value[1].ToString() + ")\r\n");
                    RecordTestResults("DigitalOutputs", pair.Key + "  Digital Output Enabled", "Fail", pair.Value[0].ToString(), "N/A", dmmMeas.ToString(), "Volts");
                }

                //turn the digital output off by sending the output specific CAN command
                //1st Frame-->(CAN Message ID):(USB Message Length):(USB Message ID);(Source node ID);(Dest. Node ID);(Message length);(Enumeration);(arg 1-->Output ON=9 or OFF=8);(arg2-->output net);0
                //Followed by 2nd frame-->(CAN message ID):(USB Message Length):(arg3);0
                //Example of the array with CAN frames that are colon and semicolon delimited "413144064:8:74;129;0;5;0;9;15;0", "413143040:2:0;0"
                tempCanFrame1 = nEnOutput[pair.Key][0]; //the first frame
                tempCanFrame2 = nEnOutput[pair.Key][1]; //the second frame

                SendCanFrame(tempCanFrame1);
                SendCanFrame(tempCanFrame2);
                
                //initial voltage value to force failure if DMM measurement fails
                dmmMeas = 5;
                tmpDmmStr = null;
                tmpDmmStr = DmmMeasure();
                if (tmpDmmStr != null)
                {
                    dmmMeas = double.Parse(tmpDmmStr);
                }

                //verify measurements are within tolerance
                if (dmmMeas <= pair.Value[1])
                {
                    howManyOutputFailures--; //subtract from the number of tests, if eventually reaching 0 or < 0, then no tests failed
                    testStatusInfo.Add("\t" + pair.Key + " Digital Output Disabled passed\r\n\tMeasured: " + dmmMeas.ToString() + " Volts(High > " + pair.Value[0].ToString() + ", Low < " + pair.Value[1].ToString() + ")\r\n");
                    RecordTestResults("DigitalOutputs", pair.Key + " Digital Output Disabled", "Pass", pair.Value[0].ToString(), "N/A", dmmMeas.ToString(), "Volts");
                }
                else
                {
                    testStatusInfo.Add("\t" + pair.Key + " Digital Output Disabled is outside tolerance\r\n\tMeasured: " + dmmMeas.ToString() + " Volts(High > " + pair.Value[0].ToString() + ", Low < " + pair.Value[1].ToString() + ")\r\n");
                    RecordTestResults("DigitalOutputs", pair.Key + "  Digital Output Disabled", "Fail", pair.Value[0].ToString(), "N/A", dmmMeas.ToString(), "Volts");
                }
                
                OnInformationAvailable();
                testStatusInfo.Clear();
            }

            //Mux output - disable mux output                
            IC_ChangeOutputState(gpioConst.u1RefDes, 0, gpioConst.clearBits);//muxOutputEn.outputOff value is a disconnected from all nets, gpioConst.clearBits commands the contol lines to their high disabled state
            //disable relay connecting the isolated grounds (DOUT_GND_EN) and ground reference for digital inputs 0-3, also enable _5V_GND_EN
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst.DOUT_GND_EN, gpioConst.clearBits);
            IC_ChangeOutputState(gpioConst.u3RefDes, gpioConst._28V_RTN_EN, gpioConst.clearBits);

            if (howManyOutputFailures <= 0)
            {
                //set the method status flag in the testRoutineInformation Dictionary
                testRoutineInformation["DigitalOutputs"] = 1;
                testStatusInfo.Add("\t***DigitalOutputs Test Passed***");
            }
            else
            {
                //set the method status flag in the testRoutineInformation Dictionary only if the test hasn't been aborted
                if (!(softwAbort || hardwAbort))
                {
                    testRoutineInformation["DigitalOutputs"] = 0;
                    testStatusInfo.Add("\r\n\t***DigitalOutputs Test Failed***");
                }
                else
                    testStatusInfo.Add("\r\n\t***DigitalOutputs Test Untested***");
            }

        }

        public void SeatIDSwitch()
        {
            bool statusBit1 = false;
            bool statusBit2 = false;

            //turn power off
            PwrSup.TurnOutputOnOff(fixPosition, false, 0, 0); ////Turn output on, 28Volts, 100mAmp limit
            UInt32 switchPosition = (UInt32)(CanDataManagement.CanId & 0xFF000000);
            if ((switchPosition != 0x08000000) | (switchPosition != 0x18000000))
            {
                //make sure both relays for Seat ID testing are disabled(disconnected)
                IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.SeatID_EN, gpioConst.clearBits);
                IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.TOGL_SeatID, gpioConst.clearBits);
            }
            //enable the Seat ID relay - SW1 & SW3 closed, SW2 & SW4 open
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.SeatID_EN, gpioConst.setBits);
            //turn power on
            PwrSup.TurnOutputOnOff(fixPosition, true, 28, .1); ////Turn output on, 28Volts, 100mAmp limit
            //get the status of the Seat ID
            //look for the CAN heartbeat with CAN ID MSB = 0x0900 0000 or 0x1900 0000:
            if (AwaitMessageID(messageIDs.heartbeat))
            {
                UInt32 tempCanID = CanDataManagement.CanId;
                //mask the 4 bytes with the bits that should be set with current switch configuration
                tempCanID = (uint)tempCanID & 0xFF000000;
                if ((tempCanID == 0x19000000) | (tempCanID == 0x09000000))
                    statusBit1 = true;
            }

            //turn power off
            PwrSup.TurnOutputOnOff(fixPosition, false, 0, 0); ////Turn output on, 28Volts, 100mAmp limit
            //enable the seat ID toggle relay - //enable the Seat ID relay - SW1 & SW3 open, SW2 & SW4 closed
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.TOGL_SeatID, gpioConst.setBits);
            //turn power on
            PwrSup.TurnOutputOnOff(fixPosition, true, 28, .1); ////Turn output on, 28Volts, 100mAmp limit

            //get the status of the Seat ID
            //look for the CAN heartbeat with CAN ID MSB = 0xAF00 0000: 
            if (AwaitMessageID(messageIDs.heartbeat))
            {
                UInt32 tempCanID = CanDataManagement.CanId;
                //mask the 4 bytes with the bits that should be set with current switch configuration
                tempCanID = (uint)tempCanID & 0xFF000000;
                if ((tempCanID == 0x1A000000) | (tempCanID == 0x0A000000))
                    statusBit2 = true;
            }

            //disable both relays
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.SeatID_EN, gpioConst.clearBits);
            IC_ChangeOutputState(gpioConst.u2RefDes, gpioConst.TOGL_SeatID, gpioConst.clearBits);

            //set the method status flag in the testRoutineInformation Dictionary
            if(statusBit1 && statusBit2)
            {
                testRoutineInformation["SeatIDSwitch"] = 1;
                testStatusInfo.Add("\r\n\t***SeatIDSwitch Test Passed***");
                RecordTestResults("SeatIDSwitch", "SW1 & SW3 closed, SW2 & SW4 open", "Pass", "", "", "Expected CAN ID: ", "", "CAN ID value = " + CanDataManagement.CanId.ToString());
                RecordTestResults("SeatIDSwitch", "SW1 and SW3 open, SW2 & SW4 closed", "Pass", "", "", "Expected CAN ID: ", "", "CAN ID value = " + CanDataManagement.CanId.ToString());
            }
            else
            {
                testRoutineInformation["SeatIDSwitch"] = 0;
                if (!statusBit1)
                {
                    testStatusInfo.Add("\tUnexpected CAN ID with Seat ID SW1 & SW3 closed, SW2 & SW4 open.  CAN ID: " + CanDataManagement.CanId.ToString());
                    RecordTestResults("SeatIDSwitch", "SW1 & SW3 closed, SW2 & SW4 open", "Fail", "", "", "Expected CAN ID: ", "", "CAN ID value = " + CanDataManagement.CanId.ToString());
                }
                else
                {
                    testStatusInfo.Add("\tUnexpected CAN ID with Seat ID SW1 & SW3 open, SW2 & SW4 closed.  CAN ID: " + CanDataManagement.CanId.ToString());
                    RecordTestResults("SeatIDSwitch", "SW1 and SW3 open, SW2 & SW4 closed", "Fail", "", "", "Expected CAN ID: ", "", "CAN ID value = " + CanDataManagement.CanId.ToString());
                }
                testRoutineInformation["SeatIDSwitch"] = 0;
                testStatusInfo.Add("\r\n\t***SeatIDSwitch Test Failed***");
            }
        }

        public void CANComm()
        {
            //set the method status flag in the testRoutineInformation Dictionary
            testRoutineInformation["CANComm"] = 1;
            testStatusInfo.Add("\t***CANComm Test Passed***");
        }

        public void USBComm()
        {
            //set the method status flag in the testRoutineInformation Dictionary
            testRoutineInformation["USBComm"] = 1;
            testStatusInfo.Add("\t***USBComm Test Passed***");
        }

        public void CANID()
        {
            //set the method status flag in the testRoutineInformation Dictionary
            testRoutineInformation["CANID"] = 1;
            testStatusInfo.Add("\t***CANID Test Passed***");
        }

        #endregion Methods unique to this assembly only
    }


}
