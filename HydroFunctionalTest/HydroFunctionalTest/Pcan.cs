using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HydroFunctionalTest
{
    using TPCANHandle = System.UInt16;
    using TPCANBitrateFD = System.String;
    using TPCANTimestampFD = System.UInt64;
    using DeviceId = System.UInt16;

    #region Enumerations
    /// <summary>
    /// Represents a PCAN status/error code
    /// </summary>
    [Flags]
    public enum TPCANStatus : uint
    {
        /// <summary>
        /// No error
        /// </summary>
        PCAN_ERROR_OK = 0x00000,
        /// <summary>
        /// Transmit buffer in CAN controller is full
        /// </summary>
        PCAN_ERROR_XMTFULL = 0x00001,
        /// <summary>
        /// CAN controller was read too late        
        /// </summary>
        PCAN_ERROR_OVERRUN = 0x00002,
        /// <summary>
        /// Bus error: an error counter reached the 'light' limit
        /// </summary>
        PCAN_ERROR_BUSLIGHT = 0x00004,
        /// <summary>
        /// Bus error: an error counter reached the 'heavy' limit
        /// </summary>
        PCAN_ERROR_BUSHEAVY = 0x00008,
        /// <summary>
        /// Bus error: an error counter reached the 'warning' limit
        /// </summary>
        PCAN_ERROR_BUSWARNING = PCAN_ERROR_BUSHEAVY,
        /// <summary>
        /// Bus error: the CAN controller is error passive
        /// </summary>
        PCAN_ERROR_BUSPASSIVE = 0x40000,
        /// <summary>
        /// Bus error: the CAN controller is in bus-off state
        /// </summary>
        PCAN_ERROR_BUSOFF = 0x00010,
        /// <summary>
        /// Mask for all bus errors
        /// </summary>
        PCAN_ERROR_ANYBUSERR = (PCAN_ERROR_BUSWARNING | PCAN_ERROR_BUSLIGHT | PCAN_ERROR_BUSHEAVY | PCAN_ERROR_BUSOFF | PCAN_ERROR_BUSPASSIVE),
        /// <summary>
        /// Receive queue is empty
        /// </summary>
        PCAN_ERROR_QRCVEMPTY = 0x00020,
        /// <summary>
        /// Receive queue was read too late
        /// </summary>
        PCAN_ERROR_QOVERRUN = 0x00040,
        /// <summary>
        /// Transmit queue is full
        /// </summary>
        PCAN_ERROR_QXMTFULL = 0x00080,
        /// <summary>
        /// Test of the CAN controller hardware registers failed (no hardware found)
        /// </summary>
        PCAN_ERROR_REGTEST = 0x00100,
        /// <summary>
        /// Driver not loaded
        /// </summary>
        PCAN_ERROR_NODRIVER = 0x00200,
        /// <summary>
        /// Hardware already in use by a Net
        /// </summary>
        PCAN_ERROR_HWINUSE = 0x00400,
        /// <summary>
        /// A Client is already connected to the Net
        /// </summary>
        PCAN_ERROR_NETINUSE = 0x00800,
        /// <summary>
        /// Hardware handle is invalid
        /// </summary>
        PCAN_ERROR_ILLHW = 0x01400,
        /// <summary>
        /// Net handle is invalid
        /// </summary>
        PCAN_ERROR_ILLNET = 0x01800,
        /// <summary>
        /// Client handle is invalid
        /// </summary>
        PCAN_ERROR_ILLCLIENT = 0x01C00,
        /// <summary>
        /// Mask for all handle errors
        /// </summary>
        PCAN_ERROR_ILLHANDLE = (PCAN_ERROR_ILLHW | PCAN_ERROR_ILLNET | PCAN_ERROR_ILLCLIENT),
        /// <summary>
        /// Resource (FIFO, Client, timeout) cannot be created
        /// </summary>
        PCAN_ERROR_RESOURCE = 0x02000,
        /// <summary>
        /// Invalid parameter
        /// </summary>
        PCAN_ERROR_ILLPARAMTYPE = 0x04000,
        /// <summary>
        /// Invalid parameter value
        /// </summary>
        PCAN_ERROR_ILLPARAMVAL = 0x08000,
        /// <summary>
        /// Unknown error
        /// </summary>
        PCAN_ERROR_UNKNOWN = 0x10000,
        /// <summary>
        /// Invalid data, function, or action.
        /// </summary>
        PCAN_ERROR_ILLDATA = 0x20000,
        /// <summary>
        /// An operation was successfully carried out, however, irregularities were registered
        /// </summary>
        PCAN_ERROR_CAUTION = 0x2000000,
        /// <summary>
        /// Channel is not initialized
        /// <remarks>Value was changed from 0x40000 to 0x4000000</remarks>
        /// </summary>
        PCAN_ERROR_INITIALIZE = 0x4000000,
        /// <summary>
        /// Invalid operation
        /// <remarks>Value was changed from 0x80000 to 0x8000000</remarks>
        /// </summary>
        PCAN_ERROR_ILLOPERATION = 0x8000000,
    }

    /// <summary>
    /// Represents a PCAN device
    /// </summary>
    public enum TPCANDevice : byte
    {
        /// <summary>
        /// Undefined, unknown or not selected PCAN device value
        /// </summary>
        PCAN_NONE = 0,
        /// <summary>
        /// PCAN Non-Plug and Play devices. NOT USED WITHIN PCAN-Basic API
        /// </summary>
        PCAN_PEAKCAN = 1,
        /// <summary>
        /// PCAN-ISA, PCAN-PC/104, and PCAN-PC/104-Plus
        /// </summary>
        PCAN_ISA = 2,
        /// <summary>
        /// PCAN-Dongle
        /// </summary>
        PCAN_DNG = 3,
        /// <summary>
        /// PCAN-PCI, PCAN-cPCI, PCAN-miniPCI, and PCAN-PCI Express
        /// </summary>
        PCAN_PCI = 4,
        /// <summary>
        /// PCAN-USB and PCAN-USB Pro
        /// </summary>
        PCAN_USB = 5,
        /// <summary>
        /// PCAN-PC Card
        /// </summary>
        PCAN_PCC = 6,
        /// <summary>
        /// PCAN Virtual hardware. NOT USED WITHIN PCAN-Basic API
        /// </summary>
        PCAN_VIRTUAL = 7,
        /// <summary>
        /// PCAN Gateway devices
        /// </summary>
        PCAN_LAN = 8
    }

    /// <summary>
    /// Represents a PCAN parameter to be read or set
    /// </summary>
    public enum TPCANParameter : byte
    {
        /// <summary>
        /// PCAN-USB device number parameter
        /// </summary>
        PCAN_DEVICE_NUMBER = 1,
        /// <summary>
        /// PCAN-PC Card 5-Volt power parameter
        /// </summary>
        PCAN_5VOLTS_POWER = 2,
        /// <summary>
        /// PCAN receive event handler parameter
        /// </summary>
        PCAN_RECEIVE_EVENT = 3,
        /// <summary>
        /// PCAN message filter parameter
        /// </summary>
        PCAN_MESSAGE_FILTER = 4,
        /// <summary>
        /// PCAN-Basic API version parameter
        /// </summary>
        PCAN_API_VERSION = 5,
        /// <summary>
        /// PCAN device channel version parameter
        /// </summary>
        PCAN_CHANNEL_VERSION = 6,
        /// <summary>
        /// PCAN Reset-On-Busoff parameter
        /// </summary>
        PCAN_BUSOFF_AUTORESET = 7,
        /// <summary>
        /// PCAN Listen-Only parameter
        /// </summary>
        PCAN_LISTEN_ONLY = 8,
        /// <summary>
        /// Directory path for log files
        /// </summary>
        PCAN_LOG_LOCATION = 9,
        /// <summary>
        /// Debug-Log activation status
        /// </summary>
        PCAN_LOG_STATUS = 10,
        /// <summary>
        /// Configuration of the debugged information (LOG_FUNCTION_***)
        /// </summary>
        PCAN_LOG_CONFIGURE = 11,
        /// <summary>
        /// Custom insertion of text into the log file
        /// </summary>
        PCAN_LOG_TEXT = 12,
        /// <summary>
        /// Availability status of a PCAN-Channel
        /// </summary>
        PCAN_CHANNEL_CONDITION = 13,
        /// <summary>
        /// PCAN hardware name parameter
        /// </summary>
        PCAN_HARDWARE_NAME = 14,
        /// <summary>
        /// Message reception status of a PCAN-Channel
        /// </summary>
        PCAN_RECEIVE_STATUS = 15,
        /// <summary>
        /// CAN-Controller number of a PCAN-Channel
        /// </summary>
        PCAN_CONTROLLER_NUMBER = 16,
        /// <summary>
        /// Directory path for PCAN trace files
        /// </summary>
        PCAN_TRACE_LOCATION = 17,
        /// <summary>
        /// CAN tracing activation status
        /// </summary>
        PCAN_TRACE_STATUS = 18,
        /// <summary>
        /// Configuration of the maximum file size of a CAN trace
        /// </summary>
        PCAN_TRACE_SIZE = 19,
        /// <summary>
        /// Configuration of the trace file storing mode (TRACE_FILE_***)
        /// </summary>
        PCAN_TRACE_CONFIGURE = 20,
        /// <summary>
        /// Physical identification of a USB based PCAN-Channel by blinking its associated LED
        /// </summary>
        PCAN_CHANNEL_IDENTIFYING = 21,
        /// <summary>
        /// Capabilities of a PCAN device (FEATURE_***)
        /// </summary>
        PCAN_CHANNEL_FEATURES = 22,
        /// <summary>
        /// Using of an existing bit rate (PCAN-View connected to a channel)
        /// </summary>
        PCAN_BITRATE_ADAPTING = 23,
        /// <summary>
        /// Configured bit rate as Btr0Btr1 value
        /// </summary>
        PCAN_BITRATE_INFO = 24,
        /// <summary>
        /// Configured bit rate as TPCANBitrateFD string
        /// </summary>
        PCAN_BITRATE_INFO_FD = 25,
        /// <summary>
        /// Configured nominal CAN Bus speed as Bits per seconds
        /// </summary>
        PCAN_BUSSPEED_NOMINAL = 26,
        /// <summary>
        /// Configured CAN data speed as Bits per seconds
        /// </summary>
        PCAN_BUSSPEED_DATA = 27,
        /// <summary>
        /// Remote address of a LAN channel as string in IPv4 format
        /// </summary>
        PCAN_IP_ADDRESS = 28,
        /// <summary>
        /// Status of the Virtual PCAN-Gateway Service 
        /// </summary>
        PCAN_LAN_SERVICE_STATUS = 29,
    }

    /// <summary>
    /// Represents the type of a PCAN message
    /// </summary>
    [Flags]
    public enum TPCANMessageType : byte
    {
        /// <summary>
        /// The PCAN message is a CAN Standard Frame (11-bit identifier)
        /// </summary>
        PCAN_MESSAGE_STANDARD = 0x00,
        /// <summary>
        /// The PCAN message is a CAN Remote-Transfer-Request Frame
        /// </summary>
        PCAN_MESSAGE_RTR = 0x01,
        /// <summary>
        /// The PCAN message is a CAN Extended Frame (29-bit identifier)
        /// </summary>
        PCAN_MESSAGE_EXTENDED = 0x02,
        /// <summary>
        /// The PCAN message represents a FD frame in terms of CiA Specs
        /// </summary>
        PCAN_MESSAGE_FD = 0x04,
        /// <summary>
        /// The PCAN message represents a FD bit rate switch (CAN data at a higher bit rate)
        /// </summary>
        PCAN_MESSAGE_BRS = 0x08,
        /// <summary>
        /// The PCAN message represents a FD error state indicator(CAN FD transmitter was error active)
        /// </summary>
        PCAN_MESSAGE_ESI = 0x10,
        /// <summary>
        /// The PCAN message represents a PCAN status message
        /// </summary>
        PCAN_MESSAGE_STATUS = 0x80,
    }

    /// <summary>
    /// Represents a PCAN filter mode
    /// </summary>
    public enum TPCANMode : byte
    {
        /// <summary>
        /// Mode is Standard (11-bit identifier)
        /// </summary>
        PCAN_MODE_STANDARD = TPCANMessageType.PCAN_MESSAGE_STANDARD,
        /// <summary>
        /// Mode is Extended (29-bit identifier)
        /// </summary>
        PCAN_MODE_EXTENDED = TPCANMessageType.PCAN_MESSAGE_EXTENDED,
    }

    /// <summary>
    /// Represents a PCAN Baud rate register value
    /// </summary>
    public enum TPCANBaudrate : ushort
    {
        /// <summary>
        /// 1 MBit/s
        /// </summary>
        PCAN_BAUD_1M = 0x0014,
        /// <summary>
        /// 800 KBit/s
        /// </summary>
        PCAN_BAUD_800K = 0x0016,
        /// <summary>
        /// 500 kBit/s
        /// </summary>
        PCAN_BAUD_500K = 0x001C,
        /// <summary>
        /// 250 kBit/s
        /// </summary>
        PCAN_BAUD_250K = 0x011C,
        /// <summary>
        /// 125 kBit/s
        /// </summary>
        PCAN_BAUD_125K = 0x031C,
        /// <summary>
        /// 100 kBit/s
        /// </summary>
        PCAN_BAUD_100K = 0x432F,
        /// <summary>
        /// 95,238 KBit/s
        /// </summary>
        PCAN_BAUD_95K = 0xC34E,
        /// <summary>
        /// 83,333 KBit/s
        /// </summary>
        PCAN_BAUD_83K = 0x852B,
        /// <summary>
        /// 50 kBit/s
        /// </summary>
        PCAN_BAUD_50K = 0x472F,
        /// <summary>
        /// 47,619 KBit/s
        /// </summary>
        PCAN_BAUD_47K = 0x1414,
        /// <summary>
        /// 33,333 KBit/s
        /// </summary>
        PCAN_BAUD_33K = 0x8B2F,
        /// <summary>
        /// 20 kBit/s
        /// </summary>
        PCAN_BAUD_20K = 0x532F,
        /// <summary>
        /// 10 kBit/s
        /// </summary>
        PCAN_BAUD_10K = 0x672F,
        /// <summary>
        /// 5 kBit/s
        /// </summary>
        PCAN_BAUD_5K = 0x7F7F,
    }

    /// <summary>
    /// Represents the type of PCAN (non plug and play) hardware to be initialized
    /// </summary>
    public enum TPCANType : byte
    {
        /// <summary>
        /// PCAN-ISA 82C200
        /// </summary>
        PCAN_TYPE_ISA = 0x01,
        /// <summary>
        /// PCAN-ISA SJA1000
        /// </summary>
        PCAN_TYPE_ISA_SJA = 0x09,
        /// <summary>
        /// PHYTEC ISA 
        /// </summary>
        PCAN_TYPE_ISA_PHYTEC = 0x04,
        /// <summary>
        /// PCAN-Dongle 82C200
        /// </summary>
        PCAN_TYPE_DNG = 0x02,
        /// <summary>
        /// PCAN-Dongle EPP 82C200
        /// </summary>
        PCAN_TYPE_DNG_EPP = 0x03,
        /// <summary>
        /// PCAN-Dongle SJA1000
        /// </summary>
        PCAN_TYPE_DNG_SJA = 0x05,
        /// <summary>
        /// PCAN-Dongle EPP SJA1000
        /// </summary>
        PCAN_TYPE_DNG_SJA_EPP = 0x06,
    }
    #endregion

    #region Structures
    /// <summary>
    /// Represents a PCAN message
    /// </summary>
    public struct TPCANMsg
    {
        /// <summary>
        /// 11/29-bit message identifier
        /// </summary>
        public uint ID;
        /// <summary>
        /// Type of the message
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public TPCANMessageType MSGTYPE;
        /// <summary>
        /// Data Length Code of the message (0..8)
        /// </summary>
        public byte LEN;
        /// <summary>
        /// Data of the message (DATA[0]..DATA[7])
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] DATA;
    }

    /// <summary>
    /// Represents a timestamp of a received PCAN message.
    /// Total Microseconds = micros + 1000 * millis + 0x100000000 * 1000 * millis_overflow
    /// </summary>
    public struct TPCANTimestamp
    {
        /// <summary>
        /// Base-value: milliseconds: 0.. 2^32-1
        /// </summary>
        public uint millis;
        /// <summary>
        /// Roll-arounds of millis
        /// </summary>
        public ushort millis_overflow;
        /// <summary>
        /// Microseconds: 0..999
        /// </summary>
        public ushort micros;
    }

    /// <summary>
    /// Represents a PCAN message from a FD capable hardware
    /// </summary>
    public struct TPCANMsgFD
    {
        /// <summary>
        /// 11/29-bit message identifier
        /// </summary>
        public uint ID;
        /// <summary>
        /// Type of the message
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public TPCANMessageType MSGTYPE;
        /// <summary>
        /// Data Length Code of the message (0..15)
        /// </summary>
        public byte DLC;
        /// <summary>
        /// Data of the message (DATA[0]..DATA[63])
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] DATA;
    }
    #endregion

    #region PCANBasic class
    /// <summary>
    /// PCAN-Basic API class implementation
    /// </summary>
    class Pcan
    {
        #region PCAN-BUS Handles Definition
        /// <summary>
        /// Undefined/default value for a PCAN bus
        /// </summary>
        public const TPCANHandle PCAN_NONEBUS = 0x00;

        /// <summary>
        /// PCAN-ISA interface, channel 1
        /// </summary>
        public const TPCANHandle PCAN_ISABUS1 = 0x21;
        /// <summary>
        /// PCAN-ISA interface, channel 2
        /// </summary>
        public const TPCANHandle PCAN_ISABUS2 = 0x22;
        /// <summary>
        /// PCAN-ISA interface, channel 3
        /// </summary>
        public const TPCANHandle PCAN_ISABUS3 = 0x23;
        /// <summary>
        /// PCAN-ISA interface, channel 4
        /// </summary>
        public const TPCANHandle PCAN_ISABUS4 = 0x24;
        /// <summary>
        /// PCAN-ISA interface, channel 5
        /// </summary>
        public const TPCANHandle PCAN_ISABUS5 = 0x25;
        /// <summary>
        /// PCAN-ISA interface, channel 6
        /// </summary>
        public const TPCANHandle PCAN_ISABUS6 = 0x26;
        /// <summary>
        /// PCAN-ISA interface, channel 7
        /// </summary>
        public const TPCANHandle PCAN_ISABUS7 = 0x27;
        /// <summary>
        /// PCAN-ISA interface, channel 8
        /// </summary>
        public const TPCANHandle PCAN_ISABUS8 = 0x28;

        /// <summary>
        /// PPCAN-Dongle/LPT interface, channel 1 
        /// </summary>
        public const TPCANHandle PCAN_DNGBUS1 = 0x31;

        /// <summary>
        /// PCAN-PCI interface, channel 1
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS1 = 0x41;
        /// <summary>
        /// PCAN-PCI interface, channel 2
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS2 = 0x42;
        /// <summary>
        /// PCAN-PCI interface, channel 3
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS3 = 0x43;
        /// <summary>
        /// PCAN-PCI interface, channel 4
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS4 = 0x44;
        /// <summary>
        /// PCAN-PCI interface, channel 5
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS5 = 0x45;
        /// <summary>
        /// PCAN-PCI interface, channel 6
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS6 = 0x46;
        /// <summary>
        /// PCAN-PCI interface, channel 7
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS7 = 0x47;
        /// <summary>
        /// PCAN-PCI interface, channel 8
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS8 = 0x48;
        /// <summary>
        /// PCAN-PCI interface, channel 9
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS9 = 0x409;
        /// <summary>
        /// PCAN-PCI interface, channel 10
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS10 = 0x40A;
        /// <summary>
        /// PCAN-PCI interface, channel 11
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS11 = 0x40B;
        /// <summary>
        /// PCAN-PCI interface, channel 12
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS12 = 0x40C;
        /// <summary>
        /// PCAN-PCI interface, channel 13
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS13 = 0x40D;
        /// <summary>
        /// PCAN-PCI interface, channel 14
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS14 = 0x40E;
        /// <summary>
        /// PCAN-PCI interface, channel 15
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS15 = 0x40F;
        /// <summary>
        /// PCAN-PCI interface, channel 16
        /// </summary>
        public const TPCANHandle PCAN_PCIBUS16 = 0x410;

        /// <summary>
        /// PCAN-USB interface, channel 1
        /// </summary>
        public const TPCANHandle PCAN_USBBUS1 = 0x51;
        /// <summary>
        /// PCAN-USB interface, channel 2
        /// </summary>
        public const TPCANHandle PCAN_USBBUS2 = 0x52;
        /// <summary>
        /// PCAN-USB interface, channel 3
        /// </summary>
        public const TPCANHandle PCAN_USBBUS3 = 0x53;
        /// <summary>
        /// PCAN-USB interface, channel 4
        /// </summary>
        public const TPCANHandle PCAN_USBBUS4 = 0x54;
        /// <summary>
        /// PCAN-USB interface, channel 5
        /// </summary>
        public const TPCANHandle PCAN_USBBUS5 = 0x55;
        /// <summary>
        /// PCAN-USB interface, channel 6
        /// </summary>
        public const TPCANHandle PCAN_USBBUS6 = 0x56;
        /// <summary>
        /// PCAN-USB interface, channel 7
        /// </summary>
        public const TPCANHandle PCAN_USBBUS7 = 0x57;
        /// <summary>
        /// PCAN-USB interface, channel 8
        /// </summary>
        public const TPCANHandle PCAN_USBBUS8 = 0x58;
        /// <summary>
        /// PCAN-USB interface, channel 9
        /// </summary>
        public const TPCANHandle PCAN_USBBUS9 = 0x509;
        /// <summary>
        /// PCAN-USB interface, channel 10
        /// </summary>
        public const TPCANHandle PCAN_USBBUS10 = 0x50A;
        /// <summary>
        /// PCAN-USB interface, channel 11
        /// </summary>
        public const TPCANHandle PCAN_USBBUS11 = 0x50B;
        /// <summary>
        /// PCAN-USB interface, channel 12
        /// </summary>
        public const TPCANHandle PCAN_USBBUS12 = 0x50C;
        /// <summary>
        /// PCAN-USB interface, channel 13
        /// </summary>
        public const TPCANHandle PCAN_USBBUS13 = 0x50D;
        /// <summary>
        /// PCAN-USB interface, channel 14
        /// </summary>
        public const TPCANHandle PCAN_USBBUS14 = 0x50E;
        /// <summary>
        /// PCAN-USB interface, channel 15
        /// </summary>
        public const TPCANHandle PCAN_USBBUS15 = 0x50F;
        /// <summary>
        /// PCAN-USB interface, channel 16
        /// </summary>
        public const TPCANHandle PCAN_USBBUS16 = 0x510;

        /// <summary>
        /// PCAN-PC Card interface, channel 1
        /// </summary>
        public const TPCANHandle PCAN_PCCBUS1 = 0x61;
        /// <summary>
        /// PCAN-PC Card interface, channel 2
        /// </summary>
        public const TPCANHandle PCAN_PCCBUS2 = 0x62;

        /// <summary>
        /// PCAN-LAN interface, channel 1
        /// </summary>
        public const TPCANHandle PCAN_LANBUS1 = 0x801;
        /// <summary>
        /// PCAN-LAN interface, channel 2
        /// </summary>
        public const TPCANHandle PCAN_LANBUS2 = 0x802;
        /// <summary>
        /// PCAN-LAN interface, channel 3
        /// </summary>
        public const TPCANHandle PCAN_LANBUS3 = 0x803;
        /// <summary>
        /// PCAN-LAN interface, channel 4
        /// </summary>
        public const TPCANHandle PCAN_LANBUS4 = 0x804;
        /// <summary>
        /// PCAN-LAN interface, channel 5
        /// </summary>
        public const TPCANHandle PCAN_LANBUS5 = 0x805;
        /// <summary>
        /// PCAN-LAN interface, channel 6
        /// </summary>
        public const TPCANHandle PCAN_LANBUS6 = 0x806;
        /// <summary>
        /// PCAN-LAN interface, channel 7
        /// </summary>
        public const TPCANHandle PCAN_LANBUS7 = 0x807;
        /// <summary>
        /// PCAN-LAN interface, channel 8
        /// </summary>
        public const TPCANHandle PCAN_LANBUS8 = 0x808;
        /// <summary>
        /// PCAN-LAN interface, channel 9
        /// </summary>
        public const TPCANHandle PCAN_LANBUS9 = 0x809;
        /// <summary>
        /// PCAN-LAN interface, channel 10
        /// </summary>
        public const TPCANHandle PCAN_LANBUS10 = 0x80A;
        /// <summary>
        /// PCAN-LAN interface, channel 11
        /// </summary>
        public const TPCANHandle PCAN_LANBUS11 = 0x80B;
        /// <summary>
        /// PCAN-LAN interface, channel 12
        /// </summary>
        public const TPCANHandle PCAN_LANBUS12 = 0x80C;
        /// <summary>
        /// PCAN-LAN interface, channel 13
        /// </summary>
        public const TPCANHandle PCAN_LANBUS13 = 0x80D;
        /// <summary>
        /// PCAN-LAN interface, channel 14
        /// </summary>
        public const TPCANHandle PCAN_LANBUS14 = 0x80E;
        /// <summary>
        /// PCAN-LAN interface, channel 15
        /// </summary>
        public const TPCANHandle PCAN_LANBUS15 = 0x80F;
        /// <summary>
        /// PCAN-LAN interface, channel 16
        /// </summary>
        public const TPCANHandle PCAN_LANBUS16 = 0x810;
        #endregion

        #region FD Bit rate parameters
        /// <summary>
        /// Clock frequency in Herz (80000000, 60000000, 40000000, 30000000, 24000000, 20000000)
        /// </summary>
        public const string PCAN_BR_CLOCK = "f_clock";
        /// <summary>
        /// Clock frequency in Megaherz (80, 60, 40, 30, 24, 20)
        /// </summary>
        public const string PCAN_BR_CLOCK_MHZ = "f_clock_mhz";
        /// <summary>
        /// Clock prescaler for nominal time quantum
        /// </summary>
        public const string PCAN_BR_NOM_BRP = "nom_brp";
        /// <summary>
        /// TSEG1 segment for nominal bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_NOM_TSEG1 = "nom_tseg1";
        /// <summary>
        /// TSEG2 segment for nominal bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_NOM_TSEG2 = "nom_tseg2";
        /// <summary>
        /// Synchronization Jump Width for nominal bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_NOM_SJW = "nom_sjw";
        /// <summary>
        /// Sample point for nominal bit rate
        /// </summary>
        public const string PCAN_BR_NOM_SAMPLE = "nom_sam";
        /// <summary>
        /// Clock prescaler for highspeed data time quantum
        /// </summary>
        public const string PCAN_BR_DATA_BRP = "data_brp";
        /// <summary>
        /// TSEG1 segment for fast data bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_DATA_TSEG1 = "data_tseg1";
        /// <summary>
        /// TSEG2 segment for fast data bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_DATA_TSEG2 = "data_tseg2";
        /// <summary>
        /// Synchronization Jump Width for highspeed data bit rate in time quanta
        /// </summary>
        public const string PCAN_BR_DATA_SJW = "data_sjw";
        /// <summary>
        /// Secondary sample point delay for highspeed data bit rate in cyles
        /// </summary>
        public const string PCAN_BR_DATA_SAMPLE = "data_ssp_offset";
        #endregion

        #region Parameter values definition
        /// <summary>
        /// The PCAN parameter is not set (inactive)
        /// </summary>
        public const int PCAN_PARAMETER_OFF = 0;
        /// <summary>
        /// The PCAN parameter is set (active)
        /// </summary>
        public const int PCAN_PARAMETER_ON = 1;
        /// <summary>
        /// The PCAN filter is closed. No messages will be received
        /// </summary>
        public const int PCAN_FILTER_CLOSE = 0;
        /// <summary>
        /// The PCAN filter is fully opened. All messages will be received
        /// </summary>
        public const int PCAN_FILTER_OPEN = 1;
        /// <summary>
        /// The PCAN filter is custom configured. Only registered 
        /// messages will be received
        /// </summary>
        public const int PCAN_FILTER_CUSTOM = 2;
        /// <summary>
        /// The PCAN-Channel handle is illegal, or its associated hardware is not available
        /// </summary>
        public const int PCAN_CHANNEL_UNAVAILABLE = 0;
        /// <summary>
        /// The PCAN-Channel handle is available to be connected (Plug and Play Hardware: it means furthermore that the hardware is plugged-in)
        /// </summary>
        public const int PCAN_CHANNEL_AVAILABLE = 1;
        /// <summary>
        /// The PCAN-Channel handle is valid, and is already being used
        /// </summary>
        public const int PCAN_CHANNEL_OCCUPIED = 2;
        /// <summary>
        /// The PCAN-Channel handle is already being used by a PCAN-View application, but is available to connect
        /// </summary>
        public const int PCAN_CHANNEL_PCANVIEW = (PCAN_CHANNEL_AVAILABLE | PCAN_CHANNEL_OCCUPIED);

        /// <summary>
        /// Logs system exceptions / errors
        /// </summary>
        public const int LOG_FUNCTION_DEFAULT = 0x00;
        /// <summary>
        /// Logs the entries to the PCAN-Basic API functions 
        /// </summary>
        public const int LOG_FUNCTION_ENTRY = 0x01;
        /// <summary>
        /// Logs the parameters passed to the PCAN-Basic API functions 
        /// </summary>
        public const int LOG_FUNCTION_PARAMETERS = 0x02;
        /// <summary>
        /// Logs the exits from the PCAN-Basic API functions 
        /// </summary>
        public const int LOG_FUNCTION_LEAVE = 0x04;
        /// <summary>
        /// Logs the CAN messages passed to the CAN_Write function
        /// </summary>
        public const int LOG_FUNCTION_WRITE = 0x08;
        /// <summary>
        /// Logs the CAN messages received within the CAN_Read function
        /// </summary>
        public const int LOG_FUNCTION_READ = 0x10;
        /// <summary>
        /// Logs all possible information within the PCAN-Basic API functions
        /// </summary>
        public const int LOG_FUNCTION_ALL = 0xFFFF;

        /// <summary>
        /// A single file is written until it size reaches PAN_TRACE_SIZE
        /// </summary>
        public const int TRACE_FILE_SINGLE = 0x00;
        /// <summary>
        /// Traced data is distributed in several files with size PAN_TRACE_SIZE
        /// </summary>
        public const int TRACE_FILE_SEGMENTED = 0x01;
        /// <summary>
        /// Includes the date into the name of the trace file
        /// </summary>
        public const int TRACE_FILE_DATE = 0x02;
        /// <summary>
        /// Includes the start time into the name of the trace file
        /// </summary>
        public const int TRACE_FILE_TIME = 0x04;
        /// <summary>
        /// Causes the overwriting of available traces (same name)
        /// </summary>
        public const int TRACE_FILE_OVERWRITE = 0x80;

        /// <summary>
        /// Device supports flexible data-rate (CAN-FD)
        /// </summary>
        public const int FEATURE_FD_CAPABLE = 0x01;

        /// <summary>
        /// The service is not running
        /// </summary>
        public const int SERVICE_STATUS_STOPPED = 0x01;
        /// <summary>
        /// The service is running
        /// </summary>
        public const int SERVICE_STATUS_RUNNING = 0x04;
        #endregion

        #region PCANBasic API Implementation
        /// <summary>
        /// Initializes a PCAN Channel 
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Btr0Btr1">The speed for the communication (BTR0BTR1 code)</param>
        /// <param name="HwType">NON PLUG and PLAY: The type of hardware and operation mode</param>
        /// <param name="IOPort">NON PLUG and PLAY: The I/O address for the parallel port</param>
        /// <param name="Interrupt">NON PLUG and PLAY: Interrupt number of the parallel por</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Initialize")]
        public static extern TPCANStatus Initialize(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            [MarshalAs(UnmanagedType.U2)]
            TPCANBaudrate Btr0Btr1,
            [MarshalAs(UnmanagedType.U1)]
            TPCANType HwType,
            UInt32 IOPort,
            UInt16 Interrupt);

        /// <summary>
        /// Initializes a PCAN Channel
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Btr0Btr1">The speed for the communication (BTR0BTR1 code)</param>
        /// <returns>A TPCANStatus error code</returns>        
        public static TPCANStatus Initialize(
            TPCANHandle Channel,
            TPCANBaudrate Btr0Btr1)
        {
            return Initialize(Channel, Btr0Btr1, (TPCANType)0, 0, 0);
        }

        /// <summary>
        /// Initializes a FD capable PCAN Channel 
        /// </summary>
        /// <param name="Channel">The handle of a FD capable PCAN Channel</param>
        /// <param name="BitrateFD">The speed for the communication (FD bit rate string)</param>
        /// <remarks> See PCAN_BR_* values
        /// Bit rate string must follow the following construction rules:
        /// * parameters and values must be separated by '='
        /// * Couples of Parameter/value must be separated by ','
        /// * Following Parameter must be filled out: f_clock, data_brp, data_sjw, data_tseg1, data_tseg2,
        ///   nom_brp, nom_sjw, nom_tseg1, nom_tseg2.
        /// * Following Parameters are optional (not used yet): data_ssp_offset, nom_samp</remarks>
        /// <example>f_clock_mhz=80, nom_brp=1, nom_tset1=63, nom_tseg2=16, nom_sjw=7, data_brp=4, data_tset1=12, data_tseg2=7, data_sjw=1</example>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_InitializeFD")]
        public static extern TPCANStatus InitializeFD(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            TPCANBitrateFD BitrateFD);

        /// <summary>
        /// Uninitializes one or all PCAN Channels initialized by CAN_Initialize
        /// </summary>
        /// <remarks>Giving the TPCANHandle value "PCAN_NONEBUS", 
        /// uninitialize all initialized channels</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Uninitialize")]
        public static extern TPCANStatus Uninitialize(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel);

        /// <summary>
        /// Resets the receive and transmit queues of the PCAN Channel
        /// </summary>
        /// <remarks>A reset of the CAN controller is not performed</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Reset")]
        public static extern TPCANStatus Reset(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel);

        /// <summary>
        /// Gets the current status of a PCAN Channel
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_GetStatus")]
        public static extern TPCANStatus GetStatus(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel);

        /// <summary>
        /// Reads a CAN message from the receive queue of a PCAN Channel
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsg structure buffer to store the CAN message</param>
        /// <param name="TimestampBuffer">A TPCANTimestamp structure buffer to get
        /// the reception time of the message</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Read")]
        public static extern TPCANStatus Read(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            out TPCANMsg MessageBuffer,
            out TPCANTimestamp TimestampBuffer);

        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Read")]
        private static extern TPCANStatus Read(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            out TPCANMsg MessageBuffer,
            IntPtr bufferPointer);

        /// <summary>
        /// Reads a CAN message from the receive queue of a PCAN Channel
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsg structure buffer to store the CAN message</param>        
        /// <returns>A TPCANStatus error code</returns>
        public static TPCANStatus Read(
            TPCANHandle Channel,
            out TPCANMsg MessageBuffer)
        {
            return Read(Channel, out MessageBuffer, IntPtr.Zero);
        }

        /// <summary>
        /// Reads a CAN message from the receive queue of a FD capable PCAN Channel 
        /// </summary>
        /// <param name="Channel">The handle of a FD capable PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsgFD structure buffer to store the CAN message</param>
        /// <param name="TimestampBuffer">A TPCANTimestampFD buffer to get the
        /// reception time of the message</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_ReadFD")]
        public static extern TPCANStatus ReadFD(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            out TPCANMsgFD MessageBuffer,
            out TPCANTimestampFD TimestampBuffer);

        [DllImport("PCANBasic.dll", EntryPoint = "CAN_ReadFD")]
        private static extern TPCANStatus ReadFD(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            out TPCANMsgFD MessageBuffer,
            IntPtr TimestampBuffer);

        /// <summary>
        /// Reads a CAN message from the receive queue of a FD capable PCAN Channel 
        /// </summary>
        /// <param name="Channel">The handle of a FD capable PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsgFD structure buffer to store the CAN message</param>
        /// <returns>A TPCANStatus error code</returns>
        public static TPCANStatus ReadFD(
            TPCANHandle Channel,
            out TPCANMsgFD MessageBuffer)
        {
            return ReadFD(Channel, out MessageBuffer, IntPtr.Zero);
        }

        /// <summary>
        ///  Transmits a CAN message 
        /// </summary>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsg buffer with the message to be sent</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_Write")]
        public static extern TPCANStatus Write(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            ref TPCANMsg MessageBuffer);

        /// <summary>
        /// Transmits a CAN message over a FD capable PCAN Channel
        /// </summary>
        /// <param name="Channel">The handle of a FD capable PCAN Channel</param>
        /// <param name="MessageBuffer">A TPCANMsgFD buffer with the message to be sent</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_WriteFD")]
        public static extern TPCANStatus WriteFD(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            ref TPCANMsgFD MessageBuffer);

        /// <summary>
        /// Configures the reception filter
        /// </summary>
        /// <remarks>The message filter will be expanded with every call to 
        /// this function. If it is desired to reset the filter, please use
        /// the 'SetValue' function</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="FromID">The lowest CAN ID to be received</param>
        /// <param name="ToID">The highest CAN ID to be received</param>
        /// <param name="Mode">Message type, Standard (11-bit identifier) or
        /// Extended (29-bit identifier)</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_FilterMessages")]
        public static extern TPCANStatus FilterMessages(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            UInt32 FromID,
            UInt32 ToID,
            [MarshalAs(UnmanagedType.U1)]
            TPCANMode Mode);

        /// <summary>
        /// Retrieves a PCAN Channel value
        /// </summary>
        /// <remarks>Parameters can be present or not according with the kind 
        /// of Hardware (PCAN Channel) being used. If a parameter is not available,
        /// a PCAN_ERROR_ILLPARAMTYPE error will be returned</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Parameter">The TPCANParameter parameter to get</param>
        /// <param name="StringBuffer">Buffer for the parameter value</param>
        /// <param name="BufferLength">Size in bytes of the buffer</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_GetValue")]
        public static extern TPCANStatus GetValue(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            [MarshalAs(UnmanagedType.U1)]
            TPCANParameter Parameter,
            StringBuilder StringBuffer,
            UInt32 BufferLength);

        /// <summary>
        /// Retrieves a PCAN Channel value
        /// </summary>
        /// <remarks>Parameters can be present or not according with the kind 
        /// of Hardware (PCAN Channel) being used. If a parameter is not available,
        /// a PCAN_ERROR_ILLPARAMTYPE error will be returned</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Parameter">The TPCANParameter parameter to get</param>
        /// <param name="NumericBuffer">Buffer for the parameter value</param>
        /// <param name="BufferLength">Size in bytes of the buffer</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_GetValue")]
        public static extern TPCANStatus GetValue(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            [MarshalAs(UnmanagedType.U1)]
            TPCANParameter Parameter,
            out UInt32 NumericBuffer,
            UInt32 BufferLength);

        /// <summary>
        /// Configures or sets a PCAN Channel value 
        /// </summary>
        /// <remarks>Parameters can be present or not according with the kind 
        /// of Hardware (PCAN Channel) being used. If a parameter is not available,
        /// a PCAN_ERROR_ILLPARAMTYPE error will be returned</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Parameter">The TPCANParameter parameter to set</param>
        /// <param name="NumericBuffer">Buffer with the value to be set</param>
        /// <param name="BufferLength">Size in bytes of the buffer</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_SetValue")]
        public static extern TPCANStatus SetValue(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            [MarshalAs(UnmanagedType.U1)]
            TPCANParameter Parameter,
            ref UInt32 NumericBuffer,
            UInt32 BufferLength);

        /// <summary>
        /// Configures or sets a PCAN Channel value
        /// </summary>
        /// <remarks>Parameters can be present or not according with the kind 
        /// of Hardware (PCAN Channel) being used. If a parameter is not available,
        /// a PCAN_ERROR_ILLPARAMTYPE error will be returned</remarks>
        /// <param name="Channel">The handle of a PCAN Channel</param>
        /// <param name="Parameter"></param>
        /// <param name="StringBuffer">Buffer with the value to be set</param>
        /// <param name="BufferLength">Size in bytes of the buffer</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_SetValue")]
        public static extern TPCANStatus SetValue(
            [MarshalAs(UnmanagedType.U2)]
            TPCANHandle Channel,
            [MarshalAs(UnmanagedType.U1)]
            TPCANParameter Parameter,
            [MarshalAs(UnmanagedType.LPStr,SizeParamIndex=3)]
            string StringBuffer,
            UInt32 BufferLength);

        /// <summary>
        /// Returns a descriptive text of a given TPCANStatus error 
        /// code, in any desired language
        /// </summary>
        /// <remarks>The current languages available for translation are: 
        /// Neutral (0x00), German (0x07), English (0x09), Spanish (0x0A),
        /// Italian (0x10) and French (0x0C)</remarks>
        /// <param name="Error">A TPCANStatus error code</param>
        /// <param name="Language">Indicates a 'Primary language ID'</param>
        /// <param name="StringBuffer">Buffer for the text (must be at least 256 in length)</param>
        /// <returns>A TPCANStatus error code</returns>
        [DllImport("PCANBasic.dll", EntryPoint = "CAN_GetErrorText")]
        public static extern TPCANStatus GetErrorText(
            [MarshalAs(UnmanagedType.U4)]
            TPCANStatus Error,
            UInt16 Language,
            StringBuilder StringBuffer);
        #endregion

        #region Crane Test Code

        #region Crane Test Members

        public UInt16 DevHandle { get; set; }
        public UInt16 DevID { get; set; }
        /// <summary>
        /// store method operation results for the Main UI to view
        /// </summary>
        public List<string> pcanReturnData = new List<string>();
        /// <summary>
        /// global variable set using predefined values from the enumerations section(region)
        /// </summary>
        TPCANBaudrate canUSBBaudRate = TPCANBaudrate.PCAN_BAUD_1M;
        /// <summary>
        /// nested dictionary will contain the CAN ID as the key and a dictionary as 
        /// the value( key= data frame, value=timestamp) or the main UI to sort through CAN data if desired.
        /// </summary>
        public Dictionary<UInt32, Dictionary<List<UInt32>, UInt32>> canReadRtnData = new Dictionary<UInt32, 
            Dictionary<List<UInt32>, UInt32>>();
        /// <summary>
        /// List will contain the most recent CAN data if the desired CAN ID is found
        /// </summary>
        public List<UInt32> tempCanDataFrame = new List<UInt32>();
        #endregion Crane Test Members

        #region Crane Test Methods

        #region ScanForDev()
        /// <summary>
        /// Will scan specifically for the PCAN-USB type devices.
        /// When found it will query the device for its ID and compare it to the device ID in the method parameter.
        /// Devices that don't match the parameter device ID will need to call the SetDevId() method 
        /// </summary>
        /// <returns></returns>
        //scan for devices and save the device ID into the 
        public bool ScanForDev(UInt16 devId)
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method

            TPCANStatus stsResult;  //holds return info from returning methods - applies to native PCAN code not Crane test code methods
            UInt32 iBuffer;           //holds data that is worked on after calling method returns -  applies to native PCAN code not Crane test code methods

            try
            {
                // Checks for a Plug&Play Handle and, according with the return value, adds it
                // to the pcanDeviceIds List.
                //
                //search for maximum of 8 devices.  the loop count is arbitrary, just need to find one that matches the device ID passed to the method
                for (System.UInt16 i = 0x51; i < 0x58; i++) //0x51 represents the first value given to the PCAN-USB type adapters in the handles definition section(region)
                {
                    stsResult = Pcan.GetValue(i, TPCANParameter.PCAN_CHANNEL_CONDITION, out iBuffer, sizeof(UInt32));                    
                    if ((stsResult == TPCANStatus.PCAN_ERROR_OK) && ((iBuffer & Pcan.PCAN_CHANNEL_AVAILABLE) == Pcan.PCAN_CHANNEL_AVAILABLE))
                    {
                        DevHandle = i;
                        // carried over from PCAN example code, may not be needed... --> stsResult = Pcan.GetValue(m_HandlesArray[i], TPCANParameter.PCAN_CHANNEL_FEATURES, out iBuffer, sizeof(UInt32));
                        if (ActivateDevice())
                        {                            
                            stsResult = GetValue(i, TPCANParameter.PCAN_DEVICE_NUMBER, out iBuffer, sizeof(UInt32)); //get the device ID and compare to method parameter
                            //if no devices match then the device needs to be configured to have its device ID equal to the method parameter
                            UInt32 tempDevID = (UInt16)iBuffer;
                            if (tempDevID == devId)
                            {
                                pcanReturnData.Add("Found PCAN-USB device with the device ID: " + devId.ToString());
                                return true;
                            }
                            DeactivateDevice();
                        }
                        else
                            pcanReturnData.Add("Failed to Initialize device.  Handle ID: " + DevHandle.ToString());
                    }
                }
                if (!returnValue)
                    pcanReturnData.Add("No PCAN-USB device found with the device ID: " + devId.ToString());
                    
            }
            catch (Exception ex)
            {
                pcanReturnData.Add("Error in the ScanForDev() function" + Environment.NewLine + ex.Message);
            }
                      
            return returnValue;
        }
        #endregion ScanForDev()

        #region SetDevId()
        /// <summary>
        /// For setting the device ID if the device memory is corrupted/erased.
        /// Only a value of 1 or 2 are accepted as device IDs
        /// device ID #1 is for fixture #1, ID #2 for fixture #2
        /// </summary>
        /// <returns></returns>
        public bool SetDevId(UInt16 devNum)
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods
            UInt32 tempDevNum = (UInt32)devNum;

            try
            {
                DeactivateDevice();
                ScanForDev(devNum);  //the return value and parameter doesn't matter here since we are manually setting the device ID.  Only one device should be connected or the ID could be assigned to another device.
                if (ActivateDevice())
                {
                    if ((devNum == 1) || (devNum == 2))
                    {
                        stsResult = SetValue(DevHandle, TPCANParameter.PCAN_DEVICE_NUMBER, ref tempDevNum, sizeof(UInt32));
                        if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                            returnValue = true;
                        else
                            MessageBox.Show("PCAN Error returned while attempting to assign ID to PCAN-USB adapter:" + Environment.NewLine + stsResult.ToString());
                    }
                    else
                    {
                        MessageBox.Show("Invalid parameter sent to SetDevId() function.  Parameter must be an integer value of 1 or 2" +
                            Environment.NewLine + "The test program only expects a total of two devices will need device IDs.");
                    }
                    DeactivateDevice();
                }
                else
                    MessageBox.Show("Failed to Initialize device.  Handle ID: " + DevHandle.ToString());
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error in SetDevId() function" + Environment.NewLine + ex.Message);
            }            
                        
            return returnValue;
        }
        #endregion SetDevId()

        #region CanWrite()
        /// <summary>
        /// Prepares the CAN message using the method's parameters and loads it into the TPCANMsg struct
        /// Writes the message and checks for any errors
        /// </summary>
        /// <param name="canData"></param>
        /// <param name="canId"></param>
        /// <param name="msgLen"></param>
        /// <param name="remoteFrame"></param>
        /// <returns></returns>
        public bool CanWrite(byte[] canData, UInt32 canId, byte msgLen, bool remoteFrame = false)
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods

            try
            {

                TPCANMsg CanMsg = new TPCANMsg();
                CanMsg.DATA = canData;
                CanMsg.ID = canId;
                CanMsg.LEN = msgLen;
                if (!remoteFrame)
                    CanMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;              
                else
                    CanMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_RTR;
                            
                //Write command
                stsResult = Write(DevHandle, ref CanMsg);
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                    returnValue = true;
                else
                    pcanReturnData.Add("PCAN Error returned while attempting to write to the PCAN-USB adapter:" + Environment.NewLine + stsResult.ToString());
            }
            catch(Exception ex)
            {
                pcanReturnData.Add("Error in CanWrite() function" + Environment.NewLine + ex.Message);
            }

            return returnValue;
        }
        #endregion CanWrite()

        #region CanRead()
        /// <summary>
        /// Calls PCAN 'Read' method and stores CAN msg values in nested dictionary 'canReadRtnData'
        /// most recent data byte stored in "canDataFrame" list
        /// </summary>
        /// <param name="canIdToMatch"></param>
        /// <returns></returns>
        public bool CanRead(UInt32 canIdToMatch)
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            tempCanDataFrame.Clear();           //List will contain the most recent CAN data if the desired CAN ID is found
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods            

            try
            {
                TPCANMsg CanMsg = new TPCANMsg();
                TPCANTimestamp timeStamp;

                //change the following line to a read command
                stsResult = Read(DevHandle, out CanMsg, out timeStamp);
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                {
                    List<UInt32> tempList = new List<UInt32>();
                    //sort through the data byte and load into a List<UInt32>
                    foreach (byte b in CanMsg.DATA)
                    {
                        tempList.Add((UInt32)b);
                    }
                    //load the can message data into the nested dictionary for the main UI to sort through if desired
                    Dictionary<List<UInt32>, UInt32> tempDic = new Dictionary<List<UInt32>, UInt32> { { tempList, timeStamp.millis } };
                    canReadRtnData.Add(CanMsg.ID, tempDic);

                    if (canIdToMatch == CanMsg.ID)
                    {
                        returnValue = true;
                        tempCanDataFrame = tempList;
                    }                    
                }                    
                else
                    pcanReturnData.Add("PCAN Error returned while attempting to read from the PCAN-USB adapter:" + Environment.NewLine + stsResult.ToString());
            }
            catch(Exception ex)
            {
                pcanReturnData.Add("Error in CanRead() function" + Environment.NewLine + ex.Message);
            }

            return returnValue;
        }
        #endregion CanRead()

        #region ActivateDevice()
        /// <summary>
        /// Calls the PCAN "Initialize" method and checks for errors
        /// </summary>
        /// <returns></returns>
        public bool ActivateDevice()
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods

            try
            {
                //Initialize the attached device and check for errors
                stsResult = Initialize(DevHandle, canUSBBaudRate);
                if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                {
                    if (stsResult != TPCANStatus.PCAN_ERROR_CAUTION)
                        pcanReturnData.Add("PCAN-USB 'Error' while initializing:" + Environment.NewLine + stsResult.ToString());
                    else
                        pcanReturnData.Add("PCAN-USB 'Caution' while initializing:" + Environment.NewLine + stsResult.ToString());
                }
                else
                    returnValue = true; //device intialization successful
            }
            catch (Exception ex)
            {
                pcanReturnData.Add("Error in ActivateDevice() function" + Environment.NewLine + ex.Message);
            }

            return returnValue;
        }
        #endregion ActivateDevice()

        #region DeactivateDevice()        
        /// <summary>
        /// Calls the PCAN "Uninitialize" method and checks for errors
        /// </summary>
        /// <returns></returns>
        public bool DeactivateDevice()
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods

            try
            {
                //Initialize the attached device and check for errors
                stsResult = Uninitialize(DevHandle);
                if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                {
                    if (stsResult != TPCANStatus.PCAN_ERROR_CAUTION)
                        pcanReturnData.Add("PCAN-USB 'Error' while uninitializing:" + Environment.NewLine + stsResult.ToString());
                    else
                        pcanReturnData.Add("PCAN-USB 'Caution' while uninitializing:" + Environment.NewLine + stsResult.ToString());
                }
                else
                    returnValue = true; //device unintialization successful
            }
            catch (Exception ex)
            {
                pcanReturnData.Add("Error in DeactivateDevice() function" + Environment.NewLine + ex.Message);
            }

            return returnValue;
        }
        #endregion DeactivateDevice()

        #region ClearDataBuffers()        
        /// <summary>
        /// Resets the device receive and transmit queues and all Crane test code member data buffers
        /// </summary>
        /// <returns></returns>
        public bool ClearDataBuffers()
        {
            bool returnValue = false;       //the method's return variable.
            pcanReturnData.Clear();         //List must be cleared upon entry into every Crane Test code method
            TPCANStatus stsResult;          //holds return info from returning methods - applies to native PCAN code not Crane test code methods
            canReadRtnData.Clear();         //for the main UI to sort through CAN data if desired.  Nested dictionary will contain the CAN ID as the key and a dictionary as the value( key= data frame, value=timestamp)
            tempCanDataFrame.Clear();       //List will contain the most recent CAN data if the desired CAN ID is found

            try
            {
                //Reset the attached device and check for errors
                stsResult =Reset(DevHandle);
                if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                {
                    if (stsResult != TPCANStatus.PCAN_ERROR_CAUTION)
                        pcanReturnData.Add("PCAN-USB 'Error' while resetting device:" + Environment.NewLine + stsResult.ToString());
                    else
                        pcanReturnData.Add("PCAN-USB 'Caution' while resetting device:" + Environment.NewLine + stsResult.ToString());
                }
                else
                    returnValue = true; //device reset successful
            }
            catch (Exception ex)
            {
                pcanReturnData.Add("Error in ResetDevice() function" + Environment.NewLine + ex.Message);
            }

            return returnValue;
        }
        #endregion ClearDataBuffers()

        #endregion Crane Test Methods

        #endregion Crane Test Code

    }
}
#endregion
