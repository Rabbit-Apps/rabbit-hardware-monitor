using LibreHardwareMonitor.Hardware;
using System.IO.Compression;
using System.Security.Principal;
using System.Text.Json;

bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
string folder = Path.Combine(AppContext.BaseDirectory, "Results-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Environment.ProcessId);
var errors = new List<string>();
var samples = new List<object>();
var inventory = new Dictionary<string, object>();
var options = new JsonSerializerOptions { WriteIndented = true };
var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true,
    IsMotherboardEnabled = true, IsControllerEnabled = true };
try
{
    Directory.CreateDirectory(folder);
    Console.WriteLine("Hardware Monitor diagnostics - scan only, no settings changes.");
    Console.WriteLine(elevated ? "Administrator: yes" : "Administrator: NO - rerun as administrator for full sensor access.");
    Console.WriteLine("Collecting sensors for approximately 10 seconds...");
    computer.Open();
    for (int sample = 0; sample < 10; sample++)
    {
        void Visit(IHardware hardware)
        {
            string id = hardware.Identifier.ToString();
            inventory[id] = new { hardware.Name, Kind = hardware.HardwareType.ToString(), Id = id };
            try { hardware.Update(); }
            catch (Exception e) { errors.Add(id + ": " + e); }
            samples.Add(new { Sample = sample, Time = DateTimeOffset.Now, Hardware = hardware.Name,
                Kind = hardware.HardwareType.ToString(), Id = id,
                Sensors = hardware.Sensors.Select(s => new { s.Name, Id = s.Identifier.ToString(),
                    Type = s.SensorType.ToString(), Value = s.Value.HasValue && float.IsFinite(s.Value.Value) ? s.Value : null }).ToArray() });
            foreach (var child in hardware.SubHardware) Visit(child);
        }
        foreach (var hardware in computer.Hardware) Visit(hardware);
        Thread.Sleep(1000);
    }
    try { File.WriteAllText(Path.Combine(folder, "hardware-report.txt"), computer.GetReport()); }
    catch (Exception e) { errors.Add("Hardware report: " + e); }
}
catch (Exception e) { errors.Add(e.ToString()); }
finally
{
    try { computer.Close(); } catch (Exception e) { errors.Add("Close: " + e); }
}
try
{
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "sensors.json"), JsonSerializer.Serialize(samples, options));
    File.WriteAllText(Path.Combine(folder, "summary.json"), JsonSerializer.Serialize(new {
        Elevated = elevated, OS = Environment.OSVersion.ToString(), Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        LibraryPackage = "0.9.6-hotspot.7faa1af.anon1", Commit = "7faa1af1fb3c2c307186c60162035d42bf3b5a48",
        Hardware = inventory.Values, Errors = errors, Timestamp = DateTimeOffset.Now }, options));
    ZipFile.CreateFromDirectory(folder, folder + ".zip");
    Console.WriteLine("Finished. Upload this file to the chat:");
    Console.WriteLine(folder + ".zip");
    if (errors.Count > 0) Console.WriteLine("Some errors were recorded; the results are still useful.");
}
catch (Exception e)
{
    Console.WriteLine("Could not save results. Extract to a writable folder, such as Downloads, and retry.\n" + e.Message);
    Environment.ExitCode = 1;
}
if (!args.Contains("--no-pause"))
{
    Console.WriteLine("Press Enter to close.");
    Console.ReadLine();
}

