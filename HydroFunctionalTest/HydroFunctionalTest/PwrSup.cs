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
        static private SerialPort pwrSupDev;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        static public List<string> pwrSupReturnData = new List<string>();
        static private Object lockRoutine = new Object();
        static public bool IsBusy { get; private set; } = false;
        static public String modelNum = "9131B";
        static public double ch1VoltLim = 29;
        static public double ch2VoltLim = 29;
        static public double ch3VoltLim = 5;
        static public double ch1CurrLim = 1.5;
        static public double ch2CurrLim = 1.5;
        static public double ch3CurrLim = 3;
        static public double maxOutputVoltDrop = 1;

        #endregion Class members

        #region Public Class Methods
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
                                //reset device for future query
                                //pwrSupDev.Write("*RST\r\n");
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
                IsBusy = false;
                return rtnStatus;
            }
        }

        static public void CloseComport()
        {
            pwrSupDev.Close();
        }

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
                IsBusy = false;
                return rtnStatus;
            }
        }

        //static public bool OVP_Tripped(int outputChan)
        //{
        //    lock(lockOVP_Tripped)
        //    {
        //        pwrSupReturnData.Clear();
        //        bool rtnStatus = false;
        //        if (Command("instrument:select CH" + outputChan.ToString()))
        //            if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))
        //                if (Query("voltage:protection:state?").Contains("1"))
        //                {
        //                    rtnStatus = true;
        //                    pwrSupReturnData.Add("OVP On");
        //                    Command("source:channel:output:state 0");
        //                    if (Command("voltage:protection:state 0"))
        //                        if (Query("voltage:protection:state?").Contains("1"))
        //                            pwrSupReturnData.Add("Failed to Clear OVP");
        //                }
        //        return rtnStatus;
        //    }
        //}

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
                if (outputChan == 3)
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
                        if (Command("instrument:select CH" + outputChan.ToString()))
                            if (Query("instrument:select?").Contains("CH" + outputChan.ToString()))
                            {
                                if (Command("source:apply CH" + outputChan.ToString() + "," + startVoltage.ToString() + "," + setCurrLimit.ToString()))
                                {
                                    String tmpQuery = Query("source:apply? CH" + outputChan.ToString());
                                    if ((tmpQuery.Contains(startVoltage.ToString())) && (tmpQuery.Contains(setCurrLimit.ToString())))
                                        if (Command("source:channel:output:state " + convertedBool.ToString()))
                                            if (Query("source:channel:output:state?").Contains(convertedBool.ToString()))
                                                outputCorrectlySet = true;
                                }
                            }
                        if (turnOn)
                        {
                            voltSlowRampSuccess = RampVoltage(setVolt);
                            String tmpVolt = Query("measure:voltage?");
                            pwrSupReturnData.Add("Channel #" + outputChan.ToString() + " output voltage set to: " + tmpVolt + "V, " + setCurrLimit.ToString() + "A");
                        }
                        else if(!outputCorrectlySet)
                            pwrSupReturnData.Add("Problem turning on power supply Channel #" + outputChan.ToString());
                        else
                        {
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
                IsBusy = false;
                return (outputCorrectlySet & voltSlowRampSuccess);
            }
        }

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
                IsBusy = false;
                return (ch1RtnStatus & ch2RtnStatus & ch3RtnStatus);
            }
        }

        #endregion Public Class Methods

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
                //the ramp is a extra precaution to prevent inrush current
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
            
            //clear any errors
            try
            {
                if (Command("*cls"))
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

        #endregion Private Methods

    }
}
