using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Diagnostics;

namespace HydroFunctionalTest
{
    class PwrSup
    {

        #region Class members
        private SerialPort dmmDev;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        public List<string> pwrSupReturnData = new List<string>();
        //private bool isBusy = false;
        public bool IsBusy { get; set; }

        #endregion Class members

        #region Class Methods
        /// <summary>
        /// Contructor
        /// </summary>
        public PwrSup()
        {

        }
        #endregion Class Methods

        #region Class Methods       

        public bool InitializePwrSup(string comPortName)
        {
            bool rtnValue = false;
            dmmDev = new SerialPort();
            try
            {
                dmmDev.PortName = comPortName;
                dmmDev.BaudRate = 115200;
                dmmDev.DataBits = 8;
                dmmDev.ReadTimeout = 500;
                dmmDev.WriteTimeout = 500;
                dmmDev.Open();
                if (dmmDev.IsOpen)
                {
                    try
                    {
                        dmmDev.Write("*IDN?\r\n");
                        for (int countLoop = 0; countLoop < 6; countLoop++)
                        {
                            String tmpReadData = dmmDev.ReadLine();
                            if (tmpReadData.Contains("9131B"))
                                rtnValue = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        pwrSupReturnData.Add("Timeout while querying comport in initialize Power Supply method");
                        dmmDev.Close();
                    }
                }
                else
                {
                    pwrSupReturnData.Add("Comport not associated with Power Supply");
                }
                //dmmReturnData.Add("DMM comport setup successful");
            }
            catch (Exception ex)
            {
                pwrSupReturnData.Add("Communication error while attempting to initialize Power supply\r\n" + ex.Message);
                rtnValue = false;
                dmmDev.Close();
            }
            return rtnValue;
        }

        public void CloseComport()
        {
            dmmDev.Close();
        }

        public String Read()
        {
            String rtnData = "";


            return rtnData;
        }

        public String Command()
        {
            String rtnData = "";


            return rtnData;

        }

        #endregion Class Methods

    }
}
