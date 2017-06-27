using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace HydroFunctionalTest
{
    static class UutComm
    {
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the JtagIsBusy property.  
        /// Together they manage access to the ProgramBootloader method, preventing multiple thread simultaneous access.
        /// </summary>
        static private Object lockRoutine = new Object();
        /// <summary>
        /// this object is used in conjunction with the 'lock()' statement and the AutoItIsBusy property.  
        /// Together they manage access to the LoadFirmwareViaAutoit method, preventing multiple thread simultaneous access.
        /// </summary>
        static private Object lockComPortBusy = new Object();
        /// <summary>
        /// Provides a public read boolean for external classes to allow for efficient access to the ProgramBootloader method
        /// </summary>
        static public bool ComPortBusy { get; private set; } = false;

        /// <summary>
        /// Allows multiple threads to check the ComPortBusy status flag to avoid race conditions
        /// </summary>
        /// <returns></returns>
        static public bool GetSetComPortBusyFlag(bool accessRequest = false)
        {
            lock (lockRoutine)
            {
                if (accessRequest)
                {
                    if (ComPortBusy)
                        return false;
                    else
                    {
                        ComPortBusy = true;
                        return ComPortBusy;
                    }
                }
                //clear the status flag
                else
                {
                    ComPortBusy = false;
                    return true;
                }

            }
        }
    }
}