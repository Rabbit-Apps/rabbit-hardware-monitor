using LibreHardwareMonitor.Hardware;
using System.Text.Json;
using System.Security.Principal;
bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
var computer = new Computer { IsCpuEnabled=true, IsGpuEnabled=true, IsMemoryEnabled=true, IsMotherboardEnabled=true, IsControllerEnabled=true };
try {
 computer.Open();
 var rows = new List<object>();
 void Visit(IHardware h) { h.Update(); foreach(var child in h.SubHardware) Visit(child); foreach(var s in h.Sensors) rows.Add(new { Hardware=h.Name, Kind=h.HardwareType.ToString(), Id=s.Identifier.ToString(), Name=s.Name, Type=s.SensorType.ToString(), Value=s.Value }); }
 foreach(var h in computer.Hardware) Visit(h);
 Thread.Sleep(1500); rows.Clear(); foreach(var h in computer.Hardware) Visit(h);
 var json = JsonSerializer.Serialize(rows,new JsonSerializerOptions {WriteIndented=true});
 if(args.Length > 0) {
   File.WriteAllText(args[0],json);
   File.WriteAllText(args[0]+".metadata.json",JsonSerializer.Serialize(new { Elevated=elevated, Timestamp=DateTimeOffset.Now, Sensors=rows.Count, Library=typeof(Computer).Assembly.GetName().Version?.ToString(), Report=computer.GetReport() },new JsonSerializerOptions {WriteIndented=true}));
 } else Console.WriteLine(json);
} finally { computer.Close(); }
