// Cube gadget interface
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace Cube8x8x8.PIC32
    {

    // Many operating systems cache files, flushing to disk when convenient. 
    // This class must flush files immediately to the board to get an immediate response. 
    // Unfortunately there seems no portable way to do this in .NET, so a fallback through 
    // the Win32 API is needed. Unfortunately again, this means this code does not run as
    // is under Linux or MacOS. Port the GetUnbufferedFile call to make this work.


    /// <summary>
    /// Represent an instance of the PIC32 gadget board
    /// for controlling the 8x8x8 HypnoCube.
    /// </summary>
    public class PIC32 : IDisposable
        {

        #region Message Event
        /// <summary>
        /// Represent a message from the gadget.
        /// </summary>
        public class MessageArgs : EventArgs
            {
            public string Message;
            }

        /// <summary>
        /// Subscribe to this to get process messages
        /// </summary>
        public event EventHandler<MessageArgs> RaiseMessageEvent;

        /// <summary>
        /// Raise the event internally
        /// </summary>
        /// <param name="message"></param>
        protected void OnMessageEvent(string message)
            {
            var localEvent = RaiseMessageEvent;
            if (localEvent != null)
                localEvent(this, new MessageArgs() { Message = "PIC32: " + DateTime.Now + ": " + message });
            }
        #endregion

        /// <summary>
        /// Create a PIC32 object
        /// </summary>
        public PIC32()
            {
            CreateGPIO();
            GadgetDrive = "";
            }

        /// <summary>
        /// The drive connected to, blank if none
        /// </summary>
        public string GadgetDrive { get; private set; }

        /// <summary>
        /// Return true if the gadget is connected, 
        /// else return false.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Open the device, return true on success. Optional drive name tells 
        /// where the gadget is located. Otherwise all drives are scanned.
        /// </summary>
        /// <param name="driveName">Optional drive name.</param>
        /// <returns>true on success, else false.</returns>
        public bool Open(string driveName = null)
            {
            try
                {
                if (IsConnected)
                    Close();
                string drive = FindDisk(driveName);
                if (drive == null)
                    {
                    OnMessageEvent(String.Format("Cannot find disk drive with {0} in root", fileNames[0]));
                    return false;
                    }

                OpenFiles(drive);

                return true;
                }
            catch (Exception ex)
                {
                OnMessageEvent("EXCEPTION: " + ex.Message);
                return false;
                }
            }

        /// <summary>
        /// Closes the device and releases all resources.
        /// Cannot reopen the device without creating a new one.
        /// </summary>
        public void Close()
            {
            if (IsConnected)
                OnMessageEvent("Disconnecting from drive " + GadgetDrive);
            Dispose(true);
            GC.SuppressFinalize(this);
            }

        /// <summary>
        /// Write an image to the device. 
        /// </summary>
        /// <param name="image"></param>
        public void WriteImage(byte[] image)
            {
            WriteFile(image, ImageFileSize, imageFile);
            }

        /// <summary>
        /// Read an image from the device. 
        /// </summary>
        /// <param name="image"></param>
        public byte[] ReadImage()
            {
            return ReadFile(ImageFileSize, imageFile);
            }

        /// <summary>
        /// Image size in bytes
        /// </summary>
        public int ImageSize { get { return ImageFileSize; } }

        /// <summary>
        /// Read the info file, returning the text
        /// </summary>
        /// <returns></returns>
        public string ReadInfo()
            {
            byte [] array = ReadFile(InfoFileSize, infoFile);
            if (array == null)
                return null;
            return Encoding.ASCII.GetString(array); // convert ASCII to .NET string
            }

        /// <summary>
        /// Read the debug file, returning the text
        /// </summary>
        /// <returns></returns>
        public byte[] ReadDebug()
            {
            byte [] array = ReadFile(DebugFileSize, debugFile);
            if (array == null)
                return null;
            return array;
            }

        /// <summary>
        /// Set the LED counter to show (true) or not (false)
        /// </summary>
        public bool ShowActivityCounterLED
            {
            get
                {
                return ReadControl(0, 0, 1) == 1;
                }
            set
                {
                WriteControl(0, 0, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Show errors on LEDs : show (true) or not (false)
        /// </summary>
        public bool ShowErrorLED
            {
            get
                {
                return ReadControl(0, 1, 1) == 1;
                }
            set
                {
                WriteControl(0, 1, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Show disk read LEDs : show (true) or not (false)
        /// </summary>
        public bool ShowReadLED
            {
            get
                {
                return ReadControl(0, 2, 1) == 1;
                }
            set
                {
                WriteControl(0, 2, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Show disk write LEDs : show (true) or not (false)
        /// </summary>
        public bool ShowWriteLED
            {
            get
                {
                return ReadControl(0, 3, 1) == 1;
                }
            set
                {
                WriteControl(0, 3, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Allow COMMAND to set LEDs states (true) or not (false)
        /// </summary>
        public bool AllowCommandLEDs
            {
            get
                {
                return ReadControl(0, 4, 1) == 1;
                }
            set
                {
                WriteControl(0, 4, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Set or clear LED 0 if the CONTROL settings allows it.
        /// </summary>
        public bool LED0
            {
            get
                {
                return ReadCommand(1, 0, 1) == 1;
                }
            set
                {
                WriteCommand(1, 0, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Set or clear LED 1 if the CONTROL settings allows it.
        /// </summary>
        public bool LED1
            {
            get
                {
                return ReadCommand(1, 1, 1) == 1;
                }
            set
                {
                WriteCommand(1, 1, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Set or clear LED 2 if the CONTROL settings allows it.
        /// </summary>
        public bool LED2
            {
            get
                {
                return ReadCommand(1, 2, 1) == 1;
                }
            set
                {
                WriteCommand(1, 2, 1, value ? 1U : 0U);
                }
            }
        /// <summary>
        /// Set or clear LED 3 if the CONTROL settings allows it.
        /// </summary>
        public bool LED3
            {
            get
                {
                return ReadCommand(1, 3, 1) == 1;
                }
            set
                {
                WriteCommand(1, 3, 1, value ? 1U : 0U);
                }
            }

        /// <summary>
        /// Plasma power down time in 100ths of a second
        /// </summary>
        public ushort PlasmaDelay
            {
            get
                {
                return (ushort)ReadControl(13, 0, 16);
                }
            set
                {
                WriteControl(13, 0, 16, value);
                }
            }

        /// <summary>
        /// Interrupt delay timer. 
        /// Lower values reduce flicker, but cause more 
        /// time to be used for updates.
        /// Values lower than 2048 might crash the system.
        /// </summary>
        public ushort InterruptDelay
            {
            get
                {
                return (ushort)ReadControl(11, 0, 16);
                }
            set
                {
                WriteControl(11, 0, 16, value);
                }
            }

        /// <summary>
        /// Represent a single GPIO
        /// </summary>
        public class GPIOHolder
            {
            /// <summary>
            /// Get/Set the value of the GPIO line
            /// </summary>
            public bool Value
                {
                get
                    {
                    return getBit();
                    }
                set
                    {
                    setBit(value);
                    }
                }
            /// <summary>
            /// The direction of a line: input or output
            /// </summary>
            public enum DirectionType
                {
                Input,Output
                }
            /// <summary>
            /// The direction of this line
            /// </summary>
            public DirectionType Direction
                {
                get
                    {
                    return getDir();
                    }
                set
                    {
                    setDir(value);
                    }
                }

            /// <summary>
            /// Create a GPIO holder, given actions to do for various tasks
            /// </summary>
            /// <param name="getBit"></param>
            /// <param name="setBit"></param>
            /// <param name="getDir"></param>
            /// <param name="setDir"></param>
            internal GPIOHolder(Func<bool> getBit, Action<bool> setBit, Func<DirectionType> getDir, Action<DirectionType> setDir)
                {
                this.getBit = getBit;
                this.setBit = setBit;
                this.getDir = getDir;
                this.setDir = setDir;
                }
            
            // Actions/Functions for handling get/set
            Func<bool> getBit;
            Action<bool> setBit;
            Func<DirectionType> getDir;
            Action<DirectionType> setDir;
            }
        
        /// <summary>
        /// The GPIO entries on the gadget
        /// </summary>
        public List<GPIOHolder> GPIO {get; private set; }

        /// <summary>
        /// Run the plasma.
        /// </summary>
        public void RunPlasma()
            {
            WriteCommand(3, 0, 8, 1);
            }

        /// <summary>
        /// Reset the remote gadget
        /// </summary>
        public void Reset()
            {
            WriteCommand(3, 0, 8, 2);
            }

        #region Non public Implementation

        #region Unbuffered Stream
        // This is needed, unfortunately, to get unbuffered Disk access to make the image fast
        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr CreateFile(
        string FileName, // file name
        uint DesiredAccess, // access mode
        uint ShareMode, // share mode
        IntPtr SecurityAttributes, // Security Attr
        uint CreationDisposition, // how to create
        uint FlagsAndAttributes, // file attributes
        IntPtr hTemplate // template file
        );

        /// <summary>
        /// Get an unbuffered stream for file reads and writes writes
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        FileStream GetUnbufferedStream(string filename, FileMode mode, FileAccess access)
            {
            uint FILE_FLAG_NO_BUFFERING = 0x20000000;
            IntPtr handlePtr = CreateFile(filename,
                    (uint)access,
                    (uint)FileShare.None,
                    IntPtr.Zero,
                    (uint)mode,
                    FILE_FLAG_NO_BUFFERING,
                    IntPtr.Zero);
            SafeFileHandle handle = new SafeFileHandle(handlePtr, true);
            FileStream stream = new FileStream(handle, access, 2048, false);
            return stream;
            }

        #endregion

        /// <summary>
        /// Helper function to set/clear a given bit
        /// </summary>
        /// <param name="data"></param>
        /// <param name="startOffset"></param>
        /// <param name="startBit"></param>
        /// <param name="bitLength"></param>
        /// <param name="value"></param>
        void SetField(byte[] data, int startOffset, int startBit, int bitLength, uint value)
            {
            Debug.WriteLine("Set: {0} {1} {2} {3}", startOffset, startBit, bitLength, value);
            // process one bit at a time
            while (bitLength-- > 0)
                {
                var bit = (value & 1) << startBit;
                var mask =(uint)(~(1 << startBit));
                data[startOffset] = (byte)((data[startOffset] & mask) | (byte)bit);
                value >>= 1;
                startBit++;
                if (startBit == 8)
                    {
                    startOffset++;
                    startBit = 0;
                    }
                }
            }

        /// <summary>
        /// Helper function to get some bitfields
        /// </summary>
        /// <param name="data"></param>
        /// <param name="byteOffset"></param>
        /// <param name="bit"></param>
        /// <returns></returns>
        uint GetField(byte[] data, int startOffset, int startBit, int bitLength)
            {
            uint value = 0;
            int finalShift = 32 - bitLength;
            // process one bit at a time
            while (bitLength-- > 0)
                {
                value >>= 1;
                value |= (uint)(((data[startOffset] >> startBit) & 1)) << 31; // set high bit
                startBit++;
                if (startBit == 8)
                    {
                    startOffset++;
                    startBit = 0;
                    }
                }
            // shift down final bits
            value >>= finalShift;
            return value;
            }


        /// <summary>
        /// Create the GPIO structures
        /// </summary>
        void CreateGPIO()
            {
            GPIO = new List<GPIOHolder>();
            for (int i = 0; i < 8; ++i)
                {
                int localIndex = i; // need local copy due to delegate closure
                GPIO.Add(
                    new GPIOHolder(
                        // get bit action
                        () => ReadCommand(0, localIndex, 1) == 1,
                        // set bit action
                        v => WriteCommand(0, localIndex, 1, v ? 1U : 0U),
                        // get direction action
                        () => ReadControl(10, localIndex, 1) == 1 ? GPIOHolder.DirectionType.Input : GPIOHolder.DirectionType.Output,
                        // set direction action
                        v => WriteControl(10, localIndex, 1, v == GPIOHolder.DirectionType.Input ? 1U : 0U)
                    ));
                }
            }



        /// <summary>
        /// Helper function to read values from control file
        /// </summary>
        /// <param name="startOffset"></param>
        /// <param name="startBit"></param>
        /// <param name="bitLength"></param>
        /// <returns></returns>
        uint ReadControl(int startOffset, int startBit, int bitLength)
            {
            var data = ReadFile(ControlFileSize, controlFile);
            return GetField(data, startOffset, startBit, bitLength);
            }

        /// <summary>
        /// Helper function to write control file
        /// </summary>
        /// <param name="startOffset"></param>
        /// <param name="startBit"></param>
        /// <param name="bitLength"></param>
        /// <param name="value"></param>
        void WriteControl(int startOffset, int startBit, int bitLength, uint value)
            {
            var data = ReadFile(ControlFileSize, controlFile);
            SetField(data, startOffset, startBit, bitLength, value);
            WriteFile(data, ControlFileSize, controlFile);
            }

        /// <summary>
        /// Helper function to read values from command file
        /// </summary>
        /// <param name="startOffset"></param>
        /// <param name="startBit"></param>
        /// <param name="bitLength"></param>
        /// <returns></returns>
        uint ReadCommand(int startOffset, int startBit, int bitLength)
            {
            var data = ReadFile(CommandFileSize, commandFile);
            return GetField(data, startOffset, startBit, bitLength);
            }

        /// <summary>
        /// Helper function to write command file
        /// </summary>
        /// <param name="startOffset"></param>
        /// <param name="startBit"></param>
        /// <param name="bitLength"></param>
        /// <param name="value"></param>
        void WriteCommand(int startOffset, int startBit, int bitLength, uint value)
            {
            var data = ReadFile(CommandFileSize, commandFile);
            SetField(data, startOffset, startBit, bitLength, value);
            WriteFile(data, CommandFileSize, commandFile);
            }

        /// <summary>
        /// IMAGE file name
        /// </summary>
        static string imageName = @"IMAGE.DAT";

        /// <summary>
        /// COMMAND file name
        /// </summary>
        static string commandName = @"COMMAND.DAT";

        /// <summary>
        /// CONTROL file name
        /// </summary>
        static string controlName = @"CONTROL.DAT";

        /// <summary>
        /// INFO file name
        /// </summary>
        static string infoName = @"INFO.TXT";

        /// <summary>
        /// DEBUG file name
        /// </summary>
        static string debugName = @"DEBUG.DAT";

        /// <summary>
        /// The filenames needed to find in a root directory for the gadget
        /// </summary>
        static List<string> fileNames = new List<string>()
        {
            imageName, 
            commandName, 
            controlName,
            infoName,
            debugName,
            @"README.TXT",
            @"PIC32.CS"
        };
        // This is the default Volume name of the gadget.
        static string VolumeName = @"8 CUBE";

        /// <summary>
        /// Return gadget disk name or null. Takes optional drive name, 
        /// otherwise checks all drives
        /// </summary>
        /// <param name="driveName">Optional drive name</param>
        /// <returns>The string of the gadget drive, or null if not found</returns>
        string FindDisk(string driveName = null)
            {
            OnMessageEvent("Checking drives...");

            string[] drives;
            if (driveName == null)
                drives = Directory.GetLogicalDrives();
            else
                drives = new string[1] { driveName };
            Array.Reverse(drives); // makes faster by looking at floppies last
            foreach (string driveNameTemp in drives)
                {
                OnMessageEvent(driveNameTemp + "....");
                if (IsGadgetDrive(driveNameTemp))
                    {
                    OnMessageEvent("Found!");
                    GadgetDrive = driveNameTemp;
                    return driveNameTemp;
                    }
                }
            OnMessageEvent("Failed!");
            return null;
            }

        /// <summary>
        /// Check if the given drive name is the gadget drive.
        /// Return true if it is, else false.
        /// </summary>
        /// <param name="driveName"></param>
        /// <returns></returns>
        bool IsGadgetDrive(string driveName)
            {
            if (new DriveInfo(driveName).VolumeLabel != VolumeName)
                return false;
            bool foundAll = true;
            foreach (var fn in fileNames)
                foundAll &= File.Exists(driveName + fn);
            return foundAll;
            }

        /// <summary>
        /// Open all files and set gadget drive
        /// </summary>
        /// <param name="drive"></param>
        void OpenFiles(string drive)
            {
            CloseFiles(); // make sure all closed
            // local function to get file sizes
            Func<string,int> FileSize = filename => { FileInfo fi = new FileInfo(drive + filename); return (int)fi.Length; };
            ImageFileSize = FileSize(imageName);
            imageFile = GetUnbufferedStream(drive + imageName, FileMode.Open, FileAccess.Write);
            InfoFileSize = FileSize(infoName);
            infoFile = GetUnbufferedStream(drive + infoName, FileMode.Open, FileAccess.Read);
            DebugFileSize = FileSize(debugName);
            debugFile = GetUnbufferedStream(drive + debugName, FileMode.Open, FileAccess.Read);
            CommandFileSize = FileSize(commandName);
            commandFile = GetUnbufferedStream(drive + commandName, FileMode.Open, FileAccess.ReadWrite);
            ControlFileSize = FileSize(controlName);
            controlFile = GetUnbufferedStream(drive + controlName, FileMode.Open, FileAccess.ReadWrite);
            GadgetDrive = drive;
            IsConnected = true;
            }

        /// <summary>
        /// Close all files and clear gadget drive
        /// </summary>
        void CloseFiles()
            {
            IsConnected = false;
            Close(ref imageFile);
            Close(ref infoFile);
            Close(ref debugFile);
            Close(ref commandFile);
            Close(ref controlFile);
            GadgetDrive = "";
            }

        /// <summary>
        /// Close the given file and set it to null
        /// </summary>
        /// <param name="file"></param>
        void Close(ref FileStream file)
            {
            if (file != null)
                file.Close();
            file = null;
            }


        /// <summary>
        /// Write the given data to the given file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="maxLength"></param>
        /// <param name="file"></param>
        void WriteFile(byte[] data, long maxLength, FileStream file)
            {
            if (IsConnected == false)
                {
                OnMessageEvent("ERROR: cannot write if not connected");
                return;
                }

            // write the image
            file.Seek(0, SeekOrigin.Begin);
            file.Write(data, 0, Math.Min(data.Length, (int)maxLength));
            file.Flush();
            }


        /// <summary>
        /// Read the data from the given file. 
        /// Return null on error
        /// </summary>
        /// <param name="image"></param>
        byte[] ReadFile(long length, FileStream file)
            {
            if (IsConnected == false)
                {
                OnMessageEvent("ERROR: cannot read if not connected");
                return null;
                }

            // read the image
            file.Seek(0, SeekOrigin.Begin);
            byte [] arr = new byte[length];
            file.Read(arr, 0, (int)length);
            return arr;
            }


        /// <summary>
        /// the file to write images to
        /// </summary>
        FileStream imageFile = null;

        /// <summary>
        /// The INFO.TXT readable file
        /// </summary>
        FileStream infoFile = null;

        /// <summary>
        /// The DEBUG.DAT readable file
        /// </summary>
        FileStream debugFile = null;

        /// <summary>
        /// The COMMAND.DAT read/write file
        /// </summary>
        FileStream commandFile = null;

        /// <summary>
        /// The CONTROL.DAT file
        /// </summary>
        FileStream controlFile = null;

        /// <summary>
        /// the size of the image file
        /// </summary>
        int ImageFileSize = 1536;

        /// <summary>
        /// Size of the command file
        /// </summary>
        int CommandFileSize = 512;

        /// <summary>
        /// Size of the control file
        /// </summary>
        int ControlFileSize = 512;

        /// <summary>
        /// Size of the info file
        /// </summary>
        int InfoFileSize = 512;

        /// <summary>
        /// Size of the debug file
        /// </summary>
        int DebugFileSize = 512;

        #endregion

        #region IDisposable interface
        // Implement the standard IDisposable interface. For example, see
        // http://www.codeproject.com/KB/cs/idisposable.aspx


        /// <summary>
        /// This variable detects redundant Dispose calls.
        /// </summary>
        bool disposed = false;

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
            {
            if (!disposed)
                {
                if (disposing)
                    { // dispose managed resources
                    CloseFiles();
                    }
                // dispose unmanaged resources
                // TODO!
                }
            disposed = true;
            // call base.Disposed(disposing) here if there is one 
            }


        /// <summary>
        /// Releases all resources used by the PIC32 gadget interface.
        /// </summary>
        public void Dispose()
            {
            this.Close();
            }

        /// <summary>
        /// This finalizer cleans up any local resources
        /// </summary>
        ~PIC32()
            {
            Dispose(false);
            }

        #endregion
        }
    }
