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
        public string[] clientCapabilities;
        public string[] serverCapabilities;
        public Session Session { get; private set; }
        private Dictionary<string, Session> sessions = new Dictionary<string, Session>();

        public Client(string[] implementedCapabilities)
        {
            socket = new ClientWebSocket();

            var mergedCapabilities = implementedCapabilities.ToList();
            mergedCapabilities.AddRange(new string[] { "HEARTBEAT", "SESSIONMODE", "QUIT" });
            clientCapabilities = mergedCapabilities.ToArray();
        }

        public async Task Connect(Uri endpoint)
        {
            this.endpoint = endpoint;
            await socket.ConnectAsync(endpoint, CancellationToken.None);

            ReceiveTask = Receive();
            Session = new Session(this);

            await SendMessage(Message.Capability.WithArguments(clientCapabilities));
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

        class Response
        {
            public List<Message> messages;
            public bool finishedTransmission;

            public Response()
            {
                this.messages = new List<Message>();
                this.finishedTransmission = false;
            }
        }
        private Dictionary<string, Response> responses = new Dictionary<string, Response>();

        public async Task<List<Message>> SendMessage(Message message, bool expectResponse = false, string tag = "*")
        {
            if (!CapabilitySupported(message.method)) {
                Console.WriteLine($"Method not supported by Server: {message.method}.");
                return null;
            }

            if (expectResponse && tag == "*") tag = $"C{Session.nextSequence}";

            var data = $"{tag} {message}";

            Console.WriteLine($"< {data}");

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"{data}\r\n")), WebSocketMessageType.Text, true, CancellationToken.None);

            if (expectResponse)
            {
                responses.Add(tag, new Response());
                Console.WriteLine($"<<< Waiting for {tag} >>>");
                while (!responses[tag].finishedTransmission)
                {
                    Thread.Sleep(10);
                }

                var value = responses[tag];
                responses.Remove(tag);

                return value.messages;
            }

            return null;
        }

        public async Task Quit(string message = "Bye bye") 
        {
            await Session.Quit(message);
            await ReceiveTask;
        }

        private void ParseMessage(string input) 
        {
            Console.WriteLine($"> {input}");

            string[] tagMethodArgs = input.Split(new char[] { (char)32 }, 2);
            var tag = tagMethodArgs[0];
            var message = Message.FromInput(tagMethodArgs[1]);

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
                response.messages.ForEach(f => SendMessage(f, false, f.isBroadcast ? "*" : tag).Wait());
            }

            if (response.actions != null)
            {
                response.actions.ForEach(action =>
                {
                    switch (action)
                    {
                        case Action.SetServerCapabilities:
                            serverCapabilities = message.arguments;
                            break;

                        // Todo: parse response + hand over to running "ListSessions()"
                        // case Action.SessionResponse

                        default:
                            Session.HandleAction(action);
                            break;
                    }
                });
            }

            if (tag != "*" && responses.ContainsKey(tag))
            {
                responses[tag].messages.Add(message);

                if (message.Equals(Message.Ok) || message.Equals(Message.Error))
                {
                    responses[tag].finishedTransmission = true;
                }
            }
        }

        public Task ReceiveTask { get; private set; }

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