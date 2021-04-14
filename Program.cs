using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ExoInterpreter;

namespace WriteMemory
{
    class Program
    {
        #region external libraries&functions
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(int hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(IntPtr hProcess);
        #endregion

        #region flags
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }
        #endregion

        static void Main(string[] args)
        {
            ExoInterpreter.WriteMemory.EProcess[] eprocesses = null;
            if (args.Length > 0)
            {
                eprocesses = ExoInterpreter.WriteMemory.Run(File.ReadAllText(args[0]));
            }
            long[] address = new long[0];
            Process[] process = new Process[0];

            if (eprocesses == null)
            {
                Array.Resize(ref eprocesses, 1);
                Console.Write("Process name: ");
                eprocesses[0].Name = Console.ReadLine();
                Console.Write("Address: ");
                eprocesses[0].Address = Console.ReadLine();
                Console.Write("Is value constant? (true/false): ");
                eprocesses[0].Constant = bool.Parse(Console.ReadLine());
            }

            Array.Resize(ref address, eprocesses.Length);
            Array.Resize(ref process, eprocesses.Length);

            for (int i = 0; i < eprocesses.Length; i++)
            {
                #region fix
                string newaddress = eprocesses[i].Address;
                if (eprocesses[i].Address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    newaddress = eprocesses[i].Address.Substring(2);
                }
                #endregion

                address[i] = long.Parse(newaddress, NumberStyles.HexNumber);
                process[i] = Process.GetProcessesByName(eprocesses[i].Name).FirstOrDefault();
            }

            Console.Clear();

            while (true)
            {
                for(int i = 0; i < eprocesses.Length; i++)
                {
                    if (process[i] == null)
                    {
                        Console.WriteLine("Process " + eprocesses[i].Name + " not found");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        if (eprocesses[i].Increment)
                        {
                            byte previous = ReadMem(process[i], address[i])[0];
                            Console.WriteLine("0x" + eprocesses[i].Address + " # (" + previous + ") = " + eprocesses[i].IncrementRate);
                            WriteMem(process[i], address[i], int.Parse(previous.ToString()) + eprocesses[i].IncrementRate);
                            Thread.Sleep(eprocesses[i].IncrementTime);
                        }
                        else if (eprocesses[i].Constant)
                        {
                            WriteMem(process[i], address[i], eprocesses[i].ConstantValue);
                            Thread.Sleep(350);
                        }
                    }
                }
            }
        }

        public static void WriteMem(Process p, long address, long value)
        {
            var hProc = OpenProcess(ProcessAccessFlags.All, false, (int)p.Id);
            var val = new byte[] { (byte)value };

            int wtf = 0;
            WriteProcessMemory(hProc, new IntPtr(address), val, (UInt32)val.LongLength, out wtf);

            CloseHandle(hProc);
        }
        
        public static byte[] ReadMem(Process p, long address)
        {
            var hProc = OpenProcess(ProcessAccessFlags.All, false, (int)p.Id);
            byte[] buffer = new byte[24];

            int bytesRead = 0;
            ReadProcessMemory((int)hProc, new IntPtr(address), buffer, buffer.Length, ref bytesRead);

            CloseHandle(hProc);

            return buffer;
        }
    
        //(PROTOTYPE) TO BE IMPROVED
        static long[] searchAddressByValue(Process p, int value, long[] addresses = null, long lastAddress = 3200000)
        {
            long[] newAddresses = new long[0];
            long[] finalAddresses = new long[0];

            #region search addresses with value
            for (long i = 0, j = 0; i < lastAddress; i++)
            {
                int valor = ReadMem(p, 0x00000000 + i)[0];
                if (valor == value)
                {
                    Array.Resize(ref newAddresses, newAddresses.Length + 1);
                    newAddresses[j] = 0x00000000 + i;
                    j++;
                    Console.WriteLine("Value found at: " + (0x00000000 + i) + " = " + valor);
                }
            }
            #endregion

            #region if there are old addresses and whe are looking for a change, finalAddresses will be defined with the matching ones
            int x = 0;
            foreach (long oldAddress in addresses)
            {
                foreach (long newAddress in newAddresses)
                {
                    if (oldAddress == newAddress)
                    {
                        Array.Resize(ref finalAddresses, finalAddresses.Length + 1);
                        finalAddresses[x] = newAddress;
                        x++;
                    }
                }
            }
            #endregion

            #region firscan defgine finalAddresses
            if (finalAddresses.Length <= 0)
            {
                finalAddresses = newAddresses;
                newAddresses = null;
            }
            #endregion

            return finalAddresses;
        }
    }
}
