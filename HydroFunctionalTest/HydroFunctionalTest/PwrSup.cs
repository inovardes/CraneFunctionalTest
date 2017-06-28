using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO.Ports;
using System.Diagnostics;

namespace HydroFunctionalTest
{
    static class PwrSup
    {

        #region Class members
        /// <summary>
        /// Holds the comport name used for reading and writing from/to the device
        /// </summary>
        static private SerialPort pwrSupDev;
        /// <summary>
        /// Provides external classes info about the power supply settings
        /// </summary>
        static public String pwrSupSettings;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        static public List<string> pwrSupReturnData = new List<string>();
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the IsBusy property.  
        /// Together they manage access to the power supply resource, preventing simultaneous requests.
        /// </summary>
        static private Object lockRoutine = new Object();
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the device
        /// </summary>
        static public bool IsBusy { get; private set; } = false;
        /// <summary>
        /// Model number for the power supply.  Used when opening comport and querying the power supply using SCPI command '*IDN'
        /// </summary>
        static private String modelNum = "9131B";
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch1VoltLim = 30;
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch2VoltLim = 30;
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch3VoltLim = 5;
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch1CurrLim = 1.5;
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch2CurrLim = 1.5;
        /// <summary>
        /// Limit that can't be exceeded.  Limit set when the power supply first initializes
        /// </summary>
        static private double ch3CurrLim = 3;

        #endregion Class members

        #region Public Class Methods
        /// <summary>
        /// Receives the comport name passed to it, opens the comport and attempts to query for its model information.  
        /// If no response or wrong model number, method returns false.  See pwrSupReturnData list for further information
        /// </summary>
        /// <param name="comPortName"></param>
        /// <returns></returns>
        static public bool InitializePwrSup(string comPortName)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                bool rtnStatus = false;
                try
                {
                    String idnCommand = "*IDN?";
                    pwrSupReturnData.Clear();
                    pwrSupDev = new SerialPort();
                    pwrSupDev.PortName = comPortName;
                    pwrSupDev.BaudRate = 115200;
                    pwrSupDev.DataBits = 8;
                    pwrSupDev.ReadTimeout = 125;
                    pwrSupDev.WriteTimeout = 125;
                    pwrSupSettings = "DMM Settings:\r\nBaudrate=" + pwrSupDev.BaudRate.ToString() + "\r\nDataBits=" + pwrSupDev.DataBits.ToString();
                    pwrSupDev.Open();
                    if (pwrSupDev.IsOpen)
                    {
                        try
                        {
                            //query the power supply for its model number
                            pwrSupDev.WriteLine(idnCommand);
                            //Check for the power supply model number in the return string
                            String tmpStr = pwrSupDev.ReadLine();
                            if (tmpStr.Contains(modelNum))
                            {
                                if (ConfigurePwrSup())
                                    rtnStatus = true;
                                else
                                    pwrSupReturnData.Add("Successfully communicated with the power supply but encountered an error when putting the device in 'remote' mode.");
                            }
                            else
                            {
                                pwrSupDev.Close();
                            }
                            
                        }
                        catch (TimeoutException)
                        {
                            pwrSupReturnData.Add("Timeout while Initializing power supply comport: " + comPortName);
                            if (pwrSupDev.IsOpen)
                                pwrSupDev.Close();
                        }
                    }
                    else
                    {
                        pwrSupReturnData.Add("Failed to open comport: " + comPortName + " for the power supply");
                    }
                }
                catch (Exception ex)
                {
                    pwrSupReturnData.Add("Communication error while attempting to initialize power supply\r\n" + comPortName + Environment.NewLine + ex.Message);
                    if (pwrSupDev.IsOpen)
                        pwrSupDev.Close();
                }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return rtnStatus;
            }
        }

        /// <summary>
        /// Releases the comport.
        /// </summary>
        static public void CloseComport()
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupDev.Close();
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
            }
        }

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Method purpose is to check if the power supply is drawing more current than the set limit.  Should only be called if the power supply output is on 
        /// returns true if the power supply voltage is lower than the expected output, meaning the output is attempting to supply max current
        /// </summary>
        /// <param name="outputChan"></param>
        /// <param name="expSuppOutput"></param>
        /// <param name="maxOutputVoltDrop"></param>
        /// <returns></returns>
        static public bool OutputVoltDrop(int outputChan, double expSuppOutput, double maxOutputVoltDrop = 1)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupReturnData.Clear();
                bool rtnStatus = false;
                if (Command("instrument:select CH" + outputChan.ToString()))
                    if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))
                    {
                        String tmpStr = Query("measure:voltage?");
                        if (tmpStr != null)
                        {
                            double tmpValue = double.Parse(tmpStr);
                            if (tmpValue < (expSuppOutput - maxOutputVoltDrop))
                            {
                                pwrSupReturnData.Add("Channel #" + outputChan + "output voltage (" + tmpValue.ToString() + "V) dropped the maximum (" +
                                    maxOutputVoltDrop.ToString() + "V) voltage from the expected output (" + expSuppOutput.ToString() + "V)");
                                rtnStatus = true;
                            }
                        }
                    }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return rtnStatus;
            }
        }

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Method will turn on the output that is passed to it and initially set the votlage to 0 and current to the passed in value.  
        /// Then, the voltage will ramped up to the voltage value
        /// that is passed to it
        /// </summary>
        /// <param name="outputChan"></param>
        /// <param name="turnOn"></param>
        /// <param name="setVolt"></param>
        /// <param name="setCurrLimit"></param>
        /// <returns></returns>
        static public bool TurnOutputOnOff(int outputChan, bool turnOn, double setVolt, double setCurrLimit)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupReturnData.Clear();
                bool outputCorrectlySet = false;
                bool voltSlowRampSuccess = false;
                //make sure the voltage and current isn't set above limits
                if ((outputChan == 1) || (outputChan == 2))
                {
                    if (outputChan == 1)
                    {
                        if (setVolt > ch1VoltLim)
                            setVolt = ch1VoltLim;
                        if (setCurrLimit > ch1CurrLim)
                            setCurrLimit = ch1CurrLim;
                    }
                    else
                    {
                        if (setVolt > ch2VoltLim)
                            setVolt = ch2VoltLim;
                        if (setCurrLimit > ch2CurrLim)
                            setCurrLimit = ch2CurrLim;
                    }
                }
                else if (outputChan == 3)
                {
                    if (setVolt > ch3VoltLim)
                        setVolt = ch3VoltLim;
                    if (setCurrLimit > ch3CurrLim)
                        setCurrLimit = ch3CurrLim;
                }
                    
                if ((outputChan <= 3) || (outputChan >= 1))
                {
                    try
                    {
                        int startVoltage = 0;//start the output voltage at 0 to prepare to ramp up slowly
                        int convertedBool = Convert.ToInt32(turnOn);
                        //select the correct output
                        if (Command("instrument:select CH" + outputChan.ToString()))
                            //verify command was received
                            if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))
                            {
                                //set the voltage and current output for the selected channel
                                if (Command("source:apply CH" + outputChan.ToString() + "," + startVoltage.ToString() + "," + setCurrLimit.ToString()))
                                {
                                    //verify command was received
                                    String tmpQuery = Query("source:apply? CH" + outputChan.ToString());
                                    if ((tmpQuery.Contains(startVoltage.ToString())) && (tmpQuery.Contains(setCurrLimit.ToString())))
                                        //Turn on/off the output
                                        if (Command("source:channel:output:state " + convertedBool.ToString()))
                                            //verify command was received
                                            if (Query("source:channel:output:state?").Contains(convertedBool.ToString()))
                                                outputCorrectlySet = true;
                                }
                            }
                        if (turnOn)
                        {
                            //now that output is on, ramp the voltage to desired level
                            voltSlowRampSuccess = RampVoltage(setVolt);
                            String tmpVolt = Query("measure:voltage?");
                            //if the output voltage doesn't reach .5V from the desired value, shut power off and return false
                            if(double.Parse(tmpVolt) < (setVolt - .5))
                            {
                                pwrSupReturnData.Add("Output voltage failed to reach the desired output voltage (+- .5V)\r\nChannel #" + outputChan.ToString() + " output voltage set to: " + tmpVolt + "V, " + setCurrLimit.ToString() + "A");
                                outputCorrectlySet = false;
                            }
                            else
                                pwrSupReturnData.Add("Channel #" + outputChan.ToString() + " output voltage set to: " + tmpVolt + "V, " + setCurrLimit.ToString() + "A");
                        }
                        else if(!outputCorrectlySet)
                            pwrSupReturnData.Add("Problem turning on power supply Channel #" + outputChan.ToString());
                        else
                        {
                            //turn the output off
                            voltSlowRampSuccess = true;
                            pwrSupReturnData.Add("Channel #" + outputChan.ToString() + " output off");
                        }
                    }
                    catch (Exception ex)
                    {
                        pwrSupReturnData.Add("Exception occurred in 'TurnOutputOnOff' method\r\n" + ex.Message);
                    }
                }
                else
                    pwrSupReturnData.Add("Invalid Channel parameter: " + outputChan.ToString() + "\r\nMust be an integer value 1, 2 or 3");
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return (outputCorrectlySet & voltSlowRampSuccess);
            }
        }

        static public String CheckCurrent(int outputChan)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupReturnData.Clear();
                String PowerSupCurrent = null;
                if (Command("instrument:select CH" + outputChan.ToString()))
                    if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))//verify command was received
                    {
                        PowerSupCurrent = Query("measure:current?");
                    }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return PowerSupCurrent;
            }
        }

        static public String CheckVoltage(int outputChan)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupReturnData.Clear();
                String PowerSupVoltage = null;
                if (Command("instrument:select CH" + outputChan.ToString()))
                    if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))//verify command was received
                    {
                        PowerSupVoltage = Query("measure:voltage?");
                    }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return PowerSupVoltage;
            }
        }

        static public bool ChangeVoltAndOrCurrOutput(int outputChan, double setVolt, double setCurrLimit)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                bool rtnData = false;
                try
                {
                    if (Command("instrument:select CH" + outputChan.ToString()))
                        if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))
                        {
                            if (Command("source:apply CH" + outputChan.ToString() + "," + setVolt.ToString() + "," + setCurrLimit.ToString()))
                            {
                                String tmpQuery = Query("source:apply? CH" + outputChan.ToString());
                                if ((tmpQuery.Contains(setVolt.ToString())) && (tmpQuery.Contains(setCurrLimit.ToString())))
                                {
                                    String tmpVolt = Query("measure:voltage?");
                                    pwrSupReturnData.Add("Channel #" + outputChan.ToString() + " output voltage set to: " + tmpVolt + "V, " + setCurrLimit.ToString() + "A");
                                    rtnData = true;
                                }
                            }
                        }
                }
                catch(Exception ex)
                {
                    pwrSupReturnData.Add("Exception occurred in 'ChangeVoltageOutput' method\r\n" + ex.Message);
                }
                //check to see that all power supply commands were successful
                if(!rtnData)
                {
                    String tmpVolt = Query("measure:voltage?");
                    pwrSupReturnData.Add("Failed to set voltage/current\r\nChannel #" + outputChan.ToString() + " output voltage set to: " + tmpVolt + "V, " + setCurrLimit.ToString() + "A");
                }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return rtnData;
            }
        }

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Will set the power supply voltage limit only if the values passed are under the class member variables that set the absolute maximum values
        /// </summary>
        /// <param name="ch1Lim"></param>
        /// <param name="ch2Lim"></param>
        /// <param name="ch3Lim"></param>
        /// <returns></returns>
        static public bool SetPwrSupVoltLimits(double ch1Lim, double ch2Lim, double ch3Lim)
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                pwrSupReturnData.Clear();
                bool ch1RtnStatus = false;
                bool ch2RtnStatus = false;
                bool ch3RtnStatus = false;
                //make sure the voltage isn't set above the UUT +28v & fixture +5V
                if (ch1Lim > ch1VoltLim)
                    ch1Lim = ch1VoltLim;
                if (ch2Lim > ch2VoltLim)
                    ch2Lim = ch2VoltLim;
                if (ch3Lim > ch3VoltLim)
                    ch3Lim = ch3VoltLim;
                try
                {
                    if (Command("instrument:select CH1"))
                        if (Query("instrument:select?").Contains("CH1"))
                            if (Command("voltage:protection:state 1"))
                                if ((Query("voltage:protection:state?")).Contains("1"))
                                    if (Command("voltage:protection " + ch1VoltLim.ToString()))
                                        if ((Query("voltage:protection?")).Contains(ch1VoltLim.ToString()))
                                        {
                                            ch1RtnStatus = true;
                                            pwrSupReturnData.Add("Power supply Ch1 limit set to " + ch1VoltLim.ToString() + "V");
                                        }

                    if (Command("instrument:select CH2"))
                        if (Query("instrument:select?").Contains("CH2"))
                            if (Command("voltage:protection:state 1"))
                                if ((Query("voltage:protection:state?")).Contains("1"))
                                    if (Command("voltage:protection " + ch2VoltLim.ToString()))
                                        if ((Query("voltage:protection?")).Contains(ch2VoltLim.ToString()))
                                        {
                                            ch2RtnStatus = true;
                                            pwrSupReturnData.Add("Power supply Ch2 limit set to " + ch2VoltLim.ToString() + "V");
                                        }

                    if (Command("instrument:select CH3"))
                        if (Query("instrument:select?").Contains("CH3"))
                            if (Command("voltage:protection:state 1"))
                                if ((Query("voltage:protection:state?")).Contains("1"))
                                    if (Command("voltage:protection " + ch3VoltLim.ToString()))
                                        if ((Query("voltage:protection?")).Contains(ch3VoltLim.ToString()))
                                        {
                                            ch3RtnStatus = true;
                                            pwrSupReturnData.Add("Power supply Ch3 limit " + ch3VoltLim.ToString() + "V");
                                        }
                }
                catch (Exception ex)
                {
                    pwrSupReturnData.Add("Exception occurred in 'SetPwrSupVoltLimits' method\r\n" + ex.Message);
                }
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return (ch1RtnStatus & ch2RtnStatus & ch3RtnStatus);
            }
        }

        #endregion Public Class Methods

        /// <summary>
        /// Private methods prevent other classes/threads making simultaneous calls to the methods.  
        /// Outside classes gain access to the power supply using public methods
        /// </summary>
        /// <param name="setVolt"></param>
        /// <returns></returns>
        #region Private Methods
        
        static private bool RampVoltage(double setVolt)
        {
            bool rtnStatus = false;
            //slowly ramp up voltage
            double voltRampStep = .5;
            int numOfSteps = 0;
            if (voltRampStep < setVolt)
                numOfSteps = (int)(setVolt / voltRampStep);
            //set the voltage to increase in steps of voltRampStep;

            try
            {
                if (Command("source:voltage:level:immediate:step:increment " + voltRampStep.ToString()))
                    if (Query("source:voltage:level:immediate:step:increment?").Contains(voltRampStep.ToString()))
                        for (int i = 0; i < numOfSteps; i++)
                        {
                            if (!Command("voltage:up"))
                                pwrSupReturnData.Add("Command Error while ramping up power supply output voltage");
                        }
                System.Threading.Thread.Sleep(500);
                //regardless of whether the ramp works, set the output voltage
                //the previous steps that ramp the voltage is an extra precaution to prevent inrush current
                if (Command("voltage " + setVolt.ToString()))
                    rtnStatus = true;
                else
                {
                    pwrSupReturnData.Add("Command Error while enabling output voltage");
                    Command("source:channel:output:state 0");
                }
            }
            catch(Exception ex)
            {
                pwrSupReturnData.Add("Exception occurred in 'RampVoltage' method\r\n" + ex.Message);
            }
            

            return rtnStatus;
        }

        static private String Query(String cmd)
        {            
            String rtnData = null;
            try
            {
                pwrSupDev.WriteLine(cmd);
                rtnData = pwrSupDev.ReadLine();
            }
            catch (TimeoutException)
            {
                pwrSupReturnData.Add("Timeout while waiting for power supply to respond to Query");
            }
            catch(Exception ex)
            {
                pwrSupReturnData.Add("Exception occurred in 'Query' method\r\n" + ex.Message);
            }
            return rtnData;
        }

        static private bool Command(String cmd)
        {
            bool rtnStatus = false;
            try
            {
                //be sure the device is in remote mode before sending a command
                pwrSupDev.WriteLine("system:remote");
                pwrSupDev.WriteLine(cmd);
                if (NoError())
                    rtnStatus = true;
                else
                    pwrSupReturnData.Add("Error occurred in power supply 'Command' method");
            }
            catch(Exception ex)
            {
                pwrSupReturnData.Add("Exception occurred in 'Command' method\r\n" + ex.Message);
            }
            return rtnStatus;
        }

        static private bool ConfigurePwrSup()
        {
            bool rtnStatus = false;
            
                
            try
            {
                if (Command("*cls"))//clear any errors
                if (Command("system:remote"))
                        rtnStatus = true;
            }
            catch(Exception ex)
            {
                pwrSupReturnData.Add("Exception occurred in 'ConfigurePwrSup' method\r\n" + ex.Message);
            }
            return rtnStatus;
        }

        static private bool NoError()
        {
            bool rtnStatus = false;
            try
            {
                String pwrSupError = Query("system:error?");
                if (pwrSupError.Contains("No error"))
                    rtnStatus = true;
                else
                {
                    pwrSupReturnData.Add("Power Supply Error: " + pwrSupError);
                    //clear the error
                    pwrSupDev.WriteLine("*cls");
                }
            }
            catch(Exception ex)
            {
                pwrSupReturnData.Add("Exception occurred in 'NoError' method\r\n" + ex.Message);
            }
            return rtnStatus;
        }

        static public bool OVP_Check()
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                bool rtnStatus = false;
                String tempStr = Query("source:voltage:protection:triped?");
                if (tempStr == null)
                    rtnStatus = false;
                else if (tempStr.Contains("1"))
                    rtnStatus = true;
                else
                    rtnStatus = false;
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
                return rtnStatus;
            }
        }

        static public void ClearOVP()
        {
            lock (lockRoutine)
            {
                IsBusy = true;
                System.Threading.Thread.Sleep(250);
                Command("source:voltage:protection:clear");
                System.Threading.Thread.Sleep(1500);
                IsBusy = false;
            }
        }

        #endregion Private Methods

    }
}
