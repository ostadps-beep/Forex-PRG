using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ForexPanel.Core;

public sealed class Mt4PipeServer : IAsyncDisposable
{
    private const string PipeName = "ForexPanel_MT4";

    private NamedPipeServerStream? pipe;
    private CancellationTokenSource? cancellation;
    private Task? serverTask;

    public bool IsConnected =>
        pipe?.IsConnected == true;

    public event Action<string>? MessageReceived;

    public Task StartAsync()
    {
        if (serverTask != null)
            return Task.CompletedTask;

        cancellation =
            new CancellationTokenSource();

        serverTask =
            RunServerAsync(
                cancellation.Token);

        return Task.CompletedTask;
    }

    private async Task RunServerAsync(
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? currentPipe = null;

            try
            {
                currentPipe =
                    new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                pipe = currentPipe;

                MessageReceived?.Invoke(
                    "WAITING|MT4");

                await currentPipe
                    .WaitForConnectionAsync(token);

                MessageReceived?.Invoke(
                    "CONNECTED|MT4");

                await ReadLoopAsync(
                    currentPipe,
                    token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                MessageReceived?.Invoke(
                    $"PIPE_ERROR|{ex.Message}");
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke(
                    $"ERROR|{ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(
                    pipe,
                    currentPipe))
                {
                    pipe = null;
                }

                try
                {
                    currentPipe?.Dispose();
                }
                catch
                {
                }

                MessageReceived?.Invoke(
                    "DISCONNECTED|MT4");
            }

            if (!token.IsCancellationRequested)
                await Task.Yield();
        }
    }

    private async Task ReadLoopAsync(
        NamedPipeServerStream stream,
        CancellationToken token)
    {
        byte[] buffer =
            new byte[4096];

        StringBuilder pending =
            new();

        while (
            stream.IsConnected &&
            !token.IsCancellationRequested)
        {
            int count =
                await stream.ReadAsync(
                    buffer,
                    token);

            if (count == 0)
                break;

            string text =
                Encoding.ASCII.GetString(
                    buffer,
                    0,
                    count);

            pending.Append(text);

            while (true)
            {
                string current =
                    pending.ToString();

                int newline =
                    current.IndexOf('\n');

                if (newline < 0)
                    break;

                string line =
                    current[..newline]
                        .TrimEnd('\r');

                pending.Remove(
                    0,
                    newline + 1);

                if (line.Length == 0)
                    continue;

                HandleMessage(line);
            }
        }
    }

    private void HandleMessage(
        string message)
    {
        MessageReceived?.Invoke(
            "MT4 -> ForexPanel: " +
            message);

        if (message.StartsWith("HELLO|"))
        {
            _ = SendAsync(
                "WELCOME|ForexPanel|PIPE_V1");

            _ = SendPingAfterDelayAsync();
        }
        else if (message == "PONG")
        {
            MessageReceived?.Invoke(
                "PING/PONG TEST: SUCCESS");
        }
    }

    private async Task SendPingAfterDelayAsync()
    {
        try
        {
            await Task.Delay(500);

            await SendAsync("PING");
        }
        catch
        {
        }
    }

    public async Task SendAsync(
        string message)
    {
        NamedPipeServerStream? current =
            pipe;

        if (current == null ||
            !current.IsConnected)
            return;

        try
        {
            byte[] data =
                Encoding.ASCII.GetBytes(
                    message + "\r\n");

            await current.WriteAsync(
                data,
                CancellationToken.None);

            await current.FlushAsync(
                CancellationToken.None);

            MessageReceived?.Invoke(
                "ForexPanel -> MT4: " +
                message);
        }
        catch (IOException ex)
        {
            MessageReceived?.Invoke(
                $"PIPE_WRITE_ERROR|{ex.Message}");
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }

        try
        {
            pipe?.Dispose();
        }
        catch
        {
        }

        if (serverTask != null)
        {
            try
            {
                await serverTask;
            }
            catch
            {
            }
        }

        cancellation?.Dispose();

        cancellation = null;
        serverTask = null;
        pipe = null;
    }
}