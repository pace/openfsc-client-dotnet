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

            await SendMessage(Message.ClientCapabilities.WithArguments(clientCapabilities));

            rootSession = new Session(this);
            await SendMessage(Message.Charset.WithArguments("UTF-8"), true);
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
            SendMessage(Message.NewSession.WithArguments(prefix), true).Wait();
            Session newSession = new Session(this, prefix);
            sessions.Add(prefix, newSession);
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
            SendMessage(Message.Sessions, true).Wait();

            // Todo: Match returned session list with local session list
            // Throw out of sync error if lists don't match

            Session[] result = new Session[sessions.Count];
            sessions.Values.CopyTo(result, 0);
            
            return result;
        }

        private void HandleSessionResponse()
        {
            // Todo: parse response + hand over to running "ListSessions()"
        }

        public async Task SendMessage(Message message, bool expectResponse = false, string tag = "*")
        {
            if (!CapabilitySupported(message.method)) {
                Console.WriteLine($"Method not supported by Server: {message.method}.");
                return;
            }

            if (expectResponse && tag == "*") tag = $"C{Session.nextSequence}";

            var data = $"{tag} {message}";

            Console.WriteLine($"SND: {data}");

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"{data}\r\n")), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task Quit(string message = "Bye bye") 
        {
            await Session.Quit(message);
            await receiveTask;
        }

        private void ParseMessage(string input) 
        {
            string[] tagMethodArgs = input.Split(new char[] { (char)32 }, 2);
            var tag = tagMethodArgs[0];
            var message = Message.FromInput(tagMethodArgs[1]);

            Console.WriteLine($"> {tag} {message}");
            var selectedSession = Session;

            if (tag.Contains(".")) 
            {
                string[] prefixTag = tag.Split(new char[] { (char)46 }, 2);
                string prefix = prefixTag[0];
                tag = prefixTag[1];
                selectedSession = GetSession(prefix);
            }

            var response = message.Evaluate(Session);

            if (response.messages != null)
            {
                response.messages.ForEach(f => SendMessage(f, false, tag).Wait());
            }

            if (response.actions != null)
            {
                response.actions.ForEach(f =>
                {
                    if (!Session.HandleAction(f))
                    {
                        switch (f)
                        {
                            case Action.SetServerCapabilities:
                                serverCapabilities = message.arguments;
                                break;

                            // Todo: parse response + hand over to running "ListSessions()"
                            // case Action.SessionResponse
                        }
                    }
                });
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