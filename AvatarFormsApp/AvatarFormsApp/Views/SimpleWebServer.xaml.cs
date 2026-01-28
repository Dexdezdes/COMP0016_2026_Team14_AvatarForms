using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

public class SimpleWebServer
{
    private HttpListener _listener;
    private string _baseDir;
    private string _url;

    public SimpleWebServer(string baseDir, string prefix = "http://localhost:5500/")
    {
        _baseDir = baseDir;
        _url = prefix;
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            // Using Task.Run to keep the UI thread free
            Task.Run(() => Listen());
            Debug.WriteLine($"[SERVER] Started at {_url} serving {_baseDir}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVER ERROR]: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch { /* Silent close */ }
    }

    private async Task Listen()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                // Process each request on its own thread so the listener doesn't block
                _ = Task.Run(() => ProcessRequest(context));
            }
            catch (HttpListenerException) { break; } // Normal when server stops
            catch (Exception ex)
            {
                Debug.WriteLine($"[LISTEN ERROR]: {ex.Message}");
            }
        }
    }

    
    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            string filename = context.Request.Url.AbsolutePath.Substring(1);
            if (string.IsNullOrEmpty(filename)) filename = "minimal.html";

            string filePath = Path.Combine(_baseDir, filename);

            if (File.Exists(filePath))
            {
                byte[] buffer = File.ReadAllBytes(filePath);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = GetMimeType(filePath);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                Debug.WriteLine($"[404] Not Found: {filePath}");
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVE ERROR]: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            // CRITICAL: Always close the stream so the browser doesn't "hang"
            context.Response.OutputStream.Close();
        }
    }

    private string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".html" => "text/html",
            ".js"   => "application/javascript",
            ".mjs"  => "application/javascript",
            ".css"  => "text/css",
            ".glb"  => "model/gltf-binary",
            ".bin"  => "application/octet-stream",
            ".json" => "application/json",
            _       => "application/octet-stream"
        };
    }
}