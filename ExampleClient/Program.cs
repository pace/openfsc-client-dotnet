using System;


class Program
{
    static void Main(string[] args)
    {
        FuelingSiteConnect.Client fsc = new FuelingSiteConnect.Client(new string[] { "PRODUCTS", "PRICES", "PUMPS", "PUMPSTATUS", "TRANSACTIONS", "CLEAR", "UNLOCKPUMP", "LOCKPUMP" });
        fsc.Connect(new Uri("wss://fsc.sandbox.k8s.pacelink.net/ws/text")).Wait();

        var session = fsc.Session;  // or multiple: .NewSession("<prefix>");

        session.productsDelegate = SendProducts;
        session.pricesDelegate = SendPrices;
        session.pumpsDelegate = SendPumps;
        session.pumpStatusDelegate = SendPumpStatus;
        session.transactionsDelegate = SendTransactions;
        session.panDelegate = PanReceived;
        session.clearTransactionDelegate = ClearTransaction;
        session.unlockPumpDelegate = UnlockPump;
        session.lockPumpDelegate = LockPump;

        session.Authenticate("329dd8cf-f841-4017-bc60-3228d8344931", "d96dc44bbd25f15d061351a29c9576e4").Wait();

        // Always send price and product changes when they occur
        // If possible, send transactions and pump status updates when they occur (not needed, though)
        // Always respond in delegates as fast as possible

        // This is usually not neccessary if your program has its own main loop.
        fsc.ReceiveTask.Wait();
    }

    /* DELEGATES IMPLEMENTATION */
    static (int code, string message) SendProducts(FuelingSiteConnect.Session fscs)
    {
        // Return all product mappings for all products available at the forecourt

        fscs.Product("0020", "diesel", (decimal)0.19).Wait();
        fscs.Product("0030", "ron95e5", (decimal)0.19).Wait();
        fscs.Product("0010", "ron98", (decimal)0.19).Wait();

        return (200, null);
    }

    static (int code, string message) SendPrices(FuelingSiteConnect.Session fscs)
    {
        // Return prices for all products available at the forecourt

        fscs.Price("0010", "LTR", "EUR", (decimal)1.759, "Super Plus").Wait();
        fscs.Price("0020", "LTR", "EUR", (decimal)1.439, "Diesel").Wait();
        fscs.Price("0030", "LTR", "EUR", (decimal)1.659, "Super 95").Wait();

        return (200, null);
    }

    static (int code, string message) SendPumps(FuelingSiteConnect.Session fscs)
    {
        // Return status for all pumps at the site

        fscs.Pump(1, "free").Wait();
        fscs.Pump(2, "free").Wait();
        fscs.Pump(3, "ready-to-pay").Wait();
        fscs.Pump(4, "free").Wait();
        fscs.Pump(5, "free").Wait();

        return (200, null);
    }

    static (int code, string message) SendPumpStatus(FuelingSiteConnect.Session fscs, int pump, int updateTTL = 0)
    {
        // Return status for given pumps
        if (pump == 3)
            fscs.Pump(pump, "ready-to-pay").Wait();
        else
            fscs.Pump(pump, "free").Wait();

        return (200, null);

        // If updateTTL > 0: Send pump status changes pro-actively for given amount of seconds.
    }

    static (int code, string message) SendTransactions(FuelingSiteConnect.Session fscs, int pump = 0, int updateTTL = 0)
    {
        if (pump == 3 || pump == 0)
        {
            fscs.Transaction(3, "asdf", "open", "0010", "EUR", (decimal)59.50, (decimal)50.0, (decimal)0.19, (decimal)9.50, "LTR", (decimal)47.11, (decimal)1.119).Wait();
            return (200, null);
        }
        else
            return (404, "No transactions found");

        // If updateTTL > 0: Send transactions matching mathing the pump selection pro-actively for given amount of seconds.
    }

    static (int code, string message) PanReceived(FuelingSiteConnect.Session fscs, string paceTransactionId, string pan)
    {
        // Do whatever you want with the PAN ;-)

        return (200, null);
    }

    static (int code, string message) ClearTransaction(FuelingSiteConnect.Session fscs, int pump, string siteTransactionId, string paceTransactionId)
    {
        // 1. Clear the transaction
        // 2. Maybe send additional data using fscs.ReceiptInfo()
        // return (200, null);
        if (pump == 3)
            return (200, null);
        else
            return (404, "PumpID unknown");
        //return (404, "SiteTransactionID unknown");
        //return (410, "SiteTransactionID not open any longer");
    }

    static (int code, string message) UnlockPump(FuelingSiteConnect.Session fscs, int pump, string currency, decimal credit, string paceTransactionId, string[] productIds)
    {
        // 1. Unload the pump for given credit and productIds if given
        // return (200, null);
        return (404, "PumpID unknown");
    }

    static (int code, string message) LockPump(FuelingSiteConnect.Session fscs, int pump = 0)
    {
        // 1. Lock the pump again if fueling hasn't been started in the meantime.
        // return (200, null);
        return (404, "PumpID unknown");
    }

}
