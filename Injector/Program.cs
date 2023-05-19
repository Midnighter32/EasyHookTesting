using EasyHook;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;

internal class Program
{
    private static unsafe void Main(string[] args)
    {
        Int32 targetPID = 0;
        string targetExe = null;
        string targetDll = null;

        string channelName = null;

        ProcessArgs(args, out targetPID, out targetExe, out targetDll);

        if (targetPID <= 0 && string.IsNullOrEmpty(targetExe) || string.IsNullOrEmpty(targetDll))
            return;

        RemoteHooking.IpcCreateServer<HookTesting.ServerInterface>(ref channelName, WellKnownObjectMode.Singleton);

        string injectionLibrary = targetDll;

        try
        {
            if (targetPID > 0)
            {
                Console.WriteLine("Attempting to inject into process {0}", targetPID);

                RemoteHooking.Inject(
                    targetPID,
                    injectionLibrary,
                    injectionLibrary,
                    channelName
                );
            }
            else if (!string.IsNullOrEmpty(targetExe))
            {
                Console.WriteLine("Attempting to create and inject into {0}", targetExe);

                RemoteHooking.CreateAndInject(
                    targetExe,
                    "",
                    0,
                    InjectionOptions.DoNotRequireStrongName,
                    injectionLibrary,
                    injectionLibrary,
                    out targetPID,
                    channelName
                );
            }
        }
        catch ( Exception e )
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("There was an error while injecting into target:");
            Console.ResetColor();
            Console.WriteLine(e.ToString());
        }

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("<Press any key to exit>");
        Console.ResetColor();
        Console.ReadKey();

        Process.GetProcessById(targetPID)?.Kill();
    }

    static void ProcessArgs(string[] args, out int targetPID, out string targetExe, out string targetDll)
    {
        targetPID = 0;
        targetExe = null;
        targetDll = null;

        while ((args.Length != 2) || !(Int32.TryParse(args[0], out targetPID) || File.Exists(args[0])) || !File.Exists(args[1]))
        {
            if (targetPID > 0 && File.Exists(args[1]))
            {
                break;
            }
            if (args.Length != 2 || !File.Exists(args[0]) || !File.Exists(args[1]))
            {
                Console.WriteLine("Usage: Injector ProcessID PathToLibrary");
                Console.WriteLine("   or: Injector PathToExecutable PathToLibrary");
                Console.WriteLine("");
                Console.WriteLine("e.g. : Injector 1234 .\\Lib.dll");
                Console.WriteLine("          to inject dll to an existing process with PID 1234");
                Console.WriteLine(@"  or : Injector ""C:\Windows\Notepad.exe"" .\Lib.dll");
                Console.WriteLine("          create new notepad.exe process using RemoteHooking.CreateAndInject");
                Console.WriteLine();

                if (args.Length != 2)
                {
                    var arr = new string[2];
                    args.CopyTo(arr, 0);
                    args = arr;
                }

                if (targetPID == 0 && !File.Exists(args[0]))
                {
                    Console.WriteLine("Enter a process Id or path to executable");
                    Console.Write("> ");

                    args[0] = Console.ReadLine();
                }

                if (!File.Exists(args[1]))
                {
                    Console.WriteLine("Enter a path to library");
                    Console.Write("> ");

                    args[1] = Console.ReadLine();
                }
            }
        }

        targetExe = args[0];
        targetDll = args[1];
    }
}