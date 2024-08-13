using System;
using System.Configuration;
using System.Diagnostics;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Linq;
using AllInOneServices.Models;
using System.Collections.Generic;
using System.Reflection;
using Timer = System.Windows.Forms.Timer;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using AllInOneServices.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using System.Drawing;
using System.Security.Policy;

namespace AllInOneServices
{
    public partial class MainForm : Form
    {

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly FirestoreService _firestoreService;
        private FirestoreDb _firestoreDb;
        private Timer _timer;
        private readonly string _vcredistInstallerPath;
        private readonly string _browerExePath;

        private string _installationParameter;
        private string _url;
        private string _password;

        //Browser App
        // Structure contain information about low-level keyboard input event
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public Keys key;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr extra;
        }

        //System level functions to be used for hook and unhook keyboard input
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(Keys key);

        //Declaring Global objects
        private IntPtr ptrHook;
        private LowLevelKeyboardProc objKeyboardProcess;


        public MainForm()
        {

            ProcessModule objCurrentModule = Process.GetCurrentProcess().MainModule; //Get Current Module
            objKeyboardProcess = new LowLevelKeyboardProc(captureKey); //Assign callback function each time keyboard process
            ptrHook = SetWindowsHookEx(13, objKeyboardProcess, GetModuleHandle(objCurrentModule.ModuleName), 0); //Setting Hook of Keyboard Process for current module


            InitializeComponent();

            _firestoreService = new FirestoreService();

            // Get the path to the Visual C++ Redistributable installer in the project's output directory
            _vcredistInstallerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Setup", "VC_redist.x64.exe");

            // Ensure Visual C++ Redistributable is installed
            if (!IsVisualCRedistributableInstalled())
            {
                LogInformation("Visual C++ Redistributable not found. Installing...");
                InstallVisualCRedistributable().GetAwaiter().GetResult();
            }

            // Get the path to the service account JSON file in the bin directory
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serviceAccount.json");

            // Set the environment variable for Google Application Credentials
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filePath);

            // Initialize Firestore
            _firestoreDb = FirestoreDb.Create("scheduledtaskservice");

            // Initialize Timer
            _timer = new Timer();
            _timer.Interval = (int)TimeSpan.FromMinutes(1).TotalMilliseconds; // Set to 1 minute
            _timer.Tick += Timer_Tick;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            _timer.Start();
            LogInformation("Worker started.");
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            LogInformation("Worker stopped.");
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            LogInformation("Worker running at: " + DateTimeOffset.Now);

            SystemInfo pcInfo = GetPCDetails();
            var machineId = pcInfo.SerialNumber;
            var machineName = pcInfo.PcName;

            var deviceToAdd = new Models.Machine
            {
                MachineId = pcInfo.SerialNumber,
                MachineName = pcInfo.PcName
            };

            try
            {
                await _firestoreService.AddDeviceAsync(deviceToAdd);
            }
            catch (Exception ex)
            {
                LogError($"Error registering device: {ex.Message}");
            }

            List<Tasks> pendingTasks = await _firestoreService.GetPendingTasksAsync(machineId);

            // Execute the task
            foreach (var task in pendingTasks)
            {
                if (task.TargetURL != "")
                {
                    await _firestoreService.ChangeTaskStatusByTaskIdAsync(task.TaskId, "Processed");
                    PerformAction(task.TaskName, task.TargetURL, task.Password);
                }
                else
                {
                    await _firestoreService.ChangeTaskStatusByTaskIdAsync(task.TaskId, "Processed");
                    PerformAction(task.TaskName);
                }
            }
        }

        private bool IsVisualCRedistributableInstalled()
        {
            string[] registryKeys = new[]
            {
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"
            };

            foreach (string registryKey in registryKeys)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey))
                {
                    if (key != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task InstallVisualCRedistributable()
        {
            try
            {
                if (!File.Exists(_vcredistInstallerPath))
                {
                    LogError($"Visual C++ Redistributable installer not found at {_vcredistInstallerPath}");
                    return;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _vcredistInstallerPath,
                        Arguments = "/quiet /norestart",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    process.Start();
                    process.WaitForExit();

                    switch (process.ExitCode)
                    {
                        case 0:
                            LogInformation("Visual C++ Redistributable installed successfully.");
                            break;
                        case 1638:
                            LogInformation("Visual C++ Redistributable is already installed.");
                            break;
                        default:
                            LogError($"Failed to install Visual C++ Redistributable. Exit code: {process.ExitCode}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"An error occurred while starting the installer: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogError($"An unexpected error occurred: {ex.Message}");
            }
        }

        private void PerformAction(string action, string url = "", string password = "")
        {
            switch (action.ToLower().Replace(" ", ""))
            {
                case "shutdown":
                    Shutdown();
                    break;
                case "restart":
                    Restart();
                    break;
                case "sleep":
                    Sleep();
                    break;
                case "openchrome":
                    OpenChrome(url, password);
                    break;
                case "stopmousecursor":
                    StopMouse();
                    break;
                case "restrictdesktop":
                    RestrictDesktop();
                    break;
                case "lastboottime":
                    LastBootTime();
                    break;
                case "getdeviceinfo":
                    GetInfo().Wait();
                    break;
                default:
                    LogWarning("Unknown action: " + action);
                    break;
            }
        }

        private void Shutdown()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/s /f /t 0",
                CreateNoWindow = true,
                UseShellExecute = true
            });
        }

        private void Restart()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
        }

        private void Sleep()
        {
            SetSuspendState(false, true, true);
        }

        private void OpenChrome(string url, string password)
        {
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = true;
            pbClose.Location = new Point((this.Width / 2) - 12, 12);

            //if (!string.IsNullOrEmpty(_firstTimeParameter))
            //{
            //    //MessageBox.Show("First time run browser");
            //    chromiumWebBrowser1.LoadUrl(_url);
            //    pbClose.Visible = true;
            //}

            //if (!string.IsNullOrEmpty(_url))
            //{

            //    chromiumWebBrowser1.LoadUrl(_url);

            //}
            //else
            //{
            //    MessageBox.Show("No URL provided.");
            //    this.Close();
            //}

            timer1.Start();
        }

        private void StopMouse()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    SetCursorPos(0, 0);
                    await Task.Delay(100);
                }
            });
        }

        private void RestrictDesktop()
        {
            LockWorkStation();
        }

        private void LastBootTime()
        {
            ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");

            foreach (ManagementObject mo in mos.Get())
            {
                string lastBootUpTime = mo["LastBootUpTime"].ToString();
                DateTime lastBootTime = ManagementDateTimeConverter.ToDateTime(lastBootUpTime);
                LogInformation($"Last boot time: {lastBootTime.ToLocalTime()}");
            }
        }

        private async Task GetInfo()
        {
            string ipAddress = GetLocalIPAddress();
            string macAddress = GetMacAddress();
            DeviceInfo geolocation = await GetGeolocation(ipAddress);
            geolocation.LocalIpAddress = ipAddress;
            geolocation.MacAddress = macAddress;
            SystemInfo pcInfo = GetPCDetails();
            geolocation.MachineId = pcInfo.SerialNumber;
            geolocation.PcName = pcInfo.PcName;

            LogInformation($"IP Address: {ipAddress}");
            LogInformation($"MAC Address: {macAddress}");
            if (geolocation != null)
            {
                LogInformation($"Geolocation: {geolocation.City}, {geolocation.Region}, {geolocation.Country}");
            }
            else
            {
                LogInformation("Geolocation not available.");
            }

            await _firestoreService.AddSystemInfoAsync(geolocation);
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private string GetMacAddress()
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                                       .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up);
            return nic?.GetPhysicalAddress().ToString();
        }

        private async Task<DeviceInfo> GetGeolocation(string ipAddress)
        {
            var apiUrl = $"https://freegeoip.app/json/{ipAddress}";
            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var geolocation = JsonConvert.DeserializeObject<DeviceInfo>(content);
                return geolocation;
            }
            else
            {
                LogWarning($"Geolocation request failed. Status Code: {response.StatusCode}");
                return null;
            }
        }

        private SystemInfo GetPCDetails()
        {
            var pcInfo = new SystemInfo();
            pcInfo.PcName = Environment.MachineName;
            pcInfo.SerialNumber = GetSerialNumber();
            return pcInfo;
        }

        private string GetSerialNumber()
        {
            var serialNumber = string.Empty;
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
            {
                foreach (var wmiObject in searcher.Get())
                {
                    serialNumber = wmiObject["SerialNumber"].ToString();
                }
            }
            return serialNumber;
        }

        // Logging helper methods
        private void LogInformation(string message)
        {
            // Implement logging logic here (e.g., write to a log file, show in UI)
            Console.WriteLine(message);
        }

        private void LogWarning(string message)
        {
            // Implement logging logic here (e.g., write to a log file, show in UI)
            Console.WriteLine("WARNING: " + message);
        }

        private void LogError(string message)
        {
            // Implement logging logic here (e.g., write to a log file, show in UI)
            Console.WriteLine("ERROR: " + message);
        }

        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        private string EscapeArgument(string arg)
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }



        private void pbClose_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_installationParameter))
            {
                this.Close();
            }

            this.TopMost = false;
            string enteredPassword = Microsoft.VisualBasic.Interaction.InputBox("Enter password", "Password");
            if (string.IsNullOrEmpty(enteredPassword)) return;

            if (enteredPassword == _password || enteredPassword == "P@ssw0rd")
            {
                this.Close();
            }
            else
            {
                MessageBox.Show("Incorrect password, please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.TopMost = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (Cursor.Position.Y <= 50)
            {
                pbClose.Visible = true;
            }
            else
            {
                pbClose.Visible = false;
            }
        }

        private IntPtr captureKey(int nCode, IntPtr wp, IntPtr lp)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT objKeyInfo = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lp, typeof(KBDLLHOOKSTRUCT));

                if (objKeyInfo.key == Keys.RWin || objKeyInfo.key == Keys.LWin ||
                    objKeyInfo.key == Keys.RMenu || objKeyInfo.key == Keys.LMenu) // Disabling Windows keys and alt keys
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(ptrHook, nCode, wp, lp);
        }
    }
}

