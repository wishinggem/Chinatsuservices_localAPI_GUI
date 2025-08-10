using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Serialization;
using static MangaAPI;
using Timer = System.Windows.Forms.Timer;

namespace Chinatsuservices_localAPI_GUI
{
    public partial class Main : Form
    {
        private CancellationTokenSource _cts;
        private Timer timer1;
        private MangaAPI api;
        public static int runningAPICount = 0;
        private Timer timeTillNextAPICall;
        private int secondsRemaining = 0;

        public Main()
        {
            InitializeComponent();

            RoundControl(Run_API, 10);
            RoundControl(proccessBar, 10);
            RoundControl(OutputConsole, 10);
            RoundControl(Rebuld_Config, 10);
            RoundControl(Open_Config, 10);

            Output.outputConsole = OutputConsole;
            proccessBar.Minimum = 0;

            timeTillNextAPICall = new Timer();
            timeTillNextAPICall.Interval = 1000; // Update every second
            timeTillNextAPICall.Tick += Timer_Tick;


            RunAPI();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (secondsRemaining > 0)
            {
                secondsRemaining--;

                int minutes = secondsRemaining / 60;
                int seconds = secondsRemaining % 60;
                TimeTillNextCall.Text = $"Time till next auto API Run: {minutes:D2}:{seconds:D2}";
            }
            else
            {

            }
        }

        private void ResetCountdown()
        {
            secondsRemaining = api.GetAPISleepAmount() / 1000; // Convert milliseconds to seconds
        }

        private void RunAPI()
        {
            Output.ChangeColor(Color.White);
            Output.WriteLine("══════════════════════════════════════════════════");
            Output.ChangeColor(Color.Green);
            Output.WriteLine("███ chinatsuservices.ddns.net local API ███");
            Output.WriteLine("Used to control and handle backend services of chinatsuservices.ddns.net Website");
            Output.WriteLine("Created by Chinatsu @ Chinatsuservices");
            Output.ChangeColor(Color.White);
            Output.WriteLine("══════════════════════════════════════════════════");

            api = new MangaAPI();
            api.proccessBar = proccessBar;
            proccessBar.ForeColor = Color.FromArgb(47, 41, 237);
            api.Run_API_Button = Run_API;
            api.defaultColor = Run_API.ForeColor;
            api.SetInits();
            api.SetupLogData();

            Thread.Sleep(10);

            StartApiLoop(api.GetAPISleepAmount());

            ResetCountdown();
            timeTillNextAPICall.Start();
        }

        private void StartApiLoop(int intervalMs)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Reset progress bar safely on UI thread
                        Invoke((Action)(() =>
                        {
                            proccessBar.Maximum = 0;
                            proccessBar.Value = 0;
                        }));

                        // Your API calls
                        api.Log(MangaAPI.LogLevel.info, $"");
                        api.Log(MangaAPI.LogLevel.info,
                            $"Running Managa API Main, Process ID: {Process.GetCurrentProcess().Id}");
                        api.Log(MangaAPI.LogLevel.info, $"");
                        await api.Run();
                        api.Log(LogLevel.info, $"Finished All Manga API Proccesses, If all API proccessses are complete the API will now sleep for {api.GetAPISleepAmount() / 60000} minutes");
                        ResetCountdown();
                    }
                    catch (Exception error)
                    {
                        api.Log(MangaAPI.LogLevel.error, error.Message);
                    }

                    // Wait for the fixed interval
                    await Task.Delay(intervalMs, token);
                }
            }, token);
        }

        private void Run_API_Click(object sender, EventArgs e)
        {
            Color defaultColor = Run_API.ForeColor;
            Run_API.ForeColor = Color.Red;
            try
            {
                api.Log(MangaAPI.LogLevel.info, $"");
                api.Log(MangaAPI.LogLevel.info, $"Manually Invoking API");
                api.Log(MangaAPI.LogLevel.info, $"Running Managa API Main, Proccess ID: {Process.GetCurrentProcess().Id}");
                api.Log(MangaAPI.LogLevel.info, $"");
                api.Run();
            }
            catch (Exception error)
            {
                api.Log(MangaAPI.LogLevel.error, $"{error.Message}");
            }
        }

        private void Rebuld_Config_Click(object sender, EventArgs e)
        {
            api.Log(MangaAPI.LogLevel.info, $"Rebuilding Config");
            api.SetInits();
            api.SetupLogData();
        }

        private void Open_Config_Click(object sender, EventArgs e)
        {
            Process proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = MangaAPI.configPath,
            };

            proc.Start();
        }

        private void Main_Load(object sender, EventArgs e)
        {

        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }

        private void proccessBar_ForeColorChanged(object sender, EventArgs e)
        {
            proccessBar.ForeColor = Color.FromArgb(47, 41, 237);
        }
    }

    public static class Output
    { 
        public static RichTextBox outputConsole;

        public static void WriteLine(string message)
        {
            if (outputConsole.InvokeRequired)
            {
                outputConsole.Invoke(new Action(() => WriteLine(message)));
            }
            else
            {
                outputConsole.AppendText(message + Environment.NewLine);
                outputConsole.ScrollToCaret();
            }
        }

        public static void ChangeColor(Color color)
        {
            outputConsole.SelectionColor = color;
        }

        public static void WriteColored(string message, Color color)
        {
            if (outputConsole.InvokeRequired)
            {
                outputConsole.Invoke(new Action(() => WriteColored(message, color)));
            }
            else
            {
                int start = outputConsole.TextLength;
                outputConsole.AppendText(message);
                int end = outputConsole.TextLength;

                outputConsole.Select(start, end - start);
                outputConsole.SelectionColor = color;
                outputConsole.SelectionLength = 0;
                outputConsole.ScrollToCaret();
            }
        }
    }
}
