using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FuelingSiteConnect
{
    internal struct StatusCode
    {
        internal static string notFound = "404";
    }

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
            var message = typeof(Message).GetFields(BindingFlags.Static | BindingFlags.Public)
              .Select(f => (Message)f.GetValue(null))
              .First(f => f.method.Equals(method));

            if (message != null)
            {
                return message;
            }

            throw new NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            return obj is Message && ((Message)obj).method.Equals(method);
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
            var result = session.sessionDelegate.SessionGetProducts(session);
            if (result == null || result.Length == 0)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            } else {
                return new Response(result);
            }
        });

        public static Message SessionMode = new Message("SESSIONMODE", (session, input) =>
        {
            return new Response(input[0].Equals("active") ? Action.SetActive : Action.SetInactive);
        });

        public static Message Heartbeat = new Message("HEARTBEAT", (session, input) =>
        {
            return new Response(
                Beat.WithArguments(DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK")),
                Ok
            );
        });

        public static Message Prices = new Message("PRICES", (session, input) =>
        {
            var result = session.sessionDelegate.SessionGetPrices(session);
            if (result == null || result.Length == 0)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(result);
            }
        });

        public static Message Pumps = new Message("PUMPS", (session, input) =>
        {
            var result = session.sessionDelegate.SessionGetPumps(session);
            if (result == null || result.Length == 0)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(result);
            }
        });

        public static Message PumpStatus = new Message("PUMPSTATUS", (session, input) =>
        {
            var result = session.sessionDelegate.SessionGetPumpStatus(session, Int32.Parse(input[0]), input.Count() > 1 ? Int32.Parse(input[1]) : 0);
            if (result == null)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(result);
            }
        });

        public static Message Transactions = new Message("TRANSACTIONS", (session, input) =>
        {
            var result = session.sessionDelegate.SessionGetTransactions(session, input.Count() > 0 ? Int32.Parse(input[0]) : 0, 0);
            if (result == null || result.Length == 0)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(result);
            }
        });

        public static Message PAN = new Message("PAN", (session, input) =>
        {
            session.sessionDelegate.SessionPanMessage(session, input[0], input[1]);
            return new Response(Ok);
        });

        public static Message ClearTransaction = new Message("CLEAR", (session, input) =>
        {
            var result = session.sessionDelegate.SessionClearTransaction(session, Int32.Parse(input[0]), input.Count() > 1 ? input[1] : null, input.Count() > 2 ? input[2] : null);
            if (result == null || result.Length == 0)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(result);
            }
        });

        public static Message UnlockPump = new Message("UNLOCKPUMP", (session, input) =>
        {
            var result = session.sessionDelegate.SessionUnlockPump(session, Int32.Parse(input[0]), input.Count() > 1 ? input[1] : null, Decimal.Parse(input[2]), input.Count() > 3 ? input[3] : null, input);
            if (!result)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(Ok);
            }
        });

        public static Message LockPump = new Message("LOCKPUMP", (session, input) =>
        {
            var result = session.sessionDelegate.SessionLockPump(session, Int32.Parse(input[0]));
            if (!result)
            {
                return new Response(Error.WithArguments(StatusCode.notFound));
            }
            else
            {
                return new Response(Ok);
            }
        });
    };


    // Helper
    public class MessageBuilder
    {
        public static Message Price(string productId, string unit, string currency, decimal pricePerUnit, string description)
        {
            return Message.Price.WithArguments(productId, unit, currency, $"{pricePerUnit:0.0000}", description);
        }

        public static Message Product(string productId, string category, decimal vatRate)
        {
            return Message.Product.WithArguments(productId, category, $"{vatRate:0.00}");
        }

        public static Message Pump(int pump, string status)
        {
            return Message.Pump.WithArguments(pump.ToString(), status);
        }

        public static Message Transaction(
            int pump,
            string siteTransactionId,
            string status,
            string productId,
            string currency,
            decimal priceWithVat,
            decimal priceWithoutVat,
            decimal vatRate,
            decimal vatAmount,
            string unit,
            decimal volume,
            decimal pricePerUnit
            )
        {
            return Message.Transaction.WithArguments(
                pump.ToString(),
                siteTransactionId,
                status,
                productId,
                currency,
                $"{priceWithVat:0.00}",
                $"{priceWithoutVat:0.00}",
                $"{vatRate:0.00}",
                $"{vatAmount:0.00}",
                $"{unit}",
                $"{volume:0.0000}",
                $"{pricePerUnit:0.0000}"
                );
        }

        public static Message ReceiptInfo(string paceTransactionId, string key, string value)
        {
            return Message.ReceiptInfo.WithArguments(paceTransactionId, key, value);
        }
    }
}
