using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

// UDP relay for network tests. A game client connects to 127.0.0.1:<listen> instead of the host's 24642.
// Each direction can be given a fixed delay, random jitter, packet loss and duplication.
// A file named by --blackhole, while it exists, drops every packet both ways, which models a dead link without killing a process.
//
//   NetShaper --listen 24700 --target 24642 --delay 50 --jitter 10 --loss 2 --dup 0 --blackhole path\to\file --stats path\to\stats.json
//
// delay is one way in milliseconds, so a round trip is twice that. loss and dup are percent per packet per direction.

var opt = new Dictionary<string, string>();
for (int i = 0; i + 1 < args.Length; i += 2)
    opt[args[i].TrimStart('-')] = args[i + 1];
int listenPort = int.Parse(opt.GetValueOrDefault("listen", "24700"));
int targetPort = int.Parse(opt.GetValueOrDefault("target", "24642"));
double delayMs = double.Parse(opt.GetValueOrDefault("delay", "0"));
double jitterMs = double.Parse(opt.GetValueOrDefault("jitter", "0"));
double lossPct = double.Parse(opt.GetValueOrDefault("loss", "0"));
double dupPct = double.Parse(opt.GetValueOrDefault("dup", "0"));
string? blackhole = opt.GetValueOrDefault("blackhole");
string? statsFile = opt.GetValueOrDefault("stats");
int seed = int.Parse(opt.GetValueOrDefault("seed", "12345"));
// --control <file>: the file is read five times a second. Its text is "delay jitter loss dup", for example "40 5 10 0",
// so a test can change the conditions while it runs. Without the file the command line values stay.
string? controlFile = opt.GetValueOrDefault("control");

var rng = new Random(seed);
var rngLock = new object();
double Next() { lock (rngLock) return rng.NextDouble(); }

var listen = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
listen.Client.ReceiveBufferSize = 4 * 1024 * 1024;
listen.Client.SendBufferSize = 4 * 1024 * 1024;
var serverEp = new IPEndPoint(IPAddress.Loopback, targetPort);

// one upstream socket per client endpoint, so the host sees each client as a separate peer
var upstream = new ConcurrentDictionary<IPEndPoint, UdpClient>();

// due time (Stopwatch ticks) ordered queue of packets to send
var queue = new PriorityQueue<(byte[] Data, Func<byte[], Task> Send), long>();
var queueLock = new object();
long toTicks(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0);

long sentUp = 0, sentDown = 0, droppedUp = 0, droppedDown = 0, bytesUp = 0, bytesDown = 0, blackholed = 0;
bool Blackholed() => blackhole != null && File.Exists(blackhole);

void Enqueue(byte[] data, bool up, Func<byte[], Task> send)
{
    if (Blackholed())
    {
        Interlocked.Increment(ref blackholed);
        return;
    }
    if (Next() * 100 < lossPct)
    {
        if (up) Interlocked.Increment(ref droppedUp); else Interlocked.Increment(ref droppedDown);
        return;
    }
    int copies = Next() * 100 < dupPct ? 2 : 1;
    for (int c = 0; c < copies; c++)
    {
        double d = delayMs + (jitterMs > 0 ? (Next() * 2 - 1) * jitterMs : 0);
        if (d < 0) d = 0;
        long due = Stopwatch.GetTimestamp() + toTicks(d);
        lock (queueLock)
            queue.Enqueue((data, send), due);
        if (up) { Interlocked.Increment(ref sentUp); Interlocked.Add(ref bytesUp, data.Length); }
        else { Interlocked.Increment(ref sentDown); Interlocked.Add(ref bytesDown, data.Length); }
    }
}

// sender thread: releases packets when they are due
var sender = new Thread(() =>
{
    while (true)
    {
        (byte[] Data, Func<byte[], Task> Send) item = default;
        bool have = false;
        long wait = 1000;
        lock (queueLock)
        {
            if (queue.TryPeek(out var top, out long due))
            {
                long now = Stopwatch.GetTimestamp();
                if (due <= now) { item = queue.Dequeue(); have = true; }
                else wait = due - now;
            }
        }
        if (have) { try { item.Send(item.Data).Wait(); } catch { } continue; }
        if (wait > Stopwatch.Frequency / 1000) Thread.Sleep(1); else Thread.Yield();
    }
}) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
sender.Start();

void StartUpstreamReader(IPEndPoint client, UdpClient up)
{
    var t = new Thread(() =>
    {
        var any = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] data;
            try { data = up.Receive(ref any); } catch { return; }
            Enqueue(data, up: false, d => listen.SendAsync(d, d.Length, client));
        }
    }) { IsBackground = true };
    t.Start();
}

if (controlFile != null)
{
    new Thread(() =>
    {
        string last = "";
        while (true)
        {
            Thread.Sleep(200);
            try
            {
                if (!File.Exists(controlFile)) continue;
                string text = File.ReadAllText(controlFile).Trim();
                if (text == last) continue;
                var p = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4) continue;
                delayMs = double.Parse(p[0]); jitterMs = double.Parse(p[1]); lossPct = double.Parse(p[2]); dupPct = double.Parse(p[3]);
                last = text;
                Console.WriteLine($"conditions now: delay {delayMs} jitter {jitterMs} loss {lossPct}% dup {dupPct}%");
            }
            catch { }
        }
    }) { IsBackground = true }.Start();
}

var statsThread = new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(1000);
        if (statsFile != null)
        {
            try
            {
                File.WriteAllText(statsFile, System.Text.Json.JsonSerializer.Serialize(new { delayMs, jitterMs, lossPct, dupPct, clients = upstream.Count, sentUp, sentDown, droppedUp, droppedDown, bytesUp, bytesDown, blackholed }));
            }
            catch { }
        }
    }
}) { IsBackground = true };
statsThread.Start();

Console.WriteLine($"NetShaper listening on 127.0.0.1:{listenPort} -> {targetPort}, delay {delayMs} ms one way, jitter {jitterMs} ms, loss {lossPct}%, dup {dupPct}%");
var from = new IPEndPoint(IPAddress.Any, 0);
while (true)
{
    byte[] data;
    try { data = listen.Receive(ref from); }
    catch (SocketException) { continue; }
    var client = new IPEndPoint(from.Address, from.Port);
    var up = upstream.GetOrAdd(client, ep =>
    {
        var u = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        u.Client.ReceiveBufferSize = 4 * 1024 * 1024;
        StartUpstreamReader(ep, u);
        return u;
    });
    Enqueue(data, up: true, d => up.SendAsync(d, d.Length, serverEp));
}
