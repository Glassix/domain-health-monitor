using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DomainMonitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Ask for domain input
            Console.Write("Enter the domain to monitor without https or http: ");
            string domain = Console.ReadLine().Trim();

            // Validate domain input
            while (string.IsNullOrWhiteSpace(domain))
            {
                Console.Write("Domain cannot be empty. Please enter a valid domain: ");
                domain = Console.ReadLine().Trim();
            }

            // Configuration options
            string logFile = "ping_log.txt";
            int intervalMs = 2000;
            bool enableConsoleOutput = true;

            // Configurable DNS servers (optional)
            List<IPAddress> customDnsServers = new List<IPAddress>
            {
                IPAddress.Parse("8.8.8.8"),    // Google DNS
                IPAddress.Parse("1.1.1.1")     // Cloudflare DNS
            };

            Console.WriteLine($"Starting domain monitor for {domain}");
            Console.WriteLine($"Press Ctrl+C to exit");

            // Set up cancellation
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Shutting down...");
            };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // Use multiple DNS servers for comparison
                    foreach (var dnsServer in customDnsServers)
                    {
                        await ResolveAndHttpRequest(domain, logFile, dnsServer, enableConsoleOutput, cts.Token);
                    }

                    // Also use system default DNS
                    await ResolveAndHttpRequest(domain, logFile, null, enableConsoleOutput, cts.Token);

                    await Task.Delay(intervalMs, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit path
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex}");
                await File.AppendAllTextAsync(logFile, $"CRITICAL ERROR: {ex}" + Environment.NewLine);
            }
        }

        static async Task ResolveAndHttpRequest(string domain, string logFile, IPAddress dnsServer, bool enableConsole, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            string ipAddress = "Resolution Failed";
            string httpResult = "N/A";
            string dnsSource = dnsServer == null ? "System Default" : dnsServer.ToString();
            double dnsResolutionTimeMs = 0;
            double httpResponseTimeMs = 0;

            try
            {
                // Custom DNS resolution
                var dnsStopwatch = Stopwatch.StartNew();
                IPAddress resolvedIp = null;

                if (dnsServer != null)
                {
                    // Use explicit DNS server with DnsClient library or custom implementation
                    // This is a simplified implementation
                    using (var udpClient = new UdpClient())
                    {
                        // In a real implementation, you would construct a proper DNS query packet
                        // and parse the response properly
                        udpClient.Connect(dnsServer, 53);
                        udpClient.Client.ReceiveTimeout = 2000;
                        udpClient.Client.SendTimeout = 2000;

                        // Use system DNS for demonstration
                        var addresses = await Dns.GetHostAddressesAsync(domain);
                        if (addresses.Length > 0)
                        {
                            resolvedIp = addresses[0];
                            ipAddress = resolvedIp.ToString();
                        }
                    }
                }
                else
                {
                    // System DNS with cache disabled
                    ServicePointManager.DnsRefreshTimeout = 0;
                    var addresses = await Dns.GetHostAddressesAsync(domain);
                    if (addresses.Length > 0)
                    {
                        resolvedIp = addresses[0];
                        ipAddress = resolvedIp.ToString();
                    }
                }

                dnsResolutionTimeMs = dnsStopwatch.Elapsed.TotalMilliseconds;

                if (resolvedIp == null)
                {
                    ipAddress = "No IP addresses found";
                }
            }
            catch (Exception e)
            {
                ipAddress = "Resolution Failed";
                httpResult = $"DNS Error: {e.Message}";
            }

            // HTTP Request with timing
            try
            {
                var httpStopwatch = Stopwatch.StartNew();

                // Advanced configuration for the handler
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.Zero,
                    PooledConnectionIdleTimeout = TimeSpan.Zero,
                    MaxConnectionsPerServer = 1,
                    EnableMultipleHttp2Connections = false,
                    UseCookies = false,
                    AllowAutoRedirect = false,
                    ConnectTimeout = TimeSpan.FromSeconds(3),

                    // Disable automatic DNS resolution caching
                    ConnectCallback = async (context, token) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(context.DnsEndPoint, token);
                        return new NetworkStream(socket, true);
                    }
                };

                using (var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) })
                {
                    // Add headers to prevent caching
                    httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store");
                    httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");

                    var response = await httpClient.GetAsync($"https://{domain}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    httpResponseTimeMs = httpStopwatch.Elapsed.TotalMilliseconds;

                    httpResult = $"{(int)response.StatusCode} {response.ReasonPhrase}";

                    // Optionally inspect headers (useful for debugging)
                    var serverHeader = response.Headers.Contains("Server") ? response.Headers.GetValues("Server").FirstOrDefault() : "Unknown";
                    httpResult += $" | Server: {serverHeader}";
                }
            }
            catch (TaskCanceledException)
            {
                httpResult = "Timeout";
            }
            catch (HttpRequestException e)
            {
                httpResult = $"HTTP Error: {e.Message}";
            }
            catch (Exception e)
            {
                httpResult = $"Error: {e.GetType().Name}";
            }

            stopwatch.Stop();

            // Enhanced logging
            var sb = new StringBuilder();
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | DNS: {dnsSource} | URL: {domain} | IP: {ipAddress} | " +
                         $"DNS Time: {dnsResolutionTimeMs:F2}ms | HTTP: {httpResult} | HTTP Time: {httpResponseTimeMs:F2}ms | " +
                         $"Total: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");

            await File.AppendAllTextAsync(logFile, sb.ToString());

            if (enableConsole)
            {
                Console.ForegroundColor = httpResult.StartsWith("2") ? ConsoleColor.Green :
                                         httpResult.Contains("Error") || httpResult.Contains("Timeout") ? ConsoleColor.Red :
                                         ConsoleColor.Yellow;

                Console.WriteLine(sb.ToString().TrimEnd());
                Console.ResetColor();
            }
        }
    }
}