using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text.Json;
using ManagedNativeWifi;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics.Eventing.Reader;


namespace ESurfingDialerManager;

class Config
{
    public string Java { get; set; } = "java.exe";
    public string ProfileName { get; set; } = "YONG";
    public string User { get; set; } = "username";
    public string Password { get; set; } = "password";
    public string JarFile { get; set; } = "ESurfingDialer-1.9.5-all.jar";
    public string Log4jConfiguration { get; set; } = "log4j.properties";
}
enum ProcessState
{
    Stoped,
    Started,
    Killing,
}
enum WiFiState
{
    Disconnected,
    Changed,
    Connected,
    Connecting,
}
public static class EnumExtensions
{
#nullable enable
    public static TEnum? GetLastEvent<TEnum>(this List<Enum> events) where TEnum : Enum
    {
        var last = events.OfType<TEnum>().LastOrDefault();
        return last;  // 直接返回找到的枚举值
    }
#nullable disable
}
class Model
{
    public Config Config { get; set; } = new Config();

    public string WorkingDirectory { get; set; }
    public AsyncQueue<Enum> EventQueue { get; set; }

    //private WindowsEventListener windowsEventListener;
    private WifiEventListener wifiEventListener;
    private Process process;
    private bool running;

    public void Init()
    {
        //创建事件监听器，通过事件监听器监听Wi-Fi连接的断开与连接。
        //windowsEventListener = new WindowsEventListener();

        wifiEventListener = new WifiEventListener();
        wifiEventListener.WifiEventOccurred += WifiEventListener_WifiEventOccurred;
        //创建事件队列，外部事件通过此队列通知主程序
        EventQueue = new AsyncQueue<Enum>();

        Console.CancelKeyPress += Console_CancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
    }

    private void WifiEventListener_WifiEventOccurred(object sender, WifiEventListener.WifiEventArgs e)
    {
        if (e.ProfileName.Equals(Config.ProfileName))
        {
            EventQueue.Enqueue(e.Action == "Disconnected" ? WiFiState.Disconnected : WiFiState.Changed);
            Console.WriteLine($"网络：{e.ProfileName}：{e.Action}");
        }
    }

    private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        Stop();
        Environment.Exit(0);
    }
    private void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        Stop();
    }

    public Process ESurfingDialerProcess()
    {
        Process process = new Process();
        process.StartInfo.WorkingDirectory = WorkingDirectory;
        process.StartInfo.CreateNoWindow = false;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(e.Data);
                Console.ResetColor();
            }
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Data);
                Console.ResetColor();
            }
        };
        process.StartInfo.FileName = Config.Java;
        process.StartInfo.Arguments =
            $"-Dlog4j.configuration=file:{Config.Log4jConfiguration} " +
            $"-jar {Config.JarFile} " +
            $"--user {Config.User} --password {Config.Password}";
        return process;
    }
    public async Task<bool> StartProcessAsync(Process process)
    {
        try
        {
            process.Start();
            this.process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"启动ESurfingDialer进程失败：{ex.Message}");
            Console.ResetColor();
            throw;//启动进程还能启动失败的吗？我也不知道这种情况怎么处理。
        }
        EventQueue.Enqueue(WiFiState.Connected);
        try
        {
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ESurfingDialer.WaitForExit失败：{ex.Message}");
            Console.ResetColor();
            throw;//我不知道这种情况该怎么处理。
        }
        finally
        {
            process.Close();
            this.process = null;
        }
        return true;
    }
    public void KillProcess(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("eSurfingDialerProcess.Kill()失败：" + ex.Message);
        }
    }
    public void Start()
    {
        running = true;
        wifiEventListener.Start();
    }
    public void Stop()
    {
        if (!running)
            return;
        running = false;
        wifiEventListener.Stop();
        EventQueue.Complete();
        if (process?.HasExited == false)
            process.Kill();
    }


    public async Task LoopAsync()
    {
        Start();
        WiFiState wifiState = WiFiState.Disconnected;

        if (ISProfileConnected(Config.ProfileName))
        {
            Console.WriteLine($"网络：{Config.ProfileName}：Connected");
            EventQueue.Enqueue(WiFiState.Connecting);
        }
        else
        {
            Console.WriteLine($"网络：{Config.ProfileName}：Disconnected");
            EventQueue.Enqueue(WiFiState.Disconnected);
        }

        Process esdProcess = null;
        Task esdTask = null;
        Task<Enum> queueTask = EventQueue.DequeueAsync();
        Task delayTask = null;
        while (running)
        {
            List<Task> tasks = new List<Task>();

            if (esdTask is not null)
                tasks.Add(esdTask);

            tasks.Add(queueTask);

            if (delayTask is not null)
                tasks.Add(delayTask);

            //等待任意一个任务完成
            Task complectedTask = await Task.WhenAny(tasks.ToArray());
            if (!running)
                return;
            bool needADelay = false;

            if (queueTask is not null && queueTask.IsCompleted)
            {
                List<Enum> events = new List<Enum>();
                events.Add(await queueTask);
                Enum nextEnum;
                while (EventQueue.TryDequeue(out nextEnum))
                {
                    events.Add(nextEnum);
                }

                WiFiState? lastWiFiEvent = events.GetLastEvent<WiFiState>();
                if (lastWiFiEvent is not null)
                {
                    //WiFi状态发生了变化(断开了)，记录最新的状态
                    wifiState = (WiFiState)lastWiFiEvent;
                    Console.WriteLine($"ESurfingDialerManager状态变更为：{wifiState}");

                    //需等待并处理
                    needADelay = true;
                }

                //继续等待队列
                queueTask = EventQueue.DequeueAsync();
            }

            if (esdTask is not null && esdTask.IsCompleted)
            {
                await esdTask;
                Console.WriteLine("ESurfingDialer进程退出了。");
                //ESurfingDialer进程退出了，不管他是正常退出还是异常退出，都对相关内容进行清理
                esdProcess = null;
                esdTask = null;

                if (wifiState == WiFiState.Connected)
                {
                    //状态变更为Connecting。
                    wifiState = WiFiState.Connecting;
                    Console.WriteLine($"ESurfingDialerManager状态变更为：{wifiState}");
                    needADelay = true;
                }
            }

            if (needADelay)
            {
                //创建等待任务。该任务5秒后完成。
                delayTask = Task.Delay(5000);
            }
            else if (delayTask is not null && delayTask.IsCompleted)
            {
                //【等待任务】创建后，5秒倒计时，期间没有发生其它事件。
                await delayTask;
                delayTask = null;
                if (wifiState == WiFiState.Disconnected)
                {
                    //Wi-Fi断开了，如果ESurfingDialer进程依然正在运行，把它结束
                    if (esdProcess is not null)
                    {
                        KillProcess(esdProcess);
                    }
                }
                else if (wifiState == WiFiState.Changed)
                {
                    if (esdProcess is not null)
                    {
                        //Wi-Fi曾断开过，但是现在又重连了，为了确保重新认证，结束ESurfingDialer进程。
                        KillProcess(esdProcess);
                    }
                    wifiState = WiFiState.Connecting;
                    Console.WriteLine($"ESurfingDialerManager状态变更为：{wifiState}");
                    //创建等待任务。
                    delayTask = Task.Delay(5000);
                }
                else if (wifiState == WiFiState.Connecting)
                {
                    process = esdProcess = ESurfingDialerProcess();
                    esdTask = StartProcessAsync(esdProcess);
                }
            }
        }
    }

    public static bool ISProfileConnected(string name)
    {
        foreach (var interfaceId in NativeWifi.EnumerateInterfaces()
            .Where(x => x.State is InterfaceState.Connected)
            .Select(x => x.Id))
        {
            // Following methods work only with connected wireless interfaces.
            var (result, cc) = NativeWifi.GetCurrentConnection(interfaceId);
            if (result is ActionResult.Success && cc.ProfileName.Equals(name))
            {
                return true;
            }
        }
        return false;
    }
}
internal class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("缺少配置文件");
            Console.WriteLine($"示例: {Process.GetCurrentProcess().ProcessName} C:\\path\\to\\ESurfingDialerManager.json");
            Console.ResetColor();
            Environment.Exit(0);
        }
        string path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine($"配置文件不存在：{path}");
            Environment.Exit(0);
        }

        Config config = null;
        try
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
                {
                    Modifiers = {
                            ti =>{
                                foreach (var prop in ti.Properties){
                                    prop.IsRequired = true;
                                }
                            }
                        }
                }
            };
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), options);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"配置文件格式有误：{path}:\n{ex.Message}");
            Console.WriteLine($"参考：\n" + JsonSerializer.Serialize(new Config()
                , new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
        }


        Model model = new Model();
        model.Config = config;
        model.WorkingDirectory = Path.GetDirectoryName(path);
        model.Init();
        await model.LoopAsync();
        Environment.Exit(0);
    }

}