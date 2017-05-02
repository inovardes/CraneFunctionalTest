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
        public bool IsBusy { get; set; }

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

        public bool InitializeDmm(string comPortName)
        {
            bool rtnValue = false;
            dmmDev = new SerialPort();
            try
            {
                dmmDev.PortName = comPortName;
                dmmDev.BaudRate = 115200;
                dmmDev.DataBits = 8;
                dmmDev.ReadTimeout = 250;
                dmmDev.WriteTimeout = 250;
                dmmDev.Open();
                if(dmmDev.IsOpen)
                {
                    try
                    {
                        dmmDev.Write("*IDN?\r\n");
                        String tmpReadData = "";
                        int loopCount = 0;
                        while((!rtnValue) && (loopCount < 8))
                        {
                            if(loopCount > 0)
                            {
                                dmmDev.Write("*IDN?");
                                tmpReadData = dmmDev.ReadLine();
                            }
                            else
                                tmpReadData = dmmDev.ReadLine();

                            if (tmpReadData.Contains("5492B"))
                                rtnValue = true;

                            loopCount++;
                        }
                    }
                    catch (TimeoutException)
                    {
                        dmmReturnData.Add("Timeout while querying comport in initialize Dmm method");
                        dmmDev.Close();
                    }                            
                }
                else
                {
                    dmmReturnData.Add("Comport not associated with DMM");
                }
                //dmmReturnData.Add("DMM comport setup successful");
            }
            catch (Exception ex)
            {
                dmmReturnData.Add("Communication error while attempting to initialize DMM\r\n" + ex.Message);
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
    }
}
