using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OSCode.Services;

public class OpenCodeClient : IDisposable
{
    private Process? _serveProcess;
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly int _port;
    private CancellationTokenSource? _sseCts;
    private Task? _sseTask;

    public event Action<string>? ServerLogReceived;
    public event Action<string, string>? EventReceived; // eventType, dataJson

    public string ServerUrl => _serverUrl;

    public OpenCodeClient(int port = 19888)
    {
        _port = port;
        _serverUrl = $"http://127.0.0.1:{port}";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task StartServerAsync(string workspacePath)
    {
        // Terminate any previous OpenCode server instance started by this client to release the port
        try
        {
            if (_serveProcess != null && !_serveProcess.HasExited)
            {
                ServerLogReceived?.Invoke("Stopping previous OpenCode server instance to free port...");
                _serveProcess.Kill(true);
                _serveProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[Warning] Could not terminate previous server instance: {ex.Message}");
        }

        // 1. Spawn opencode serve
        _serveProcess = new Process();
        _serveProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "opencode.cmd",
            Arguments = $"serve --port {_port} --hostname 127.0.0.1",
            WorkingDirectory = workspacePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _serveProcess.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) ServerLogReceived?.Invoke(e.Data);
        };
        _serveProcess.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) ServerLogReceived?.Invoke("[SERVER ERROR] " + e.Data);
        };

        ServerLogReceived?.Invoke($"Spawning opencode serve on port {_port}...");
        try
        {
            _serveProcess.Start();
        }
        catch (Exception)
        {
            // Try powershell/cmd fallback if path is not found directly
            _serveProcess.StartInfo.FileName = "powershell.exe";
            _serveProcess.StartInfo.Arguments = $"-NoProfile -Command \"opencode serve --port {_port} --hostname 127.0.0.1\"";
            _serveProcess.Start();
        }

        _serveProcess.BeginOutputReadLine();
        _serveProcess.BeginErrorReadLine();

        // 2. Wait for health check success
        int retries = 15;
        while (retries > 0)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/global/health");
                if (response.IsSuccessStatusCode)
                {
                    ServerLogReceived?.Invoke("OpenCode server is healthy and responding!");
                    break;
                }
            }
            catch
            {
                // Wait and retry
            }
            await Task.Delay(1000);
            retries--;
        }

        if (retries == 0)
        {
            ServerLogReceived?.Invoke("Warning: OpenCode health check timed out. Server may not be ready.");
        }
    }

    public void StartEventSubscription(string? workspaceDir = null)
    {
        _sseCts = new CancellationTokenSource();
        string url = $"{_serverUrl}/event";
        if (!string.IsNullOrEmpty(workspaceDir))
        {
            url += $"?directory={Uri.EscapeDataString(workspaceDir)}";
        }

        _sseTask = Task.Run(() => SubscribeToSseAsync(url, _sseCts.Token));
    }

    private async Task SubscribeToSseAsync(string url, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = Timeout.InfiniteTimeSpan;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(stream);

                string currentEvent = "message";
                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(token);
                    if (line == null) break;

                    if (line.StartsWith("event:"))
                    {
                        currentEvent = line.Substring(6).Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        string data = line.Substring(5).Trim();
                        EventReceived?.Invoke(currentEvent, data);
                    }
                    else if (string.IsNullOrEmpty(line))
                    {
                        currentEvent = "message";
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ServerLogReceived?.Invoke($"[SSE Connection Error]: {ex.Message}. Reconnecting in 3 seconds...");
                    await Task.Delay(3000, token);
                }
            }
        }
    }

    public async Task<List<string>> DiscoverModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/config/providers");
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = new List<string>();
                if (doc.RootElement.TryGetProperty("providers", out var providers))
                {
                    foreach (var provider in providers.EnumerateArray())
                    {
                        string providerId = provider.GetProperty("id").GetString() ?? "";
                        if (provider.TryGetProperty("models", out var providerModels))
                        {
                            foreach (var m in providerModels.EnumerateArray())
                            {
                                string modelId = m.GetProperty("id").GetString() ?? "";
                                models.Add($"{providerId}/{modelId}");
                            }
                        }
                    }
                }
                if (models.Count > 0) return models;
            }
        }
        catch { }

        // Fallback: use opencode models command output
        try
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"opencode models\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            p.Start();
            string output = await p.StandardOutput.ReadToEndAsync();
            p.WaitForExit();

            var list = new List<string>();
            foreach (var line in output.Split('\n'))
            {
                string m = line.Trim();
                if (!string.IsNullOrEmpty(m) && m.Contains("/"))
                {
                    list.Add(m);
                }
            }
            return list;
        }
        catch
        {
            return new List<string> { "opencode/deepseek-v4-flash-free", "opencode/mimo-v2.5-free", "opencode/nemotron-3-ultra-free" };
        }
    }

    public async Task<string> CreateSessionAsync(string directoryPath, string modelTag, string agentMode = "coder")
    {
        try
        {
            string providerId = "opencode";
            string modelId = "deepseek-v4-flash-free";
            int slashIdx = modelTag.IndexOf('/');
            if (slashIdx != -1)
            {
                providerId = modelTag.Substring(0, slashIdx);
                modelId = modelTag.Substring(slashIdx + 1);
            }

            var requestBody = new
            {
                agent = agentMode,
                model = new
                {
                    id = modelId,
                    providerID = providerId
                }
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_serverUrl}/session?directory={Uri.EscapeDataString(directoryPath)}", content);
            if (response.IsSuccessStatusCode)
            {
                string resJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resJson);
                return doc.RootElement.GetProperty("id").GetString() ?? "";
            }
            else
            {
                string err = await response.Content.ReadAsStringAsync();
                ServerLogReceived?.Invoke($"[CreateSession Failed]: {response.StatusCode} - {err}");
            }
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[CreateSession Exception]: {ex.Message}");
        }

        return "";
    }

    public async Task<bool> SendMessageAsync(string sessionID, string messageText, string directoryPath)
    {
        try
        {
            var requestBody = new
            {
                parts = new object[]
                {
                    new
                    {
                        type = "text",
                        text = messageText
                    }
                }
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_serverUrl}/session/{sessionID}/message?directory={Uri.EscapeDataString(directoryPath)}", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[SendMessage Exception]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CancelSessionAsync(string sessionID)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_serverUrl}/session/{sessionID}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[CancelSession Exception]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ReplyToPermissionAsync(string requestID, string replyType, string directoryPath)
    {
        try
        {
            var requestBody = new
            {
                reply = replyType // "once", "always", "reject"
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_serverUrl}/permission/{requestID}/reply?directory={Uri.EscapeDataString(directoryPath)}", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[ReplyToPermission Exception]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ReplyToQuestionAsync(string requestID, List<string> answers, string directoryPath)
    {
        try
        {
            var answersList = new List<object>();
            foreach (var ans in answers)
            {
                answersList.Add(new
                {
                    options = new string[0],
                    text = ans
                });
            }

            var requestBody = new
            {
                answers = answersList.ToArray()
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_serverUrl}/question/{requestID}/reply?directory={Uri.EscapeDataString(directoryPath)}", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ServerLogReceived?.Invoke($"[ReplyToQuestion Exception]: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _sseCts?.Cancel();
        try
        {
            if (_serveProcess != null && !_serveProcess.HasExited)
            {
                _serveProcess.Kill(true);
            }
        }
        catch { }
        _httpClient.Dispose();
    }
}
