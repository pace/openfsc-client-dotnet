using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FuelingSiteConnect
{
    class Response
    {
        internal List<Message> messages;
        internal List<Action> actions;

        internal Response(List<Message> messages, List<Action> actions)
        {
            this.messages = messages;
            this.actions = actions;
        }

        internal Response(params Message[] messages)
        {
            this.messages = messages.ToList();
            this.actions = null;
        }

        internal Response(params Action[] actions)
        {
            this.actions = actions.ToList();
            this.messages = null;
        }

        internal Response()
        {
            this.messages = null;
            this.actions = null;
        }
    }

    enum Action
    {
        SetActive,
        SetInactive,
        SetServerCapabilities
    }

    public class Message
    {
        public string method { get; private set; }
        public string[] arguments { get; private set; }

        private Func<Session, string[], Response> receiveHandler;
        private const string separator = " ";

        private Message(string method, Func<Session, string[], Response> receiveHandler)
        {
            this.method = method;
            this.receiveHandler = receiveHandler;
        }

        private Message(string method)
        {
            this.method = method;
            this.receiveHandler = null;
        }

        public Message WithArguments(params string[] arguments)
        {
            this.arguments = arguments;
            return this;
        }

        override public string ToString()
        {
            var elements = new List<string> { method };
            elements.AddRange(arguments);
            return string.Join(separator, elements);
        }

        internal static Message FromInput(string input)
        {
            var splitted = input.Split(separator.ToCharArray());
            var method = splitted[0];
            var arguments = new string[0];

            if (splitted.Length > 0)
            {
                arguments = new string[splitted.Length - 1];
                Array.Copy(splitted, 1, arguments, 0, splitted.Length - 1);
            }

            return fromMethod(method).WithArguments(arguments);
        }

        internal Response Evaluate(Session session) {
            if (receiveHandler != null)
            {
                return receiveHandler(session, arguments);
            }
            else
            {
                return new Response();
            }
        }

        internal static Message fromMethod(string method)
        {
            Console.WriteLine($"Searching for method {method}");
            var message = typeof(Message).GetFields(BindingFlags.Static | BindingFlags.Public)
              .Select(f => (Message)f.GetValue(null))
              .First(f => f.method.Equals(method));

            if (message != null)
            {
                return message;
            }

            throw new NotImplementedException();
        }

        // Server messages
        public static Message PlainAuth = new Message("PLAINAUTH");
        public static Message Price = new Message("PRICE");
        public static Message Product = new Message("PRODUCT");
        public static Message Pump = new Message("PUMP");
        public static Message Transaction = new Message("TRANSACTION");
        public static Message ReceiptInfo = new Message("RECEIPTINFO");
        public static Message Beat = new Message("BEAT");
        public static Message Quit = new Message("QUIT");
        public static Message Error = new Message("ERR");
        public static Message Ok = new Message("OK");
        public static Message Charset = new Message("CHARSET");
        public static Message NewSession = new Message("NEWSESSION");
        public static Message Sessions = new Message("SESSIONS");


        // Client messages
        public static Message Capability = new Message("CAPABILITY", (session, input) =>
        {
            return new Response(Action.SetServerCapabilities);
        });

        public static Message Products = new Message("PRODUCTS", (session, input) =>
        {
            if (session.productsDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No PRODUCTS delegate registered."));
            else
            {
                var (code, message) = session.productsDelegate(session);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message SessionMode = new Message("SESSIONMODE", (session, input) =>
        {
            return new Response(input[0].Equals("active") ? Action.SetActive : Action.SetInactive);
        });

        public static Message Heartbeat = new Message("HEARTBEAT", (session, input) =>
        {
            return new Response(
                //  Option: .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK")
                Message.Beat.WithArguments(DateTime.Now.ToString("0")),
                Message.Ok
            );
        });

        public static Message Prices = new Message("PRICES", (session, input) =>
        {
            if (session.pricesDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No PRICES delegate registered."));
            else
            {
                var (code, message) = session.pricesDelegate(session);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message Pumps = new Message("PUMPS", (session, input) =>
        {
            if (session.pumpsDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No PUMPS delegate registered."));
            else
            {
                var (code, message) = session.pumpsDelegate(session);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message PumpStatus = new Message("PUMPSTATUS", (session, input) =>
        {
            if (session.pumpStatusDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No PUMPSTATUS delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.pumpStatusDelegate(session, Int32.Parse(input[0]), input.Count() > 1 ? Int32.Parse(input[1]) : 0);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message Transactions = new Message("TRANSACTIONS", (session, input) =>
        {
            if (session.transactionsDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No TRANSACTIONS delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.transactionsDelegate(session, input.Count() > 0 ? Int32.Parse(input[0]) : 0);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message PAN = new Message("PAN", (session, input) =>
        {
            if (session.panDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No PAN delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.panDelegate(session, input[0], input[1]);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message ClearTransaction = new Message("CLEAR", (session, input) =>
        {
            if (session.clearTransactionDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No CLEAR delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.clearTransactionDelegate(session, Int32.Parse(input[0]), input.Count() > 1 ? input[1] : null, input.Count() > 2 ? input[2] : null);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message UnlockPump = new Message("UNLOCKPUMP", (session, input) =>
        {
            if (session.unlockPumpDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No UNLOCKPUMP delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.unlockPumpDelegate(session, Int32.Parse(input[0]), input.Count() > 1 ? input[1] : null, Decimal.Parse(input[2]), input.Count() > 3 ? input[3] : null, input);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });

        public static Message LockPump = new Message("LOCKPUMP", (session, input) =>
        {
            if (session.unlockPumpDelegate == null)
                return new Response(Error.WithArguments(500.ToString(), "Can't handle request: No UNLOCKPUMP delegate registered."));
            else
            {
                // TODO: Extension GetOrDefault(1, 0)
                // TODO: OkOrError(200, code, message);
                var (code, message) = session.unlockPumpDelegate(session, Int32.Parse(input[0]), input.Count() > 1 ? input[1] : null, Decimal.Parse(input[2]), input.Count() > 3 ? input[3] : null, input);
                if (code == 200)
                    return new Response(Ok);
                else
                    return new Response(Error.WithArguments(code.ToString(), message));
            }
        });
    };
}
