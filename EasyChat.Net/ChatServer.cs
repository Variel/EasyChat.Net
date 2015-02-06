using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Variel.EasyChat
{
    public class ChatServer : Component
    {
        private TcpListener _listener;

        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current ??
                                                               new SynchronizationContext();

        public bool Started { get; private set; }

        private int _port = 30000;

        public int Port
        {
            get { return _port; }
            set
            {
                if (Started)
                    throw new InvalidOperationException("이미 시작 된 서버의 포트를 변경할 수 없습니다");

                if (0 >= value || value >= 65536)
                    throw new ArgumentOutOfRangeException("value", "포트 번호는 1부터 65535사이의 정수입니다");

                _port = value;
            }
        }

        private Thread _dataThread;
        private List<ClientSession> _sessions = new List<ClientSession>();

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(AcceptTcpClient, null);
            Started = true;
            _dataThread = new Thread(DataReceive);
            _dataThread.IsBackground = true;
            _dataThread.Start();
        }

        public void AcceptTcpClient(IAsyncResult result)
        {
            var client = _listener.EndAcceptTcpClient(result);
            _sessions.Add(new ClientSession(client));
            _listener.BeginAcceptTcpClient(AcceptTcpClient, null);
        }

        public void DataReceive()
        {
            while (Started)
            {
                if (_sessions.Count == 0)
                {
                    Thread.Yield();
                    continue;
                }

                var tmpSessions = _sessions.ToArray();
                if (tmpSessions.Length == 0 || tmpSessions.All(s => s.Client.Connected == false))
                {
                    Thread.Yield();
                    continue;
                }

                foreach (var session in tmpSessions)
                {
                    if (!session.Stream.DataAvailable)
                        continue;

                    var data = session.Reader.ReadLine();
                    for (int i = 0; i < tmpSessions.Length; i++)
                    {
                        //if (tmpSessions[i] == session)
                        //    continue;

                        if (tmpSessions[i].Client.Connected && tmpSessions[i].Stream.CanWrite)
                        {
                            tmpSessions[i].Writer.WriteLine(data);
                            tmpSessions[i].Writer.Flush();
                        }
                        else if (!tmpSessions[i].Client.Connected)
                            _sessions.Remove(tmpSessions[i]);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _dataThread.Abort();
        }
    }

    public class ClientSession
    {
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public StreamReader Reader { get; private set; }
        public StreamWriter Writer { get; private set; }

        public ClientSession(TcpClient client)
        {
            this.Client = client;
            this.Stream = client.GetStream();
            this.Reader = new StreamReader(this.Stream);
            this.Writer = new StreamWriter(this.Stream);
        }
    }
}