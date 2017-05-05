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
    static class Dmm
    {
        #region Class members
        /// <summary>
        /// Holds the comport name used for reading and writing from/to the device
        /// </summary>
        static private SerialPort dmmDev;
        /// <summary>
        /// Load all the settings into one string to spit out.
        /// </summary>
        static public String dmmSettings;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        static public List<string> dmmReturnData = new List<string>();
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the IsBusy property.  
        /// Together they manage access to the power supply resource, preventing simultaeous requests.
        /// </summary>
        static private Object lockRoutine = new Object();
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the device
        /// </summary>
        static public bool IsBusy { get; private set; } = false;
        /// <summary>
        /// Model number for the DMM.  Used when opening comport and querying the DMM using SCPI command '*IDN'
        /// </summary>
        static public String modelNum = "5492B";

        #endregion Class members

        /// <summary>
        /// Releases the comport.
        /// </summary>
        static public void CloseComport()
        {
            dmmDev.Close();
        }

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Receives the comport name passed to it, opens the comport and attempts to query for its model information.  
        /// If no response or wrong model number, method returns false.  See dmmReturnData list for further information
        /// </summary>
        /// <param name="comPortName"></param>
        /// <returns></returns>
        static public bool InitializeDmm(string comPortName)
        {
            lock (lockRoutine)
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
                    dmmSettings = "DMM Settings:\r\nBaudrate=" + dmmDev.BaudRate.ToString() + "\r\nDataBits=" + dmmDev.DataBits.ToString() + "\r\nParity=None" + "\r\nTermination Character=Carriage Return(CR)" + "\r\nReturn (Echo Select)=ON";
                    dmmDev.Open();
                    if (dmmDev.IsOpen)
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
                    if (dmmDev.IsOpen)
                        dmmDev.Close();
                }
                return rtnValue;
            }
        }

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        ///   The parameter must be a valid SCPI command per the DMM programming syntax.
        ///   The DMM returns the command string and this is used for error checking
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        static public String Measure(String cmd)
        {
            lock (lockRoutine)
            {
                dmmReturnData.Clear();
                String rtnData = null;
                int loopCount = 0;
                int loopMax = 3;
                //send command and then loop till response is received or loopMax
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
        }

        /// <summary>
        /// unused.  can be used as a method template
        /// </summary>
        /// <returns></returns>
        static public String Read()
        {
            String rtnData = "";


            return rtnData;

        }
    }
}
