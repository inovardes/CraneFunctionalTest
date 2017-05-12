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
        /// The ScanForDevs method Saves the device names to List elements for the Main UI to view
        /// </summary>
        public List<string> gpioDeviceIds;
        /// <summary>
        /// Once 
        /// </summary>
        private string DeviceId { get; set; }
        /// <summary>
        /// Stores method operation results to List elements for the Main UI to view
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
        public bool ScanForDevs(int fixtPos)
        {
            bool requestSuccessful = false;
            gpioReturnData.Clear();
            gpioDeviceIds.Clear();

            try
            {
                string[] attachedDevices = DaqSystem.Local.Devices;
                if (attachedDevices.Length <= 0)
                {
                    gpioReturnData.Add("No GPIO devices were found.");
                }
                else
                {
                    gpioReturnData.Add("GPIO devices found:");
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
                        gpioReturnData.Add(s);

                        //check to see what fixture this device belongs to by checking the pullup and pulldown on the fixture (port 1, pin 0)
                        DeviceId = s;   //Read and write functions use DeviceId to communicate with devices (by default any connected device will be assigned to fixture 1)
                        if ((GpioRead(1, 0) == 0) & (fixtPos == 2))
                        {
                            DeviceId = s;
                            requestSuccessful = true;
                            deviceObject.Dispose();
                            break;
                        }
                        else if ((GpioRead(1, 0) == 1) & (fixtPos == 1))
                        {
                            DeviceId = s;
                            requestSuccessful = true;
                            deviceObject.Dispose();
                            break;
                        }
                        else
                            DeviceId = null;

                        deviceObject.Dispose();
                        index++;
                    }
                    
                }
            }
            catch (DaqException ex)
            {
                gpioReturnData.Add("Error while initializing the GPIO adapter." + Environment.NewLine + ex.Message);
            }
            
            return requestSuccessful;
        }
        #endregion ScanForDevs Method

        public string GetDeviceId()
        {
            return DeviceId;
        }

        #region GpioWrite Method
        /// <summary>
        ///  Commands GPIO to set/clear pins.  Parameters must include the pin status (either an 8 bit value or just 1 bit), and then the port number and optional pin number to be set/cleared.
        ///  Pin to set is optional if opting to set the entire port with an 8 bit value.  pinNum by default = 8 to allow for ommitting pin number when setting entire port (no such pin number = 8)
        /// </summary>
        /// <param name="devName"></param>
        /// <param name="portNum"></param>
        /// <param name="byteValue"></param>
        /// <param name="pinNum"></param>
        /// <returns></returns>
        public bool GpioWrite(UInt32 portNum, UInt32 byteValue)
        {
            bool requestSuccessful = false;
            gpioReturnData.Clear();

            try
            {
                NationalInstruments.DAQmx.Device deviceObject = DaqSystem.Local.LoadDevice(DeviceId);
                using (NationalInstruments.DAQmx.Task digitalWriteTask = new NationalInstruments.DAQmx.Task())
                {
                    //Determine whether an entire port will be set or just a single pin                    
                    string gpioLines = DeviceId + "/port" + portNum.ToString();
                    //  Create an Digital Output channel and name it.
                    digitalWriteTask.DOChannels.CreateChannel(gpioLines, "", ChannelLineGrouping.OneChannelForAllLines);

                    //  Write digital port data. WriteDigitalSingChanSingSampPort writes a single sample
                    //  of digital data on demand, so no timeout is necessary.
                    DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
                    writer.WriteSingleSamplePort(true, (UInt32)byteValue);
                    gpioReturnData.Add("GPIO Write Action Details:" + Environment.NewLine + "Device = " + DeviceId.ToString() + ", Port #" +
                        portNum.ToString() + ", Set/Clear Value: " + byteValue.ToString());
                }

            }
            catch(DaqException ex)
            {
                gpioReturnData.Add("Error while writing to the GPIO adapter." + Environment.NewLine + ex.Message);
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
        public UInt32 GpioRead(UInt32 portNum, UInt32 pinNum = 8)
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
                        gpioLines = DeviceId + "/port" + portNum.ToString();
                    else
                        gpioLines = DeviceId + "/port" + portNum.ToString() + "/line" + pinNum.ToString();
                    //  Create an Digital Output channel and name it.
                    digitalReadTask.DIChannels.CreateChannel(gpioLines, "", ChannelLineGrouping.OneChannelForAllLines);

                    DigitalSingleChannelReader reader = new DigitalSingleChannelReader(digitalReadTask.Stream);
                    returnData = (reader.ReadSingleSamplePortUInt32() & 0xFF);
                    //by default the return data is a byte value that must be converted to a bit representation
                    int temp;
                    if (pinNum != 8)
                    {
                        temp = (int)returnData >> (int)pinNum;
                        returnData = (uint)temp;
                    }
                        
                    gpioReturnData.Add("Read from GPIO " + DeviceId.ToString() + ", Port" + portNum.ToString() + ", Pin(opt) " + pinNum.ToString() + 
                        Environment.NewLine + "Value = " + returnData.ToString());
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
