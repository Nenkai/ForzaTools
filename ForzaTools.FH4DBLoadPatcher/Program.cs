using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ForzaTools.FH4DBLoadPatcher
{
    // Quick and sloppy tool which will not even check if steam/FH4 is installed
    // Made in like 15 minutes so eh

    internal class Program
    {
        private const int ProcessAllAccess = 0x1F0FFF;

        [DllImport("kernel32.dll")]
        private static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(int hProcess,
            IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
          IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        //ForzaHorizon4.exe+305D1DE 
        private const int Fh4Address = 0x305D1DE;
        private const int Fh4SteamAppId = 1293830;
        private const string Fh4ProcessName = "ForzaHorizon4";

        private static void Main(string[] args)
        {
            Console.WriteLine("FH4 DB Load Patcher - FH4 - by Nenkai");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("- https://twitter.com/Nenkaai");
            Console.WriteLine("-----------------------------");
            Console.WriteLine("NOTE: Remember to revert to the original .slt file if not booting through this!");

            try
            {
                KillIfExists();

                if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    if (!File.Exists(args[0]))
                    {
                        Console.WriteLine($"Executable file '{args[0]}' does not exist");
                        return;
                    }

                    if (!args[0].EndsWith(".exe"))
                    {
                        Console.WriteLine($"Invalid file provided");
                        return;
                    }


                    BootFromPath(args[0]);
                }
                else
                {
                    BootFromSteam();
                }

                int attempts = 100;
                while (attempts > 0)
                {
                    var processes = Process.GetProcessesByName(Fh4ProcessName);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine("ForzaHorizon4 detected");
                        Process process = processes[0];
                        Patch(process);
                        return;
                    }

                    Thread.Sleep(200);
                    attempts--;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return;
            }

            Console.WriteLine("Failed to start and patch FH4, aborting.");
        }

        /// <summary>
        /// Boots forza from the specified path
        /// </summary>
        /// <param name="path"></param>
        static void BootFromPath(string path)
        {
            Console.WriteLine($"Starting from {path}");
            var fh4process = Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = Path.GetDirectoryName(path),
                FileName = path
            });


            ProcessModule mainModule = fh4process.MainModule;
            IntPtr processHandle = OpenProcess(ProcessAllAccess, false, fh4process.Id);

            // Non-genuine versions might be unpacked rather quickly, check for it
            bool hasExited = fh4process.WaitForExit(1500);
            if (!hasExited)
            {
                if (IsUnpacked(processHandle, mainModule.BaseAddress))
                {
                    Console.WriteLine("FH4 is running and already unpacked, proceed to immediate patch");
                    Patch(fh4process);
                    return;
                }
            }

            hasExited = fh4process.WaitForExit(10000);
            if (hasExited)
            {
                // Probably started genuine steam exe
                Console.WriteLine("FH4 process has exited, probably steam bootstrap/restarting.");

                // Wait a bit until it has at least started
                Thread.Sleep(3000);
            }
        }

        /// <summary>
        /// Boots forza from steam
        /// </summary>
        static void BootFromSteam()
        {
            Console.WriteLine("Attempting to start FH4 from steam");
            Console.WriteLine("NOTE: Provide executable path to start from an executable instead");

            var steamProcess = Process.Start(new ProcessStartInfo()
            {
                FileName = @$"steam://rungameid/{Fh4SteamAppId}",
                UseShellExecute = true,
                Verb = "open"
            });
        }

        /// <summary>
        /// Waits for the process to be unpacked and patches it
        /// </summary>
        /// <param name="process"></param>
        private static void Patch(Process process)
        {
            Console.WriteLine("Applying patch");
            ProcessModule mainModule = process.MainModule;
            IntPtr baseAddress = mainModule.BaseAddress;
            IntPtr processHandle = OpenProcess(ProcessAllAccess, false, process.Id);

            while (true)
            {
                // Wait for arxan unpack
                if (IsUnpacked(processHandle, baseAddress))
                {
                    /* change "usegamedbencryption" command line arg flag check to pass as a no

                       notes: rax is pointer to AppCommandLineParameters
                              AppCommandLineParameters->CForzaCommandLineParameter ctor is manually marked as arxan protected
                              AppCommandLineParameters->0x2EF4 = 'usegamedbencryption' - can be seen in OpusDev
                              command line arg parsing is disabled in retail builds, but all the flags seem to remain 
                    */

                    byte[] cmp = { 0x80, 0xb8, 0xf4, 0x2e, 0x00, 0x00, 0x01 };  // cmp     byte ptr [rax+2EF4h], 0  --> cmp     byte ptr [rax+2EF4h], 1

                    // TODO maybe: same function has another command line arg check to read from 'game:\media\db\patch\', may be worth enabling in the future

                    if (!WriteProcessMemory(processHandle, baseAddress + Fh4Address, cmp, cmp.Length, out _))
                    {
                        Console.WriteLine("Failed to write process memory");
                        return;
                    }

                    Console.WriteLine("Applied patch. Waiting 20 seconds to let the game boot");
                    Thread.Sleep(20000);

                    Console.WriteLine("Reverting edit to avoid game crash due to possible module verification");
                    cmp[6] = 0x00;
                    if (!WriteProcessMemory(processHandle, baseAddress + Fh4Address, cmp, cmp.Length, out _))
                    {
                        Console.WriteLine("Failed to write process memory");
                        return;
                    }
                    break;
                }

                Thread.Sleep(100);
            }


            Console.WriteLine("Done.");
        }

        /// <summary>
        /// Checks and kills forza if running
        /// </summary>
        static void KillIfExists()
        {
            var processes = Process.GetProcessesByName(Fh4ProcessName);
            if (processes.Length <= 0) return;
            Console.WriteLine("Forza running, killed it");
            processes[0].Kill();
            Thread.Sleep(3000);
        }

        /// <summary>
        /// Checks if the process is unpacked in memory incl. arxan
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="baseAddress"></param>
        /// <returns></returns>
        static bool IsUnpacked(IntPtr processHandle, IntPtr baseAddress)
        {
            var bytesRead = 0;
            var buffer = new byte[0x07]; // cmp     byte ptr [rax+2EF4h], 0
            
            if (ReadProcessMemory((int)processHandle, baseAddress + Fh4Address, buffer, buffer.Length, ref bytesRead))
            {
                return buffer[5] == 0 && buffer[6] == 0;
            }

            Console.WriteLine("Failed to read process memory");
            return false;

        }

    }
}