using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FuelingSiteConnect 
{
    /// <summary>
    /// To be implemented by POS connector
    /// </summary>
    public interface ISessionDelegate
    {
        /// <summary>
        /// Returns XXX ... bla foo
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        Message[] SessionGetProducts(Session session);
        Message[] SessionGetPrices(Session session);
        Message[] SessionGetPumps(Session session);
        Message SessionGetPumpStatus(Session session, int pump, int updateTTL);
        Message[] SessionGetTransactions(Session session, int pump, int updateTTL);
        void SessionPanMessage(Session session, string paceTransactionId, string pan);
        Message[] SessionClearTransaction(Session session, int pump, string siteTransactionId, string paceTransactionId);
        bool SessionUnlockPump(Session session, int pump, string currency, decimal credit, string paceTransactionId, string[] productIds);
        bool SessionLockPump(Session session, int pump);
        Message[] SessionPushRequest(Session session);
    }

    public class SessionClearSiteTransactionIDUnknownException : Exception { };
    public class SessionClearSiteTransactionIDExpiredException : Exception { };
    public class UnlockPumpIDUnknownException : Exception { };
    public class LockPumpIDUnknownException : Exception { };

    public class Session 
    {
        private Client fsc;

        public string prefix { get; } = null;
        private ulong clientSequence = 0; public ulong nextSequence { get { clientSequence++; return clientSequence; } }
        private string siteAccessKey;

        public bool active { get; private set; } = true;
        public bool authenticated { get; } = false;
        private string secret;

        public ISessionDelegate sessionDelegate;

        public Session(Client client, string prefix = null)
        {
            this.fsc = client;
            this.prefix = prefix;
        }

        public async Task Authenticate(string siteAccessKey, string secret) 
        {
            this.siteAccessKey = siteAccessKey;
            this.secret = secret;
            await SendMessage(Message.PlainAuth.WithArguments(siteAccessKey, secret), true);
        }

        public async Task Quit(string reason = "Bye bye")
        {
            await SendMessage(Message.Quit.WithArguments(reason), true);
        }

        private async Task SendMessage(Message message, bool expectResponse = false, string tag = "*")
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
