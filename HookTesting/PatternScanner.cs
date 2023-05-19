using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace HookTesting
{
    internal class PatternScanner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        private static IntPtr DllImageAddress(string module, out int moduleSize)
        {
            Process currentProcess = Process.GetCurrentProcess();
            foreach (ProcessModule processModule in currentProcess.Modules)
            {
                if (processModule.FileName == module)
                {
                    moduleSize = processModule.ModuleMemorySize;
                    return processModule.BaseAddress;
                }
            }

            moduleSize = 0;
            return IntPtr.Zero;
        }

        public static unsafe IntPtr GetVTableFuncAddress(IntPtr obj, int funcIndex)
        {
            var pointer = *(IntPtr*)(void*)obj;
            return *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);
        }

        public static IntPtr FindPattern(string pattern, int dataStartOffset, int dataInstructionSize, string moduleName = null)
        {
            var patternAddress = FindPattern(pattern, moduleName);

            return GetAddressFromBytecode(patternAddress, dataStartOffset, dataInstructionSize);
        }

        public static IntPtr GetAddressFromBytecode(IntPtr baseAddress, int dataStartOffset, int dataInstructionSize)
        {
            byte[] memory = ReadBytes(Process.GetCurrentProcess().Handle, baseAddress, dataInstructionSize);
            int offset = BitConverter.ToInt32(memory, dataStartOffset);

            return new IntPtr(baseAddress.ToInt64() + dataInstructionSize + offset);
        }

        public static IntPtr FindPattern(string pattern, string moduleName = null)
        {
            int moduleSize = 0;
            IntPtr baseAddress = IntPtr.Zero;

            IntPtr processHandle = Process.GetCurrentProcess().Handle;

            if (string.IsNullOrEmpty(moduleName))
            {
                baseAddress = Process.GetCurrentProcess().MainModule.BaseAddress;
                moduleSize = Process.GetCurrentProcess().MainModule.ModuleMemorySize;
            }
            else
                baseAddress = DllImageAddress(moduleName, out moduleSize);

            if (baseAddress == IntPtr.Zero)
            {
                throw new ArgumentException("Module not found.");
            }

            return FindPattern(processHandle, baseAddress, moduleSize, pattern);
        }

        public static byte[] ReadBytes(IntPtr processHandle, IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            IntPtr bytesRead;
            ReadProcessMemory(processHandle, address, buffer, size, out bytesRead);
            return buffer;
        }

        public static IntPtr FindPattern(IntPtr processHandle, IntPtr startAddress, int searchRange, string pattern)
        {
            byte[] memoryRegion = ReadBytes(processHandle, startAddress, searchRange);
            byte[] patternBytes = PatternToBytes(pattern);

            for (int i = 0; i < memoryRegion.Length - patternBytes.Length; i++)
            {
                bool found = true;

                for (int j = 0; j < patternBytes.Length; j++)
                {
                    if (patternBytes[j] != 0x00 && memoryRegion[i + j] != patternBytes[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return new IntPtr(startAddress.ToInt64() + i);
                }
            }

            return IntPtr.Zero;
        }

        private static byte[] PatternToBytes(string pattern)
        {
            List<byte> patternBytes = new List<byte>();
            string[] patternParts = pattern.Split(' ');

            foreach (string part in patternParts)
            {
                if (part == "?")
                {
                    patternBytes.Add(0x00);
                }
                else
                {
                    patternBytes.Add(Convert.ToByte(part, 16));
                }
            }

            return patternBytes.ToArray();
        }
    }
}
