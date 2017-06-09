using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;

namespace HydroFunctionalTest
{
    static class Eload
    {
        static private string comPort = "9";
        public const string python = @"C:\Python25\python.exe";  // path to python interpreter
        static public string myPythonApp = "C:\\CraneFunctionalTest\\AccessELoad.py";      // python app to call
        public const string connType = "obj";                    // always keep this as 'obj' for connection type
        public const string baudRate = "9600";                   // should always be 9600 baud for the 8500 model
        static public List<String> returnData = new List<String>();
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the device
        /// </summary>
        static public bool IsBusy { get; private set; } = false;
        
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the IsBusy property.  
        /// Together they manage access to the Eload resource, preventing simultaneous requests.
        /// </summary>
        static private Object lockRoutine = new Object();

        /// <summary>
        /// Provides a way for threads to reserve the Eload which allows other threads to know whether to run other tests while Eload in in use.
        /// </summary>
        /// <param name="eloadRequested"></param>
        /// <returns></returns>
        static public bool ReserveEload(bool eloadRequested)
        {
            lock (lockRoutine)
            {
                if (eloadRequested)
                    if (IsBusy)
                        return false;
                    else
                    {
                        IsBusy = true;
                        return true;
                    }
                else
                {
                    IsBusy = false;
                    return false;
                }
            }
        }

        static public bool TalkToLoad(String tempComPort)
        {
            lock (lockRoutine)
            {
                comPort = tempComPort.Substring(3);
                bool rtnStatus = false;
                returnData.Clear();
                // parameters to send python app
                string method = "check";    // method (setup, read, toggle, or access)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //read
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method;

                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}", connType, comPort, baudRate, method);
                // start the process 
                myProcess.Start();

                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                myString = myString + "Verify Eload Settings:\r\nAddress=0\r\nBaudrate=" + baudRate.ToString() + "\r\nParity=Default";
                if (myString.Contains("true") && (myString.Contains(baudRate)))
                    rtnStatus = true;
                else
                    returnData.Add(myString);

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();

                return rtnStatus;
            }
        }

        static public bool Setup(string mode, float modeValue)
        {
            lock (lockRoutine)
            {
                bool rtnStatus = false;
                returnData.Clear();

                // parameters to send python app
                string method = "setup";    // method (setup, read, or toggle)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //setup
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method + " " + mode + " " + modeValue.ToString();
                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}, {4}, {5}", connType, comPort, baudRate, method, mode, value);
                // start the process 
                myProcess.Start();

                // Read the standard output of the app we called.  
                // in order to avoid deadlock we will read output first 
                // and then wait for process terminate: 
                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                returnData.Add(myString); //save the output we got from python app

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();

                return rtnStatus;
            }
        }

        static public bool SetMaxCurrent(double maxCurrent)
        {
            lock (lockRoutine)
            {
                bool rtnStatus = false;
                returnData.Clear();
                // parameters to send python app
                string method = "setMaxCurrent";    // method (setup, read, or toggle)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //read
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method + " " + maxCurrent.ToString();
                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}", connType, comPort, baudRate, method);
                // start the process 
                myProcess.Start();

                // Read the standard output of the app we called.  
                // in order to avoid deadlock we will read output first 
                // and then wait for process terminate: 
                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                if (myString.Contains("true") && (myString.Contains(maxCurrent.ToString())))
                    rtnStatus = true;
                returnData.Add(myString); //save the output we got from python app 

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();

                return rtnStatus;
            }
        }

        static public bool SetMaxVoltage(double maxVoltage)
        {
            lock (lockRoutine)
            {
                bool rtnStatus = false;
                returnData.Clear();
                // parameters to send python app
                string method = "setMaxVoltage";    // method (setup, read, or toggle)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //read
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method + " " + maxVoltage.ToString();
                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}", connType, comPort, baudRate, method);
                // start the process 
                myProcess.Start();

                // Read the standard output of the app we called.  
                // in order to avoid deadlock we will read output first 
                // and then wait for process terminate: 
                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                if (myString.Contains("true") && (myString.Contains(maxVoltage.ToString())))
                    rtnStatus = true;
                returnData.Add(myString); //save the output we got from python app 

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();

                return rtnStatus;
            }
        }

        static public void Read(out double measuredVoltage, out double measuredCurrent)
        {
            lock (lockRoutine)
            {
                returnData.Clear();
                Console.WriteLine("reading from E-Load...");
                // parameters to send python app
                string method = "read";    // method (setup, read, or toggle)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //read
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method;
                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}", connType, comPort, baudRate, method);
                // start the process 
                myProcess.Start();

                // Read the standard output of the app we called.  
                // in order to avoid deadlock we will read output first 
                // and then wait for process terminate: 
                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                returnData.Add(myString); //save the output we got from python app 

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();

                //indexes for accessing data returned in the string
                int vIndex = myString.IndexOf("V");
                int aIndex = myString.IndexOf("A");
                //int wIndex = myString.IndexOf("W");

                // measured voltage
                measuredVoltage = double.Parse(myString.Substring(0, vIndex));         // get voltage string and convert to float

                // measured current
                String tempString = myString.Substring(vIndex + 2, (aIndex - vIndex)-3);
                measuredCurrent = double.Parse(myString.Substring(vIndex + 2, (aIndex - vIndex)-3));
            }
        }

        static public void Toggle(string state)
        {
            lock (lockRoutine)
            {
                returnData.Clear();
                Console.WriteLine("toggling E-Load " + state);
                // parameters to send python app
                string method = "toggle";    // method (setup, read, or toggle)

                // Create new process start info
                ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

                // make sure we can read the output from stdout 
                myProcessStartInfo.UseShellExecute = false;
                myProcessStartInfo.RedirectStandardOutput = true;
                myProcessStartInfo.CreateNoWindow = true;

                //toggle
                myProcessStartInfo.Arguments = myPythonApp + " " + connType + " " + comPort + " " + baudRate + " " + method + " " + state;

                Process myProcess = new Process();
                // assign start information to the process 
                myProcess.StartInfo = myProcessStartInfo;

                //Console.WriteLine("Calling Python script with arguments: {0}, {1}, {2}, {3}, {4}", connType, comPort, baudRate, method, state);
                // start the process 
                myProcess.Start();

                // Read the standard output of the app we called.  
                // in order to avoid deadlock we will read output first 
                // and then wait for process terminate: 
                StreamReader myStreamReader = myProcess.StandardOutput;
                string myString = myStreamReader.ReadToEnd();
                returnData.Add(myString); //save the output we got from python app

                // wait exit signal from the app we called and then close it. 
                myProcess.WaitForExit();
                myProcess.Close();
            }
        }
    }
}
