using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace HydroFunctionalTest
{
    class ProgrammingControl
    {
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the JtagIsBusy property.  
        /// Together they manage access to the ProgramBootloader method, preventing multiple thread simultaneous access.
        /// </summary>
        static private Object lockJtagRoutine = new Object();
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the AutoItIsBusy property.  
        /// Together they manage access to the LoadFirmwareViaAutoit method, preventing multiple thread simultaneous access.
        /// </summary>
        static private Object lockAutoItRoutine = new Object();
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the ProgramBootloader method
        /// </summary>
        static public bool AutoItIsBusy { get; private set; } = false;
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the LoadFirmwareViaAutoit method
        /// </summary>
        static public bool JtagIsBusy { get; private set; } = false;

        //file path and file names
        /// <summary>
        /// Path location to the .cfg & .bin files used to flash the bootloader.  There must be a different .cfg and .bin file for every assembly
        /// </summary>
        private const string jtagPrgmrFilePath = "Olimex OpenOCD\\openocd-0.8.0\\bin-x64";
        /// <summary>
        /// The name of the AutoIt script that will be used to automate the loading of the firmware into the device under test
        /// </summary>
        private const string autoItExe = "LoadFirmwareViaAutoIt.exe";

        #region//ProgramBootloader

        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Runs the cmd script that initiates programming via JTAG.  The mainTestProgmPath is the root folder where the Olimex software and other files are located.  
        /// The cfgFileName is simply the class object instance type (e.g., PSM85307) which will be converted into a string.  This string needs to match the actual
        /// name of the .cfg file that is used to setup/connect to the JTAG device which then calls actual bootloader .bin file which needs to reside in the same folder location
        /// For example: the 'PSM_85307.cfg' file is used in the command arguments to begin the programming.  Within the 'PSM_85307.cfg' file is a reference to the actual
        /// bootloader .bin file.  If the actual .bin file has a different name than what is referenced in the .cfg file, the programming will fail due to not finding the file
        /// </summary>
        /// <param name="mainTestPrgmPath"></param>
        /// <param name="cfgFileName"></param>
        /// <returns></returns>
        static public Dictionary<bool, String> ProgramBootloader(String bootSourceFilePath, Object cfgFileName, UsbToGpio gpioObj)
        {
            lock (lockJtagRoutine)
            {
                JtagIsBusy = true;
                Dictionary<bool, String> rtnData = new Dictionary<bool, string>();
                rtnData.Add(false, "ProgramBootloader Method failed to run completely through");
                string output = "";
                try
                {
                    //Create the process
                    var programmer = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = bootSourceFilePath + jtagPrgmrFilePath,
                            FileName = bootSourceFilePath + jtagPrgmrFilePath + "\\" + "openocd-x64-0.8.0.exe",
                            Arguments = "-f olimex-arm-usb-tiny-h.cfg -f " + cfgFileName.ToString().Substring(cfgFileName.ToString().IndexOf(".") + 1) + ".cfg",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    //Start the process
                    programmer.Start();
                    //Read all the output (for some reason the programmer outputs to the StandardError Output)
                    output = programmer.StandardError.ReadToEnd();
                    //Wait for the process to finish
                    programmer.WaitForExit();
                    //Did it pass?
                    if (output.Contains("** Programming Finished **"))
                    {
                        rtnData.Clear();
                        rtnData.Add(true, "JTAG Programming Passed");
                    }
                    else
                    {
                        rtnData[false] = "Failed to program bootloader\r\n" + output;
                    }
                }
                catch (Exception ex)
                {
                    rtnData[false] = "Exception while programming bootloader\r\n" + ex.Message;
                }
                JtagIsBusy = false;
                return rtnData;
            }
        }
        #endregion

        #region//LoadFirmwareViaAutoit
        /// <summary>
        /// Method uses 'lock' to prevent simultaneous access from multiple threads.  Check 'IsBusy' to avoid having to wait for the device to become available.
        /// Calls the AutoIt program which will open Crane's software utility to program the firmware into the device using mouse and keyboard macros
        /// No feedback returns from Crane's software, so indication of successsful programming is unknown.  The method will expect to see the instance (uutSpecificArgument)
        /// type sent which will be converted to a string for the command line arguments.  This string will instruct the AutoIt script to select the
        /// appropriate firmware file to use and other unique actions required for the assembly to be programmed.
        /// </summary>
        /// <param name="mainTestPrgmPath"></param>
        /// <param name="uutSpecificArguments"></param>
        /// <returns></returns>
        static public Dictionary<bool, String> LoadFirmwareViaAutoit(String autoItScriptPath, Object uutSpecificArgument)
        {
            lock (lockAutoItRoutine)
            {
                AutoItIsBusy = true;
                Dictionary<bool, String> rtnData = new Dictionary<bool, string>();
                rtnData.Add(false, "LoadFirmwareViaAutoit Method failed to run completely through");
                string output = "";
                try
                {
                    //Create the process
                    var autoit = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = autoItScriptPath,
                            FileName = autoItScriptPath + autoItExe,
                            Arguments = uutSpecificArgument.ToString().Substring(uutSpecificArgument.ToString().IndexOf(".") + 1),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    //Start the process
                    autoit.Start();
                    //Read all the output (for some reason the programmer outputs to the StandardError Output)
                    output = autoit.StandardError.ReadToEnd();
                    //Wait for the process to finish
                    autoit.WaitForExit();
                    //Did it pass?
                    if (autoit.ExitCode == 1)
                    {
                        rtnData.Clear();
                        rtnData.Add(true, "Load Test Firmware Pass");
                    }
                    else
                    {
                        rtnData[false] = output + "\r\n\r\nFailed to load test firmware\r\n";
                    }
                }
                catch (Exception ex)
                {
                    rtnData[false] = "Exception occured while Loading Firmware\r\n" + ex.Message;
                }
                AutoItIsBusy = false;
                return rtnData;
            }
        }
        #endregion
    }
}
