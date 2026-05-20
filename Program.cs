using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;

namespace TheBinger
{
    class Program
    {
        static void Main()
        {
            EnableSystemProxy();

            TcpListener listener = new TcpListener(IPAddress.Loopback, 8888);
            listener.Start();

            Console.WriteLine("TheBinger (C# 4.0): Google → Bing redirector active.");
            Console.WriteLine("Listening on 127.0.0.1:8888");
            Console.WriteLine("Press Ctrl+C to exit.");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                HandleClient(client);
            }
        }

        static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[8192];
                int read = stream.Read(buffer, 0, buffer.Length);

                if (read <= 0)
                {
                    stream.Close();
                    client.Close();
                    return;
                }

                string request = Encoding.ASCII.GetString(buffer, 0, read);

                // HTTPS CONNECT
                if (request.StartsWith("CONNECT"))
                {
                    if (IsGoogleDomain(request))
                    {
                        string redirect =
                            "HTTP/1.1 302 Found\r\n" +
                            "Location: https://www.bing.com\r\n" +
                            "Connection: close\r\n\r\n";

                        byte[] resp = Encoding.ASCII.GetBytes(redirect);
                        stream.Write(resp, 0, resp.Length);
                        stream.Close();
                        client.Close();
                        return;
                    }

                    // Allow all other HTTPS traffic
                    string ok = "HTTP/1.1 200 Connection Established\r\n\r\n";
                    byte[] okBytes = Encoding.ASCII.GetBytes(ok);
                    stream.Write(okBytes, 0, okBytes.Length);
                    stream.Close();
                    client.Close();
                    return;
                }

                // HTTP GET (rare)
                if (request.StartsWith("GET") && IsGoogleDomain(request))
                {
                    string query = ExtractQuery(request);
                    string redirectUrl = "https://www.bing.com/search?q=" + Uri.EscapeDataString(query);

                    string redirect =
                        "HTTP/1.1 302 Found\r\n" +
                        "Location: " + redirectUrl + "\r\n" +
                        "Connection: close\r\n\r\n";

                    byte[] resp = Encoding.ASCII.GetBytes(redirect);
                    stream.Write(resp, 0, resp.Length);
                    stream.Close();
                    client.Close();
                    return;
                }

                stream.Close();
                client.Close();
            }
            catch
            {
                try { client.Close(); } catch { }
            }
        }

        static string ExtractQuery(string request)
        {
            try
            {
                int start = request.IndexOf("q=") + 2;
                if (start < 2) return "";

                int end = request.IndexOfAny(new char[] { '&', ' ', '\r', '\n' }, start);
                if (end < 0) end = request.Length;

                return request.Substring(start, end - start);
            }
            catch
            {
                return "";
            }
        }

        static bool IsGoogleDomain(string request)
        {
            return request.Contains("google.") ||
                   request.Contains("www.google.");
        }

        static void EnableSystemProxy()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);

            key.SetValue("ProxyEnable", 1);
            key.SetValue("ProxyServer", "127.0.0.1:8888");

            Console.WriteLine("System proxy enabled → 127.0.0.1:8888");
        }
    }
}
