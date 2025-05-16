using System;
using System.IO;
using System.Net;
using System.Reflection;

using Microsoft.Azure.Functions.Worker.Http;

namespace KeyVault.Acmebot.Internal;

public static class StaticFileHelper
{
    private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

    public static HttpResponseData ServeStaticFile(HttpRequestData req, string path)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);

        // Get the file path from the URL
        if (string.IsNullOrEmpty(path))
        {
            path = "index.html";
        }

        // Check if the file exists in wwwroot
        var resourceName = $"KeyVault.Acmebot.wwwroot.{path.Replace('/', '.')}";
        
        if (!File.Exists($"wwwroot/{path}") && !ResourceExists(resourceName))
        {
            // If not found, default to index.html for SPA routing
            path = "index.html";
            resourceName = "KeyVault.Acmebot.wwwroot.index.html";
        }

        // Set content type based on file extension
        string contentType = GetContentType(path);
        response.Headers.Add("Content-Type", contentType);

        // First try to read from file system for development
        if (File.Exists($"wwwroot/{path}"))
        {
            var bytes = File.ReadAllBytes($"wwwroot/{path}");
            response.Body.Write(bytes, 0, bytes.Length);
        }
        // Then try embedded resource for production
        else if (ResourceExists(resourceName))
        {
            using var stream = CurrentAssembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                stream.CopyTo(response.Body);
            }
        }
        else
        {
            response = req.CreateResponse(HttpStatusCode.NotFound);
            response.WriteString("File not found");
        }

        return response;
    }

    private static string GetContentType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".html" => "text/html",
            ".js" => "text/javascript",
            ".css" => "text/css",
            ".ico" => "image/x-icon",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };
    }

    private static bool ResourceExists(string resourceName)
    {
        return Array.IndexOf(CurrentAssembly.GetManifestResourceNames(), resourceName) > -1;
    }
}