using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.DAQmx;

namespace HydroFunctionalTest
{
    class UsbToGpio
    {
        private const string gpioProdType = "USB-6501";
        /// <summary>
        /// The ScanForDevs method Saves the device names here
        /// </summary>
        public List<string> gpioDeviceIds;
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        public List<string> gpioReturnData;

        #region Contructor/Destructor
        public UsbToGpio()
        {
            gpioDeviceIds = new List<string>();
            gpioReturnData = new List<string>();
        }
        #endregion Contructor/Destructor

        #region Member Methods

        #region ScanForDevs Method
        /// <summary>
        /// Intialization of GPIO adapter.  Find attached gpio devices, ignore all others
        /// </summary>
        /// <returns></returns>        
        public bool ScanForDevs()
        {
            bool requestSuccessful = false;
            gpioReturnData.Clear();

            try
            {
                string[] attachedDevices = DaqSystem.Local.Devices;
                if (attachedDevices.Length <= 0)
                {
                    gpioReturnData.Add("No GPIO devices were found.");
                }
                else
                {                    
                    int index = 0;
                    foreach (string s in attachedDevices)
                    {
                        NationalInstruments.DAQmx.Device deviceObject = DaqSystem.Local.LoadDevice(attachedDevices[index]);
                        //string devID = deviceObject.DeviceID;
                        //long serialNum = deviceObject.SerialNumber;
                        
                        string prodNum = deviceObject.ProductType;                       
                        if (prodNum.Contains(gpioProdType))
                        {
                            gpioDeviceIds.Add(attachedDevices[index]);
                        }
                        deviceObject.Dispose();
                        index++;
                    }
                    requestSuccessful = true;
                    gpioReturnData.Add("GPIO devices found, see 'gpioDeviceIds' for list of connected devices.");                  
                }
            }
            catch (DaqException ex)
            {
                gpioReturnData.Add("Error while initializing the GPIO adapter." + Environment.NewLine + ex.Message);
                requestSuccessful = false;
            }
            
            return requestSuccessful;
        }
        #endregion ScanForDevs Method

        #region GpioWrite Method
        /// <summary>
        /// set/clear pins, parameters must include the desired device (gpioDeviceIds) as well as port/pin numbers to be set/cleared.
        ///Pin to set is optional if opting to set all pins on the entire port.
        /// </summary>
        /// <param name="devName"></param>
        /// <param name="portNum"></param>
        /// <param name="setClearBits"></param>
        /// <param name="pinNum"></param>
        /// <returns></returns>
        public bool GpioWrite(string devName, UInt32 portNum, UInt32 setClearBits, UInt32 pinNum = 8)
        {
            bool requestSuccessful = false;
            gpioReturnData.Clear();

            try
            {
                NationalInstruments.DAQmx.Device deviceObject = DaqSystem.Local.LoadDevice(devName);
                using (NationalInstruments.DAQmx.Task digitalWriteTask = new NationalInstruments.DAQmx.Task())
                {
                    //Determine whether an entire port will be set or just a single pin                    
                    string gpioLines;
                    if (pinNum > 7)
                        gpioLines = devName + "/port" + portNum.ToString();
                    else
                        gpioLines = devName + "/port" + portNum.ToString() + "/line" + pinNum.ToString();
                    //  Create an Digital Output channel and name it.
                    digitalWriteTask.DOChannels.CreateChannel(gpioLines, "", ChannelLineGrouping.OneChannelForAllLines);

                    //  Write digital port data. WriteDigitalSingChanSingSampPort writes a single sample
                    //  of digital data on demand, so no timeout is necessary.
                    DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
                    writer.WriteSingleSamplePort(true, setClearBits);
                    gpioReturnData.Add("GPIO Write Action Details:" + Environment.NewLine + "Device = " + devName.ToString() + ", Port #" +
                        portNum.ToString() + ", Pin(opt) #" + pinNum.ToString() + ", Set/Clear Value: " + setClearBits.ToString());
                }

            }
            catch(DaqException ex)
            {
                gpioReturnData.Add("Error while writing to the GPIO adapter." + Environment.NewLine + ex.Message);
                requestSuccessful = false;
            }

            return requestSuccessful;
        }
        #endregion GpioWrite Method

        #region GpioRead Method
        /// <summary>
        /// Querys the device and returns either a bit or a byte based on the optional method parameter
        /// </summary>
        /// <param name="devName"></param>
        /// <param name="portNum"></param>
        /// <param name="pinNum"></param>
        /// <returns></returns>
        public UInt32 GpioRead(string devName, UInt32 portNum, UInt32 pinNum = 8)
        {
            UInt32 returnData = 0xFF;
            gpioReturnData.Clear();

            try
            {
                using (NationalInstruments.DAQmx.Task digitalReadTask = new NationalInstruments.DAQmx.Task())
                {
                    //Determine whether an entire port will be read or just a single pin                    
                    string gpioLines;
                    if (pinNum > 7)
                        gpioLines = devName + "/port" + portNum.ToString();
                    else
                        gpioLines = devName + "/port" + portNum.ToString() + "/line" + pinNum.ToString();
                    //  Create an Digital Output channel and name it.
                    digitalReadTask.DIChannels.CreateChannel(gpioLines, "", ChannelLineGrouping.OneChannelForAllLines);

                    DigitalSingleChannelReader reader = new DigitalSingleChannelReader(digitalReadTask.Stream);
                    returnData = reader.ReadSingleSamplePortUInt32();
                    gpioReturnData.Add("GPIO return data: " + Environment.NewLine + returnData.ToString() + Environment.NewLine + 
                        "Device = " + devName.ToString() + ", Port #" + portNum.ToString() + ", Pin(opt) #" + pinNum.ToString());
                }
            }
            catch (DaqException ex)
            {
                gpioReturnData.Add("Error while writing to the GPIO adapter." + Environment.NewLine + ex.Message);
            }
            return returnData;
        }
        # endregion GpioRead Method

        #endregion Member Methods
    }

}
