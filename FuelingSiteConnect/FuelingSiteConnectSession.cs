using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FuelingSiteConnect 
{
    // Delegates to be implemented by POS connector
    // TODO: Why no interface?
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

        public string prefix { get; } = null;
        private ulong clientSequence = 0; public ulong nextSequence { get { clientSequence++; return clientSequence; } }
        private string siteAccessKey;

        public bool active { get; private set; } = true;
        public bool authenticated { get; } = false;
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
            this.prefix = prefix;
        }

        public async Task Authenticate(string siteAccessKey, string secret) 
        {
            this.siteAccessKey = siteAccessKey;
            this.secret = secret;
            await SendMessage(Message.PlainAuth.WithArguments(siteAccessKey, secret));
        }

        public async Task Price(string productId, string unit, string currency, decimal pricePerUnit, string description) 
        {
            await SendMessage(Message.Price.WithArguments(productId, unit, currency, $"{pricePerUnit:0.0000}", description), false);
        }

        public async Task Product(string productId, string category, decimal vatRate) 
        {
            await SendMessage(Message.Product.WithArguments(productId, category, $"{vatRate:0.00}"));
        }

        public async Task Pump(int pump, string status) 
        {
            await SendMessage(Message.Pump.WithArguments(pump.ToString(), status), false);
        }

        public async Task Transaction(
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
            await SendMessage(Message.Transaction.WithArguments(
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
                ), false);
        }

        public async Task ReceiptInfo(string paceTransactionId, string key, string value) 
        {
            await SendMessage(Message.ReceiptInfo.WithArguments(paceTransactionId, key, value), false);
        }

        public async Task Quit(string reason = "Bye bye") 
        {
            await SendMessage(Message.Quit.WithArguments(reason), false);
        }

        private async Task SendMessage(Message message, bool expectResponse = true, string tag = "*")
        {
            if (expectResponse)
            {
                tag = $"C{nextSequence}";
            }
            if (prefix != null) {
                tag = $"{prefix}.{tag}";
            }
            await fsc.SendMessage(message, expectResponse, tag);
        }

        internal bool HandleAction(Action action)
        {
            switch (action)
            {
                case Action.SetActive:
                    active = true;
                    break;

                case Action.SetInactive:
                    active = false;
                    break;

                default:
                    return false;
            }

            return true;
        }
    }

}
