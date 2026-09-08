using System;
using System.IO;
using System.Reflection;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace InSongLeaderboardSocket;

internal sealed class OverlayBehavior : WebSocketBehavior
{
    protected override void OnOpen()
    {
        var json = Plugin.GetCurrentStateJson();
        if (json != null)
            Send(json);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        // Server-push only; client messages ignored.
    }
}

internal sealed class OverlayServer : IDisposable
{
    private WebSocketSharp.Server.HttpServer? _server;
    private string? _overlayHtml;

    public int Port { get; }
    public bool IsRunning => _server != null;

    public OverlayServer(int port)
    {
        Port = port;
        LoadOverlayHtml();
    }

    public void Start()
    {
        if (_server != null) return;
        if (string.IsNullOrEmpty(_overlayHtml)) return;

        _server = new WebSocketSharp.Server.HttpServer(Port);
        _server.AddWebSocketService<OverlayBehavior>("/overlay");

        _server.OnGet += (sender, e) =>
        {
            var path = e.Request.Url.AbsolutePath;

            if (path == "/" || path == "/index.html")
            {
                var bytes = Encoding.UTF8.GetBytes(_overlayHtml!);
                e.Response.StatusCode = 200;
                e.Response.ContentType = "text/html; charset=utf-8";
                e.Response.ContentLength64 = bytes.Length;
                e.Response.WriteContent(bytes);
            }
            else
            {
                e.Response.StatusCode = 404;
                e.Response.Close();
            }
        };

        _server.Start();

        Plugin.Log.Info(string.Format("HTTP+WS server started on http://localhost:{0}/", Port));
        Plugin.Log.Info(string.Format("Overlay at http://localhost:{0}/ , WebSocket at ws://localhost:{0}/overlay", Port));
    }

    public void Stop()
    {
        if (_server == null) return;
        _server.Stop();
        _server = null;
        Plugin.Log.Info("Server stopped.");
    }

    public void Broadcast(string json)
    {
        if (_server == null) return;
#pragma warning disable CS0618
        _server.WebSocketServices.Broadcast(json);
#pragma warning restore CS0618
    }

    public void Dispose()
    {
        Stop();
    }

    private void LoadOverlayHtml()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("InSongLeaderboardSocket.overlay.html");
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                    _overlayHtml = reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("Failed to load overlay HTML: " + ex.Message);
            _overlayHtml = "<html><body><h1>Failed to load overlay</h1></body></html>";
        }
    }
}
