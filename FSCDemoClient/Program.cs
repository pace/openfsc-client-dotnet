﻿using System;
using FuelingSiteConnect;

class Program : ISessionDelegate
{
    static void Main(string[] args)
    {
        Client fsc = new Client(new string[] { "PRODUCTS", "PRICES", "PUMPS", "PUMPSTATUS", "TRANSACTIONS", "CLEAR", "UNLOCKPUMP", "LOCKPUMP" });
        fsc.Connect(new Uri("wss://fsc.sandbox.k8s.pacelink.net/ws/text")).Wait();

        var session = fsc.Session;  // or multiple: .NewSession("<prefix>");
        session.sessionDelegate = new Program();
        session.Authenticate("329dd8cf-f841-4017-bc60-3228d8344931", "d96dc44bbd25f15d061351a29c9576e4").Wait();

        // Always send price and product changes when they occur
        // If possible, send transactions and pump status updates when they occur (not needed, though)
        // Always respond in delegates as fast as possible

        // This is usually not neccessary if your program has its own main loop.
        fsc.ReceiveTask.Wait();
    }

    Message[] ISessionDelegate.SessionGetProducts(Session session)
    {
        return new Message[]
        {
            MessageBuilder.Product("0020", "diesel", (decimal)0.19),
            MessageBuilder.Product("0030", "ron95e5", (decimal)0.19),
            MessageBuilder.Product("0010", "ron98", (decimal)0.19)
        };
    }

    Message[] ISessionDelegate.SessionGetPrices(Session session)
    {
        return new Message[]
        {
            MessageBuilder.Price("0010", "LTR", "EUR", (decimal)1.759, "Super Plus"),
            MessageBuilder.Price("0020", "LTR", "EUR", (decimal)1.439, "Diesel"),
            MessageBuilder.Price("0030", "LTR", "EUR", (decimal)1.659, "Super 95")
    };
    }

    Message[] ISessionDelegate.SessionGetPumps(Session session)
    {
        return new Message[]
        {
            MessageBuilder.Pump(1, "free"),
            MessageBuilder.Pump(2, "free"),
            MessageBuilder.Pump(3, "ready-to-pay"),
            MessageBuilder.Pump(4, "free"),
            MessageBuilder.Pump(5, "free")
        };
    }

    Message ISessionDelegate.SessionGetPumpStatus(Session session, int pump, int updateTTL)
    {
        if (pump == 3)
            return MessageBuilder.Pump(pump, "ready-to-pay");
        else
            return MessageBuilder.Pump(pump, "free");
    }

    Message[] ISessionDelegate.SessionGetTransactions(Session session, int pump, int updateTTL)
    {
        return new Message[]
        {
            MessageBuilder.Transaction(3, "asdf", "open", "0010", "EUR", (decimal)59.50, (decimal)50.0, (decimal)0.19, (decimal)9.50, "LTR", (decimal)47.11, (decimal)1.119)
        };
    }

    void ISessionDelegate.SessionPanMessage(Session session, string paceTransactionId, string pan)
    {
        // Do whatever you want with the PAN ;-)
    }

    Message[] ISessionDelegate.SessionClearTransaction(Session session, int pump, string siteTransactionId, string paceTransactionId)
    {
        // 1. Clear the transaction
        // 2. Optional send additional data using MessageBuilder.ReceiptInfo()
        if (pump == 3)
            return new Message[] { MessageBuilder.ReceiptInfo(paceTransactionId, "foo", "bar") };

        throw new SessionClearSiteTransactionIDUnknownException();
        //throw new SessionClearSiteTransactionIDExpiredException();
    }

    bool ISessionDelegate.SessionUnlockPump(Session session, int pump, string currency, decimal credit, string paceTransactionId, string[] productIds)
    {
        // 1. Unload the pump for given credit and productIds if given
        // return true;

        throw new UnlockPumpIDUnknownException();
    }

    bool ISessionDelegate.SessionLockPump(Session session, int pump)
    {
        // return true;
        throw new LockPumpIDUnknownException();
    }
}
