using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Windows.Forms;
using System.Reflection;

namespace HydroFunctionalTest
{
    class SAM_55207
    {
        //NOTES:
        //  Power supply class - must turn on/off correct port to fixture depending on the fixture position
        //  ELoad class - must connect equipment (enable relays) to fixture depending on the fixture position
        //  DMM class - must connect equipment (enable relays) to fixture depending on the fixture position
        //

        #region Common Data Types required for all assemblies
        //Hardware & other Class objects instantiated in main UI that need to be passed to this class
        /// <summary>
        /// Provides control to the GPIO adapter in the fixture.  Initializes when the main UI passes the object to this class contructor
        /// </summary>
        private UsbToGpio gpioObj;
        /// <summary>
        /// Provides control to the PCAN adapter in the base station.  Initializes when the main UI passes the object to this class contructor
        /// </summary>
        private Pcan pCanObj;
        /// <summary>
        /// Saves pass fail info about overall test and subtests.  Key --> Name of test, Value = passed --> 1 or failed --> 0 or incomplete --> -1:
        /// This data is passed to the main UI upon the OnTestComplete event
        /// </summary>
        private Dictionary<String, int> passFailStatus;
        /// <summary>
        /// Provides the main UI with control to abort test.
        /// Value is changed in the event handler function btnAbort1_Click
        /// </summary>
        private bool userAbortClick = false;
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
        private List<String> testDataCSV = new List<String>();
        /// <summary>
        /// Store test progress here to send back to the main UI to update the user of status
        /// </summary>
        private List<String> testStatusInfo = new List<String>();
        #endregion Common Data Types required for all assemblies

        #region Data types unique to this assembly only
        //Write data types here
        #endregion Data types unique to this assembly only

        #region Contructor/Destructor
        /// <summary>
        /// Contructor.  Initializes program members.  Could optionally set these values using an external file.
        /// Hardware Objects must be sent from Main UI for this class to control and get status
        /// </summary>
        /// <param name="tmpGpio"></param>
        /// <param name="tmpPcan"></param>
        public SAM_55207(int tmpPos, string serNum, UsbToGpio tmpGpio, Pcan tmpPcan)
        {
            fixPosition = tmpPos;
            pCanObj = tmpPcan;
            gpioObj = tmpGpio;
            uutSerialNum = serNum;

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

        #endregion

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
        /// The main UI must provide the UUT with the cancel button object to subscribe to the button event.
        /// </summary>
        public void RunTests(System.Windows.Forms.Button softwAbortEvent)
        {
            testStatusInfo.Clear();
            //subscribe to the main UI abort button event
            softwAbortEvent.Click += new System.EventHandler(btnAbort1_Click);
            bool allTestsDone = false;
            userAbortClick = false;
            testStatusInfo.Add("***Begin Testing***");
            OnInformationAvailable();
            //loop until all tests have been complete or user aborts test.
            while (!allTestsDone && (!UserAbortCheck()))
            {
                if (!passFailStatus.ContainsValue(-1))
                    allTestsDone = true;//testing completed
                else
                {
                    foreach (var pair in passFailStatus)
                    {
                        //break out of the loop if user has aborted the test
                        if (UserAbortCheck())
                            break;
                        if (pair.Value == -1)
                        {
                            //run the tests (call functions):
                        }
                        //Fire the OnInformationAvailable event to update the main UI with test status from the testStatusInfo List
                        //Each function puts test status in the List --> testStatusInfo.Add("test status")
                        OnInformationAvailable();
                    }
                }
            }
            //unsubscribe from the main UI abort button event
            softwAbortEvent.Click -= new System.EventHandler(btnAbort1_Click);
            //Fire the OnTestComplete event to update the main UI and end the test
            testStatusInfo.Add("***End Testing UUT***");
            OnInformationAvailable();
            OnTestComplete();
        }

        private bool UserAbortCheck()
        {
            bool rtnResult = false;
            testStatusInfo.Clear();
            if (userAbortClick)
            {
                testStatusInfo.Add("***Abort Button Clicked***");
                rtnResult = true;
            }
            //check to see if user has aborted test
            //if aborted:
            //  Save test results up to the point at which the test was aborted
            //  Mark the UUT test as aborted on the ATR
            //  Follow the abort routine: Power down UUT, command all inputs/outputs to high impedence (via software and hardware commands)

            //Fire the OnInformationAvailable event to update the main UI with test status
            //Each function puts test status in the List --> testStatusInfo.Add("test status")
            OnInformationAvailable();
            return rtnResult;
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
                testStatusInfo.Add("Successfully sent pass/fail status to database.\r\n" + "Server Response: '" + tmpServResponseStr);
            else
                testStatusInfo.Add("Response from server does not match expected string.\r\nExpected response: '" + expectServerResponse + "'\r\nActual Server Response '" + tmpServResponseStr + "'");
            OnInformationAvailable();
        }

        /// <summary>
        /// Event hanling function fires when user clicks the GUI cancel button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbort1_Click(object sender, EventArgs e)
        {
            userAbortClick = true;
        }
        #endregion Common Methods required for all assemblies

        #region Methods unique to this assembly only
        //Write methods here
        #endregion Methods unique to this assembly only
    }
}
