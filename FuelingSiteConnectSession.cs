using System;
using System.Linq;
using System.Threading.Tasks;

namespace FuelingSiteConnect 
{
    // Delegates to be implemented by POS connector
    public delegate (int code, string message) ProductsRequestDelegate(Session fscs);
    public delegate (int code, string message) PricesRequestDelegate(Session fscs);
    public delegate (int code, string message) PumpsRequestDelegate(Session fscs);
    public delegate (int code, string message) PumpStatusRequestDelegate(Session fscs, int pump, int updateTTL = 0);
    public delegate (int code, string message) TransactionsRequestDelegate(Session fscs, int pump = 0, int updateTTL = 0);
    public delegate (int code, string message) PanMessageDelegate(Session fscs, string paceTransactionId, string pan);
    public delegate (int code, string message) ClearTransactionRequestDelegate(Session fscs, int pump, string siteTransactionId, string paceTransactionId);
    public delegate (int code, string message) UnlockPumpRequestDelegate(Session fscs, int pump, string currency, decimal credit, string paceTransactionId, string[] productIds);
    public delegate (int code, string message) LockPumpRequestDelegate(Session fscs, int pump);

    public class Session 
    {
        private Client fsc;
        private string _prefix = null; public string prefix { get => _prefix; }
        private ulong clientSequence = 0; public ulong nextSequence { get { clientSequence++; return clientSequence; } }
        private string siteAccessKey;
        private bool _active = true; public bool active { get => _active; }
        private bool _authenticated = false; public bool authenticated { get => _authenticated; }
        private string secret;

        public ProductsRequestDelegate productsDelegate;
        public PricesRequestDelegate pricesDelegate;
        public PumpsRequestDelegate pumpsDelegate;
        public PumpStatusRequestDelegate pumpStatusDelegate;
        public TransactionsRequestDelegate transactionsDelegate;
        public PanMessageDelegate panDelegate;
        public ClearTransactionRequestDelegate clearTransactionDelegate;
        public UnlockPumpRequestDelegate unlockPumpDelegate;
        public LockPumpRequestDelegate lockPumpDelegate;

        public Session(Client client, string prefix = null)
        {
            this.fsc = client;
            this._prefix = prefix;
        }

        public async Task Authenticate(string siteAccessKey, string secret) 
        {
            this.siteAccessKey = siteAccessKey;
            this.secret = secret;
            await SendMessage("PLAINAUTH", $"{siteAccessKey} {secret}");
        }

        public async Task Price(string productId, string unit, string currency, decimal pricePerUnit, string description) 
        {
            await SendMessage("PRICE", $"{productId} {unit} {currency} {pricePerUnit:0.0000} {description}", false);
        }

        public async Task Product(string productId, string category, decimal vatRate) 
        {
            await SendMessage("PRODUCT", $"{productId} {category} {vatRate:0.00}", false);
        }

        public async Task Pump(int pump, string status) 
        {
            await SendMessage("PUMP", $"{pump} {status}", false);
        }

        public async Task Transaction(int pump, string siteTransactionId, string status, string productId, string currency, decimal priceWithVat, decimal priceWithoutVat, decimal vatRate, decimal vatAmount, string unit, decimal volume, decimal pricePerUnit) 
        {
            await SendMessage("TRANSACTION", $"{pump} {siteTransactionId} {status} {productId} {currency} {priceWithVat:0.00} {priceWithoutVat:0.00} {vatRate:0.00} {vatAmount:0.00} {unit} {volume:0.0000} {pricePerUnit:0.0000}", false);
        }

        public async Task ReceiptInfo(string paceTransactionId, string key, string value) 
        {
            await SendMessage("RECEIPTINFO", $"{paceTransactionId} {key} {value}", false);
        }

        private async Task HeartbeatResponse(string tag) 
        {
            await SendMessage("BEAT", $"{DateTime.Now.ToString("O")}", false, tag);  // Option: .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK")
            await SendOk(tag);
        }

        public async Task Quit(string message = "Bye bye") 
        {
            await SendMessage("QUIT", $"{message}", false);
        }

        private async Task SendOk(string tag = "*") 
        {
            await SendMessage("OK", null, false, tag);
        }

        private async Task SendError(int code, string message, string tag = "*") 
        {
            await SendMessage("ERR", $"{code} {message}", false, tag);
        }

        private async Task SendMessage(string method, string args = null, bool expectResponse = true, string tag = "*")
        {
            if (expectResponse)
            {
                tag = $"C{nextSequence}";
            }
            if (prefix != null) {
                tag = $"{prefix}.{tag}";
            }
            await fsc.SendMessage(method, args, expectResponse, tag);
        }

        private void HandleResponse(string tag, string response, string message) 
        {

        }

        private void HandleSessionmode(string mode) 
        {
            _active = mode == "active";
        }

        public bool HandleMessage(string tag, string method, string args) 
        {
            string[] argv = args == null ? new string[0] : args.Split((char)32);
            int code;
            string message;

            switch (method) {
                case "HEARTBEAT":
                    HeartbeatResponse(tag).Wait();
                    break;
                case "OK":
                    HandleResponse(tag, method, args);
                    break;
                case "ERR":
                    HandleResponse(tag, method, args);
                    break;
                case "SESSIONMODE":
                    HandleSessionmode(args);
                    break;
                case "PRODUCTS":
                    if (productsDelegate == null) 
                        SendError(500, "Can't handle request: No PRODUCTS delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = productsDelegate(this);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "PRICES":
                    if (pricesDelegate == null) 
                        SendError(500, "Can't handle request: No PRICES delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = pricesDelegate(this);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "PUMPS":
                    if (pumpsDelegate == null) 
                        SendError(500, "Can't handle request: No PUMPS delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = pumpsDelegate(this);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "PUMPSTATUS":
                    if (pumpStatusDelegate == null) 
                        SendError(500, "Can't handle request: No PUMPSTATUS delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = pumpStatusDelegate(this, Int32.Parse(argv[0]), argv.Count() > 1 ? Int32.Parse(argv[1]) : 0);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "TRANSACTIONS":
                    if (transactionsDelegate == null) 
                        SendError(500, "Can't handle request: No TRANSACTIONS delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = transactionsDelegate(this, argv.Count() > 0 ? Int32.Parse(argv[0]) : 0);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "PAN":
                    if (panDelegate == null) 
                        SendError(500, "Can't handle message: No PAN delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = panDelegate(this, argv[0], argv[1]);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "CLEAR":
                    if (clearTransactionDelegate == null) 
                        SendError(500, "Can't handle request: No CLEAR delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = clearTransactionDelegate(this, Int32.Parse(argv[0]), argv.Count() > 1 ? argv[1] : null, argv.Count() > 2 ? argv[2] : null);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "UNLOCKPUMP":
                    if (unlockPumpDelegate == null) 
                        SendError(500, "Can't handle request: No UNLOCKPUMP delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = unlockPumpDelegate(this, Int32.Parse(argv[0]), argv.Count() > 1 ? argv[1] : null, Decimal.Parse(argv[2]), argv.Count() > 3 ? argv[3] : null, argv);
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                case "LOCKPUMP":
                    if (lockPumpDelegate == null) 
                        SendError(500, "Can't handle request: No LOCKPUMP delegate registered.", tag).Wait();
                    else 
                    {
                        (code, message) = lockPumpDelegate(this, Int32.Parse(argv[0]));
                        if (code == 200)
                            SendOk(tag).Wait();
                        else
                            SendError(code, message, tag).Wait();
                    }
                    break;
                default:
                    // Todo: Throw error (or drop silently ??)
                    Console.WriteLine($"Don't know how to handle method: {method}.");
                    break;
            }

            return true;
        }
    }

}
