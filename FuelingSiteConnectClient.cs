using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FuelingSiteConnect 
{

    public class Client 
    {
        Uri endpoint;
        public ClientWebSocket socket;
        private Task receiveTask;
        public string[] clientCapabilities;
        public string[] serverCapabilities;
        private Session rootSession; public Session Session { get => rootSession; }
        private Dictionary<string, Session> sessions = new Dictionary<string, Session>();

        public Client(string[] implementedCapabilities)
        {
            socket = new ClientWebSocket();

            clientCapabilities = new string[implementedCapabilities.Count()+3];
            clientCapabilities[0] = "HEARTBEAT";
            clientCapabilities[1] = "SESSIONMODE";
            clientCapabilities[2] = "QUIT";
            implementedCapabilities.CopyTo(clientCapabilities, 3);
        }

        public async Task Connect(Uri endpoint)
        {
            this.endpoint = endpoint;
            await socket.ConnectAsync(endpoint, CancellationToken.None);

            receiveTask = Receive();

            await SendMessage("CAPABILITY", $"{string.Join(" ", clientCapabilities)}");

            rootSession = new Session(this);
            await SendMessage("CHARSET", "UTF-8", true);
        }

        public bool CapabilitySupported(string serverCapability) 
        {
            if (serverCapability == "CAPABILITY" ||
                serverCapability == "OK" ||
                serverCapability == "ERR") return true;

            return Array.Exists(serverCapabilities, x => x == serverCapability);
        }

        public Session NewSession(string prefix)
        {
            SendMessage("NEWSESSION", $"{prefix}", true).Wait();
            Session newSession = new Session(this, prefix);
            this.sessions.Add(prefix, newSession);
            return newSession;
        }

        /* Returns session object identified by prefix */
        public Session GetSession(string prefix)
        {
            return sessions[prefix];
        }

        // Todo: List all sessions (and sync with server before!)
        public Session[] ListSessions() 
        {
            SendMessage("SESSIONS").Wait();

            Session[] result = new Session[sessions.Count];
            sessions.Values.CopyTo(result, 0);
            return result;
        }

        public async Task SendMessage(string method, string args = null, bool expectResponse = false, string tag = "*")
        {
            if (!CapabilitySupported(method)) {
                Console.WriteLine($"Method not supported by Server: {method}.");
                return;
            }

            if (expectResponse && tag == "*") tag = $"C{Session.nextSequence}";

            var data = $"{tag} {method}";
            if (args != null) data = $"{data} {args}";

            Console.WriteLine($"SND: {data}");

            await socket.SendAsync(Encoding.UTF8.GetBytes($"{data}\r\n"), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task Quit(string message = "Bye bye") 
        {
            await Session.Quit(message);
            await receiveTask;
        }

        private void ParseServerCapabilities(string args) 
        {
            serverCapabilities = args.Split((char)32);
        }

        private void ParseMessage(string message) 
        {
            string[] tagMethodArgs = message.Split((char)32, 3);
            string tag = tagMethodArgs[0], method = tagMethodArgs[1];
            string args = null;
            if (tagMethodArgs.Count() > 2) 
            {
                args = tagMethodArgs[2];
            }
            Console.WriteLine($"RCV: {tag} {method} {args}");

            if (tag.Contains(".")) 
            {
                string[] prefixTag = tag.Split((char)46, 2);
                string prefix = prefixTag[0];
                tag = prefixTag[1];
                GetSession(prefix).HandleMessage(tag, method, args);
            }
            else 
            {
                switch (method) {
                    case "CAPABILITY":
                        ParseServerCapabilities(args);
                        break;
                    default:
                        Session.HandleMessage(tag, method, args);
                        break;
                }
            }
        }

        public Task ReceiveTask { get => receiveTask; }

        async Task Receive()
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8)) {
                        ParseMessage((await reader.ReadToEndAsync()).Replace("\r\n",""));
                    }
                }
            } while (true);

            Console.WriteLine("Connection closed.");
        }
    }
}