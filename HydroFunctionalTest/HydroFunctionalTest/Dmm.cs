using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Diagnostics;

namespace HydroFunctionalTest
{
    class Dmm
    {
        #region Class members
        private SerialPort dmmDev;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        public List<string> dmmReturnData = new List<string>();
        //private bool isBusy = false;
        public bool IsBusy { get; protected set; }
        public const String modelNum = "5492B";

        #endregion Class members

        #region Class Methods
        /// <summary>
        /// Contructor
        /// </summary>
        public Dmm()
        {
            
        }
        #endregion Class Methods

        #region Class Methods

        #endregion Class Methods

        public void CloseComport()
        {
            dmmDev.Close();
        }

        public bool InitializeDmm(string comPortName)
        {
            dmmReturnData.Clear();
            bool rtnValue = false;
            dmmDev = new SerialPort();
            String idnCommand = "*IDN?";
            try
            {
                dmmDev.PortName = comPortName;
                dmmDev.BaudRate = 115200;
                dmmDev.DataBits = 8;
                dmmDev.ReadTimeout = 125;
                dmmDev.WriteTimeout = 125;
                dmmDev.Open();
                if(dmmDev.IsOpen)
                {
                    try
                    {
                        //query the power supply for its model number                        
                        dmmDev.WriteLine(idnCommand);
                        String tmpStr = dmmDev.ReadTo("\r");
                        if (tmpStr.Contains(idnCommand))
                        {
                            //Check for the DMM model number in the return string
                            if (tmpStr.Contains(modelNum))
                                rtnValue = true;
                            else
                            {
                                //reset device for future query
                                //dmmDev.Write("*RST\r\n");
                                dmmDev.Close();
                            }
                        }
                        //else, string doesn't contain command
                        else
                        {
                            dmmReturnData.Add("comport: " + comPortName + " failed to respond to command: " + idnCommand);
                            dmmDev.Close();
                        }
                    }
                    catch (TimeoutException)
                    {
                        dmmReturnData.Add("Timeout while Initializing DMM comport: " + comPortName);
                        if (dmmDev.IsOpen)
                            dmmDev.Close();
                    }
                }
                else
                {
                    dmmReturnData.Add("Failed to open comport: " + comPortName + "for the DMM");
                }
            }
            catch (Exception ex)
            {
                dmmReturnData.Add("Communication error while attempting to initialize DMM\r\nComport: " + comPortName + Environment.NewLine + ex.Message);
                rtnValue = false;
                if(dmmDev.IsOpen)
                    dmmDev.Close();
            }
            return rtnValue;
        }

        public String Measure(String cmd)
        {
            dmmReturnData.Clear();
            String rtnData = null;
            int loopCount = 0;
            int loopMax = 3;
            //send command and then loop(loopMax) till response is received
            dmmDev.WriteLine(cmd);
            while ((rtnData == null) && (loopCount < loopMax))
            {
                try
                {
                    String tmpReadData = dmmDev.ReadTo("\r");
                    //Check for the command in the return string
                    if (tmpReadData.Contains(cmd))
                    {
                        int index = tmpReadData.IndexOf("\n");
                        rtnData = tmpReadData.Substring(index + 1);
                    }                   
                    //else, string doesn't contain command
                    else
                        dmmReturnData.Add("DMM failed to respond to command: " + cmd);
                    loopCount++;
                }
                catch (TimeoutException)
                {
                    dmmReturnData.Add("Timeout while waiting for DMM to respond to command");
                }
            }
            //check to see if the rtnData value is a number before sending it back
            double i = 0;
            if (double.TryParse(rtnData, out i))
                dmmReturnData.Add("Dmm returned a number value: " + rtnData);
            else
            {
                dmmReturnData.Add("DMM didn't return a numeric value: " + rtnData);
                rtnData = null;
            }
            return rtnData;
        }

        public String Read()
        {
            String rtnData = "";


            return rtnData;

        }
    }
}
