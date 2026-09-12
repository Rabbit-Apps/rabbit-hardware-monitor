using HardwareMonitor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if(args.Contains("--render")){
            var square=new System.Windows.Shapes.Rectangle{Width=150,Height=150,Fill=Brushes.CornflowerBlue,RenderTransform=new RotateTransform(),RenderTransformOrigin=new Point(.5,.5)};
            ((RotateTransform)square.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(2)){RepeatBehavior=RepeatBehavior.Forever});
            var window=new Window{Title="FPS rendering test",Width=450,Height=400,Content=new Grid{Background=Brushes.Black,Children={square}}};
            new Application().Run(window);return;
        }
        int assertions=0;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);assertions++;}
        var statistics = new FpsStatistics();
        statistics.Add(double.NaN); statistics.Add(double.PositiveInfinity); statistics.Add(-1);
        Check(statistics.Read().Average == null, "Invalid samples are ignored");
        for (int i=0;i<99;i++) statistics.Add(20);
        Check(statistics.Read().Low == null, "Statistics wait for 100 frames and two seconds");
        statistics.Add(20);
        Check(Math.Abs(statistics.Read().Average!.Value-50)<.001 && Math.Abs(statistics.Read().Low!.Value-50)<.001, "Steady 50 FPS statistics");
        statistics.Reset();
        Check(statistics.Read().Average == null, "Manual reset clears statistics");
        for (int i=0;i<990;i++) statistics.Add(10);
        for (int i=0;i<10;i++) statistics.Add(100);
        Check(Math.Abs(statistics.Read().Average!.Value-1000000d/10900)<.001, "Average uses total frame duration");
        Check(Math.Abs(statistics.Read().Low!.Value-10)<.001, "Slowest one percent uses frame durations");
        statistics.Add(2000);
        Check(statistics.Read().Low < 10, "Long stutters contribute to statistics");
        var statsTracker = new FpsTracker();
        statsTracker.Accept("Application,ProcessID,SwapChainAddress,MsBetweenPresents,PresentMode,msGPUActive",0);
        var statsCandidate = new FpsTracker.Candidate(456,"test.exe",50,0,false,true,"A");
        statsTracker.Statistics(0, statsCandidate,456);
        for(int i=0;i<100;i++) statsTracker.Accept("test.exe,456,A,20,Composed: Flip,1",1);
        for(int i=0;i<100;i++) statsTracker.Accept("test.exe,456,B,100,Composed: Flip,1",1);
        Check(statsTracker.Statistics(1,statsCandidate,456).Average == 50, "Statistics ignore other swap chains");
        Check(statsTracker.Statistics(2,null,456).Average == null, "Paused game hides stale figures");
        Check(statsTracker.Statistics(3,statsCandidate,456).Average == 50, "Brief pause retains session statistics");
        statsTracker.ResetSeconds=30;
        Check(statsTracker.Statistics(30,statsCandidate,456).Average == null, "Automatic interval reset");
        for(int i=0;i<100;i++) statsTracker.Accept("test.exe,456,A,20,Composed: Flip,1",31);
        Check(statsTracker.Statistics(31,statsCandidate,456).Average == 50, "Collect again after timer reset");
        Check(statsTracker.Statistics(32,statsCandidate with { Pid=457 },457).Average == null, "Changing process resets statistics");
        var oldSettings = System.Text.Json.JsonSerializer.Deserialize<FpsSettings>("{\"Enabled\":true,\"Show\":true}")!;
        Check(oldSettings.ShowLive && oldSettings.ShowAverage && oldSettings.ShowLow && oldSettings.ResetSeconds==0, "Old FPS settings retain defaults");
        var choices = new FpsSettings(true,null,true,false,true,false,60);
        Check(System.Text.Json.JsonSerializer.Deserialize<FpsSettings>(System.Text.Json.JsonSerializer.Serialize(choices))==choices, "FPS display and timer choices persist");
        if(args.Length==2&&args[0]=="--csv"){
            var replay=new FpsTracker();foreach(var line in System.IO.File.ReadLines(args[1]))replay.Accept(line,1);
            var actual=replay.Candidates(1);Check(actual.Any(c=>c.Name=="FpsTests.exe"&&c.Fps>0),"Parse actual helper output");
            Console.WriteLine("Actual capture CSV parsed successfully.");
        }
        var tracker=new FpsTracker();
        tracker.Accept("Application,ProcessID,SwapChainAddress,MsBetweenPresents,PresentMode,msGPUActive",0);
        void Frames(int pid,string name,string chain,double ms,double now){for(int i=0;i<80;i++)tracker.Accept($"{name},{pid},{chain},{ms.ToString(System.Globalization.CultureInfo.InvariantCulture)},Composed: Flip,1",now);}
        Frames(123,"game.exe","A",1000d/60,1);Frames(123,"game.exe","B",1000d/30,1);
        var candidates=tracker.Candidates(1);
        Check(candidates.Length==1&&Math.Abs(candidates[0].Fps-60)<.01,"Swap chains must not sum to 90 FPS");
        Check(tracker.Select(1,candidates,new(123,true),[123],null)==null,"Wait for stable foreground");
        Frames(123,"game.exe","A",1000d/60,3);
        Check(tracker.Select(3,tracker.Candidates(3),new(123,true),[123],null)==123,"Select foreground game");
        Frames(234,"chrome.exe","A",1000d/60,4);
        Check(tracker.Select(4,tracker.Candidates(4),new(234,true),[123,234],null)==123,"Fullscreen browser must not steal selection");
        Check(tracker.Select(10,tracker.Candidates(10),new(234,false),[123,234],null)==123,"Retain paused game on Alt-Tab");
        Check(tracker.Candidates(10).Length==0,"Do not display stale FPS");
        Check(tracker.Select(11,[],new(234,false),[234],null)==null,"Clear exited game");
        tracker.ManualApplication="chrome.exe";
        Frames(234,"chrome.exe","A",1000d/60,12);
        Check(tracker.Select(12,tracker.Candidates(12),new(234,false),[234],"chrome.exe")==234,"Manual selection bypasses desktop filter");
        Check(FpsTracker.Csv("\"game, deluxe.exe\",123")[0]=="game, deluxe.exe","Quoted CSV field");
        tracker.Accept("game.exe,123,A,NaN,Composed: Flip,0",13);
        tracker.Accept("game.exe,123,A,Infinity,Composed: Flip,0",13);
        Check(tracker.Candidates(13).All(c=>double.IsFinite(c.Fps)),"Reject nonfinite timings");
        Frames(345,"other.exe","A",1000d/120,14);
        tracker.Select(14,tracker.Candidates(14),new(345,true),[234,345],null);
        Frames(345,"other.exe","A",1000d/120,16);
        Check(tracker.Select(16,tracker.Candidates(16),new(345,true),[234,345],null)==345,"Switch to a second active game");
        Check(new FpsTracker().Candidates(0).Length==0,"Empty capture has no candidates");
        var reader=new FpsStreamReader();
        reader.ReadDiagnostics(new System.IO.StringReader(new string('x',20000)+"last error"),CancellationToken.None).GetAwaiter().GetResult();
        Check(reader.Diagnostics.Length==8192&&reader.Diagnostics.EndsWith("last error"),"Bound diagnostics while retaining latest error");
        var never=new TaskCompletionSource();
        var failed=reader.ReadFrames(new System.IO.StringReader("broken row"),_=>throw new FormatException("bad frame"),CancellationToken.None);
        bool parserReported=false;
        try{FpsStreamReader.WaitForUpdate(failed,never.Task,CancellationToken.None).GetAwaiter().GetResult();}catch(FormatException){parserReported=true;}
        Check(parserReported,"Parser failure promptly reaches capture supervisor");
        bool eofReported=false;
        try{FpsStreamReader.WaitForUpdate(Task.CompletedTask,never.Task,CancellationToken.None).GetAwaiter().GetResult();}catch(System.IO.IOException){eofReported=true;}
        Check(eofReported,"Unexpected EOF reported");
        FpsStreamReader.WaitForUpdate(Task.CompletedTask,Task.CompletedTask,CancellationToken.None).GetAwaiter().GetResult();
        string temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HardwareMonitor-tests-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(temp);
        try{
            string file=System.IO.Path.Combine(temp,"dashboard.json");
            System.IO.File.WriteAllText(file,"{\"SensorOverrides\":{\"CPU temperature\":null,\"GPU temperature\":\"\"}}");
            Check(DashboardPreferences.Load(out _,temp).SensorOverrides.Count==0,"Invalid sensor overrides are discarded");
            var first=new DashboardPreferences{Fps=new(false,"game.exe",true),Visibility=new(){["CPU temperature"]=false},Arrangement=[new("P-core utilisation",1)],Thresholds=new(){["CPU temperature"]=80}};
            SettingsStore.Write(file,first);
            SettingsStore.Write(file,first with{Fps=new(true,null,false)});
            Check(DashboardPreferences.Load(out _,temp).Fps.Show==false,"Atomic settings round trip");
            System.IO.File.WriteAllText(file,"{broken");
            var recovered=DashboardPreferences.Load(out var notice,temp);
            Check(recovered.Fps==first.Fps&&notice!=null,"Recover complete previous settings from backup");
            System.IO.File.Delete(file);System.IO.File.Delete(file+".bak");
            SettingsStore.Write(System.IO.Path.Combine(temp,"fps.json"),first.Fps);
            SettingsStore.Write(System.IO.Path.Combine(temp,"layout.json"),first.Arrangement);
            var migrated=DashboardPreferences.Load(out _,temp);
            Check(migrated.Fps==first.Fps&&migrated.Arrangement.Single().Column==1,"Migrate legacy settings without losing preferences");
            Check(!System.IO.File.Exists(file),"Loading legacy settings does not overwrite files");
            System.IO.Directory.CreateDirectory(file);
            bool saveFailed=false;
            try{SettingsStore.Write(file,first);}catch(System.IO.IOException){saveFailed=true;}
            Check(saveFailed&&System.IO.Directory.GetFiles(temp,"*.tmp").Length==0,"Failed commit cleans temporary file");
        }finally{System.IO.Directory.Delete(temp,true);}
        var sensors=new List<SensorSample>{
            new("Cpu","P-Core #1",LibreHardwareMonitor.Hardware.SensorType.Clock,"/intelcpu/0/clock/1",5000),
            new("Cpu","CPU Core #1 Thread #1",LibreHardwareMonitor.Hardware.SensorType.Load,"/intelcpu/0/load/1",20),
            new("Cpu","CPU Core #1 Thread #2",LibreHardwareMonitor.Hardware.SensorType.Load,"/intelcpu/0/load/2",40),
            new("Cpu","CPU Core #2",LibreHardwareMonitor.Hardware.SensorType.Load,"/intelcpu/0/load/3",100),
            new("Memory","Memory Used",LibreHardwareMonitor.Hardware.SensorType.Data,"/ram/data/0",16)
        };
        Check(new SensorSnapshot(sensors).PcoreUtilisation().Value==30,"P-core average excludes E-core load");
        sensors[1]=sensors[1] with{Value=null};
        Check(new SensorSnapshot(sensors).PcoreUtilisation().Value==null,"Missing thread makes P-core average unavailable");
        Check(new SensorSnapshot(sensors).Value("/ram/data/0")==16,"RAM ID resolves exact reading");
        if (args.Length == 2 && args[0] == "--sensors") {
            var options = new System.Text.Json.JsonSerializerOptions();
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            var fixture = System.Text.Json.JsonSerializer.Deserialize<SensorSample[]>(System.IO.File.ReadAllText(args[1]), options)!;
            var snapshot = new SensorSnapshot(fixture);
            Check(Math.Abs(snapshot.Resolve("Cpu", LibreHardwareMonitor.Hardware.SensorType.Temperature,"CPU Package",null,false)!.Value-53.875008f)<0.001,"Ryzen temperature from scan");
            Check(snapshot.Resolve("Cpu",LibreHardwareMonitor.Hardware.SensorType.Power,"CPU Package",null,false)>2,"Ryzen package power from scan");
            Check(snapshot.Resolve("Cpu",LibreHardwareMonitor.Hardware.SensorType.Clock,"P-Core",null,true)==3743,"Ryzen max clock excludes effective clocks");
            Check(snapshot.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Clock,"GPU Core",null,false)==400,"Radeon clock from scan");
            Check(snapshot.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Load,"GPU Memory",null,true)>59,"Radeon dedicated allocation percentage");
            Check(snapshot.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Temperature,"GPU Core",null,false)==null,"Missing GPU temperature is not inferred from CPU");
            Check(snapshot.PcoreUtilisation().Value==null,"Ryzen does not invent P-core utilisation");
        }
        var radeon = new SensorSnapshot(new SensorSample[] {
            new("GpuAmd","GPU Fan",LibreHardwareMonitor.Hardware.SensorType.Fan,"/gpu-amd/0/fan/0",0),
            new("GpuAmd","GPU Fan",LibreHardwareMonitor.Hardware.SensorType.Control,"/gpu-amd/0/control/0",45),
            new("GpuAmd","GPU Memory",LibreHardwareMonitor.Hardware.SensorType.Temperature,"/gpu-amd/0/temperature/1",52),
            new("GpuAmd","GPU Memory",LibreHardwareMonitor.Hardware.SensorType.Load,"/gpu-amd/0/load/1",3)
        });
        Check(radeon.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Fan,"GPU Fan 1",null,false)==0,"AMD zero RPM is valid, not control percentage");
        Check(radeon.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Fan,"GPU Fan 2",null,false)==null,"AMD single fan does not fabricate second channel");
        Check(radeon.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Temperature,"GPU Memory Junction",null,false)==52,"AMD memory temperature mapping excludes memory load");
        Check(radeon.SelectedValue("/gpu-amd/0/fan/0",LibreHardwareMonitor.Hardware.SensorType.Fan)==0,"Selected stopped fan remains valid");
        Check(radeon.SelectedValue("/gpu-amd/0/control/0",LibreHardwareMonitor.Hardware.SensorType.Fan)==null,"Selected sensor rejects wrong units");
        Check(radeon.SelectedValue("/missing/fan/0",LibreHardwareMonitor.Hardware.SensorType.Fan)==null,"Missing saved source does not fall back");
        var prefs = new DashboardPreferences { SensorOverrides = new() { ["CPU fan"] = "/lpc/test/fan/5" } };
        var restored = System.Text.Json.JsonSerializer.Deserialize<DashboardPreferences>(System.Text.Json.JsonSerializer.Serialize(prefs))!;
        Check(restored.SensorOverrides["CPU fan"]=="/lpc/test/fan/5","Sensor overrides survive settings serialization");
        var initialVisibility = new Dictionary<string,bool> { ["CPU"] = true, ["Fan"] = false };
        Check(DashboardPreferences.MergeVisibility(new Dictionary<string,bool>(),initialVisibility,initialVisibility).Count==0,"Unchanged editor preserves discovery defaults");
        var changedVisibility = DashboardPreferences.MergeVisibility(new Dictionary<string,bool> { ["CPU"] = true },initialVisibility,new Dictionary<string,bool> { ["CPU"] = true, ["Fan"] = true });
        Check(changedVisibility["CPU"] && changedVisibility["Fan"],"Visibility edit preserves saved choices and records changed checkbox");
        var dual = new SensorSample[] {
            new("GpuAmd","GPU Core",LibreHardwareMonitor.Hardware.SensorType.Temperature,"/gpu-amd/0/temperature/0",40),
            new("GpuAmd","GPU Core",LibreHardwareMonitor.Hardware.SensorType.Temperature,"/gpu-amd/1/temperature/0",70),
            new("GpuAmd","GPU Hot Spot",LibreHardwareMonitor.Hardware.SensorType.Temperature,"/gpu-amd/0/temperature/7",45)
        };
        var dualSnapshot = new SensorSnapshot(dual,new[] { new GpuDevice("/gpu-amd/0","GpuAmd",true),new GpuDevice("/gpu-amd/1","GpuAmd",false) });
        Check(dualSnapshot.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Temperature,"GPU Core",null,false)==70,"Dedicated GPU wins over integrated of same brand");
        Check(dualSnapshot.Resolve("GpuNvidia",LibreHardwareMonitor.Hardware.SensorType.Temperature,"GPU Hot Spot",null,true)==null,"Missing selected GPU sensor never leaks from another adapter");
        Check(dualSnapshot.SelectedValue("/gpu-amd/0/temperature/0",LibreHardwareMonitor.Hardware.SensorType.Temperature)==40,"Manual GPU override remains available");
        Console.WriteLine($"Passed {assertions} FPS, settings and sensor assertions.");
    }
}






