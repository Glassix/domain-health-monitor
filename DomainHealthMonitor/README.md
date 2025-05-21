# Domain Monitor

A lightweight C# console application for monitoring domain availability and response times.

## Features

- Monitor any domain with real-time DNS resolution and HTTP request metrics
- Compare results across multiple DNS servers (Google DNS, Cloudflare DNS, and system default)
- Track DNS resolution time, HTTP response time, and total request time
- Color-coded console output for easy status identification
- Detailed logging to a text file for historical analysis

## Usage

1. Build and run the application:
   ```
   dotnet run
   ```

2. Enter the domain you want to monitor when prompted:
   ```
   Enter the domain to monitor: example.com
   ```

3. The application will start monitoring the domain, displaying:
   - DNS server used
   - Resolved IP address
   - DNS resolution time
   - HTTP response status
   - HTTP response time
   - Total operation time

4. Press `Ctrl+C` to exit the application

## Log File

The application logs all monitoring data to `ping_log.txt` in the application directory with the following format:

```
YYYY-MM-DD HH:MM:SS.fff | DNS: [server] | URL: [domain] | IP: [ip_address] | DNS Time: [time]ms | HTTP: [status] | HTTP Time: [time]ms | Total: [time]ms
```

## Configuration

You can modify the following settings in the code:
- Log file location (`string logFile = "ping_log.txt"`)
- Polling interval (`int intervalMs = 2000`)
- Custom DNS servers list
- HTTP client timeout and connection parameters