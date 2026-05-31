using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using POE2Radar.Core;
using POE2Radar.Overlay;

// ── Self-relaunch with random process name ──
if (!args.Contains("--launched"))
{
    var currentExe = Environment.ProcessPath;
    if (currentExe != null)
    {
        var currentDir = Path.GetDirectoryName(currentExe)!;
        var randomName = GenName() + ".exe";
        var targetExe = Path.Combine(currentDir, randomName);

        try
        {
            if (!File.Exists(targetExe))
                CreateHardLink(targetExe, currentExe, IntPtr.Zero);

            var psi = new ProcessStartInfo(targetExe, "--launched")
            {
                UseShellExecute = false,
                WorkingDirectory = currentDir,
            };
            Process.Start(psi);
            return 0;
        }
        catch
        {
            // hardlink failed, just run directly
        }
    }
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

var myName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "Radar");
Console.Title = myName;
Console.WriteLine(myName);
Console.WriteLine(new string('=', myName.Length));

ProcessHandle? process;
try
{
    process = ProcessHandle.AttachToPoE();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to attach: {ex.Message}");
    Console.Error.WriteLine("Make sure PoE2 is running and you launched as Administrator.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 1;
}
if (process is null)
{
    Console.Error.WriteLine("PoE2 not running (no matching process found).");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 1;
}
using var processHandle = process;
Console.WriteLine($"Attached to {process.ProcessName} (PID {process.ProcessId})");

var reader = new MemoryReader(process);
var slot = Bootstrap.ResolveGameStateSlot(process, reader);
if (slot == 0) return 2;

Console.WriteLine();
Console.WriteLine("Running. F9 = settings. Ctrl+C to exit.");

using var app = new RadarApp(process, reader, slot);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; app.RequestShutdown(); };
app.Run();

// Clean up hardlinks (anything that isn't the original exe)
try
{
    var self = Environment.ProcessPath;
    var dir = Path.GetDirectoryName(self);
    if (self != null && dir != null)
    {
        foreach (var f in Directory.GetFiles(dir, "*.exe"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            if (name != "Overlay" && f != self)
                try { File.Delete(f); } catch { }
        }
    }
}
catch { }

return 0;

static string GenName()
{
    var rng = Random.Shared;
    var v = "eioau";
    var c = "bcdfghjklmnpqrstvwxyz";
    var parts = new List<string>();
    for (var w = 0; w < rng.Next(1, 3); w++)
    {
        var len = rng.Next(5, 9);
        var ch = new char[len];
        for (var i = 0; i < len; i++)
            ch[i] = (i % 2 == 0) ? c[rng.Next(c.Length)] : v[rng.Next(v.Length)];
        ch[0] = char.ToUpper(ch[0]);
        parts.Add(new string(ch));
    }
    return string.Join("", parts);
}

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
