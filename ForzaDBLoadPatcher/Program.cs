using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ForzaDBLoadPatcher
{
    // Quick and sloppy tool which will not even check if steam/FH4 is installed
    // Made in like 15 minutes so eh

    internal class Program
    {
        const int PROCESS_WM_READ = 0x0010;
        const int PROCESS_ALL_ACCESS = 0x1F0FFF;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
          IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        const int FH4Address = 0x305C6DE;
        const int FH4SteamAppId = 1293830;
        const string FH4ProcessName = "ForzaHorizon4";

        static void Main(string[] args)
        {
            Console.WriteLine("FH4 DB Load Patcher - FH4 - by Nenkai");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("- https://twitter.com/Nenkaai");
            Console.WriteLine("-----------------------------");
            Console.WriteLine("NOTE: Remember to revert to the original .slt file if not booting through this!");

            KillIfExists();

            Console.WriteLine("Starting FH4 from steam");
            var steamProcess = Process.Start(new ProcessStartInfo()
            {
                FileName = @$"steam://rungameid/{FH4SteamAppId}",
                UseShellExecute = true,
                Verb = "open"
            });
            Thread.Sleep(8000);

            int attempts = 100;
            while (attempts > 0)
            {
                var processes = Process.GetProcessesByName(FH4ProcessName);
                if (processes.Length > 0)
                {
                    Console.WriteLine("ForzaHorizon4 detected");
                    Process process = processes[0];
                    ProcessForza4(process);
                    return;
                }

                Thread.Sleep(200);
                attempts--;
            }

            Console.WriteLine("Failed to start and patch FH4, aborting.");
        }

        static void KillIfExists()
        {
            var processes = Process.GetProcessesByName(FH4ProcessName);
            if (processes.Length > 0)
            {
                Console.WriteLine("Forza exists, killed it");
                processes[0].Kill();
                Thread.Sleep(3000);
            }
        }

        static void ProcessForza4(Process process)
        {
            Console.WriteLine("Applying patch");
            ProcessModule mainModule = process.MainModule;
            IntPtr baseAddress = mainModule.BaseAddress;
            IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

            int bytesRead = 0;
            byte[] buffer = new byte[0x07]; // cmp     byte ptr [rax+2EF4h], 0
            if (!ReadProcessMemory((int)processHandle, baseAddress + FH4Address, buffer, buffer.Length, ref bytesRead))
            {
                Console.WriteLine("Failed to read process memory");
                return;
            }

            while (true)
            {
                // Wait for arxan unpack
                if (!ReadProcessMemory((int)processHandle, baseAddress + FH4Address, buffer, buffer.Length, ref bytesRead))
                {
                    Console.WriteLine("Failed to read process memory");
                    return;
                }

                if (buffer[6] == 0)
                {
                    /* change "usegamedbencryption" command line arg flag check to pass as a no

                       notes: rax is pointer to AppCommandLineParameters
                              AppCommandLineParameters->CForzaCommandLineParameter ctor is manually marked as arxan protected
                              AppCommandLineParameters->0x2EF4 = 'usegamedbencryption' - can be seen in OpusDev
                              command line arg parsing is disabled in retail builds, but all the flags seem to remain 
                    */

                    // call    GetAppCommandLineParameters
                    buffer[6] = 0x01; // cmp     byte ptr [rax+2EF4h], 0  --> cmp     byte ptr [rax+2EF4h], 1
                    // jz      loc_7FF7E51DC466

                    // TODO maybe: same function has another command line arg check to read from 'game:\media\db\patch\', may be worth enabling in the future

                    if (!WriteProcessMemory(processHandle, baseAddress + FH4Address, buffer, buffer.Length, out IntPtr lpNumberOfBytesWritten))
                    {
                        Console.WriteLine("Failed to write process memory");
                        return;
                    }

                    Console.WriteLine("Applied patch. Waiting 20 seconds to let the game boot");
                    Thread.Sleep(20000);

                    Console.WriteLine("Reverting edit to avoid game crash due to possible module verification");
                    buffer[6] = 0x00;
                    if (!WriteProcessMemory(processHandle, baseAddress + FH4Address, buffer, buffer.Length, out _))
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
    }
}