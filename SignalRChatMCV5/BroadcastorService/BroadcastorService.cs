using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using Microsoft.AspNet.SignalR.Client;
using System.Net.Http;

namespace BroadcastorService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class BroadcastorService : IBroadcastorService
    {
        private static Dictionary<string, IBroadcastorCallBack> clients = new Dictionary<string, IBroadcastorCallBack>();
        private static object locker = new object();

        private IHubProxy HubProxy { get; set; }
        const string ServerURI = "http://localhost:41631/signalr";
        private HubConnection Connection { get; set; }

        public BroadcastorService()
        {
            if (Connection == null || Connection.State == ConnectionState.Disconnected)
            {
                ConnectAsync();
            }
        }


        public void RegisterClient(string clientName)
        {
            if (clientName != null && clientName != "")
            {
                try
                {
                    IBroadcastorCallBack callback =
                        OperationContext.Current.GetCallbackChannel<IBroadcastorCallBack>();
                    lock (locker)
                    {
                        //remove the old client
                        if (clients.Keys.Contains(clientName))
                            clients.Remove(clientName);
                        clients.Add(clientName, callback);
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        public void NotifyServer(EventDataType eventData)
        {
            lock (locker)
            {
                var inactiveClients = new List<string>();
                foreach (var client in clients)
                {
                    //if (client.Key != eventData.ClientName)
                    //{
                    try
                    {
                        client.Value.BroadcastToClient(eventData);
                        if (Connection != null && Connection.State == ConnectionState.Connected)
                        {
                            HubProxy.Invoke("Send", eventData.ClientName, eventData.EventMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        inactiveClients.Add(client.Key);
                    }
                    //}
                }

                if (inactiveClients.Count > 0)
                {
                    foreach (var client in inactiveClients)
                    {
                        clients.Remove(client);
                    }
                }
            }
        }

        private async void ConnectAsync()
        {
            Connection = new HubConnection(ServerURI);
            Connection.Closed += Connection_Closed;
            HubProxy = Connection.CreateHubProxy("ChatHub");
            //Handle incoming event from server: use Invoke to write to console from SignalR's thread

            try
            {
                await Connection.Start();
            }
            catch (HttpRequestException)
            {
                return;
            }
        }

        private void Connection_Closed()
        {
        }
    }
}
