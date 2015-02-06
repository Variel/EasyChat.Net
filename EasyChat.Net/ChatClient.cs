using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Variel.EasyChat
{
    public class ChatClient: Component
    {
        private readonly TcpClient _client = new TcpClient();
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        public bool Connected
        {
            get { return _client.Connected; }
        }

        private string _hostAddress = "localhost";
        public string HostAddress
        {
            get { return _hostAddress; }
            set
            {
                if (_client.Connected)
                    throw new InvalidOperationException("이미 연결 된 상태에서 호스트 주소를 변경할 수 없습니다");

                _hostAddress = value;
            }
        }

        private int _port = 30000;
        public int Port
        {
            get { return _port; }
            set
            {
                if (_client.Connected)
                    throw new InvalidOperationException("이미 연결 된 상태에서 포트 번호를 변경할 수 없습니다");

                if(0 >= value || value >= 65536)
                    throw new ArgumentOutOfRangeException("value", "포트 번호는 1부터 65535사이의 정수입니다");

                _port = value;
            }
        }

        public event MessageReceiveDelegate Receive;

        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;
        private Thread _dataThread;

        public void Connect()
        {
            _client.Connect(HostAddress, Port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream);
            _writer = new StreamWriter(_stream);
            _dataThread = new Thread(DataReceive);
            _dataThread.IsBackground = true;
            _dataThread.Start();
        }

        public void Send(string message)
        {
            if(!_client.Connected)
                throw new InvalidOperationException("연결이 되지 않은 상태에서 메시지를 전송할 수 없습니다");

            _writer.WriteLine(message);
            _writer.Flush();
        }

        public void Disconnect()
        {
            _client.Close();
            _reader.Dispose();
            _writer.Dispose();
            _dataThread.Abort();
        }

        private void DataReceive()
        {
            while(Connected)
            {
                try
                {
                    var data = _reader.ReadLine();
                    var tmpReceive = Receive;
                    if (tmpReceive != null)
                    {
                        _syncContext.Send(_ =>
                        {
                            tmpReceive(this, data);
                        }, null);
                    }
                }
                catch(IOException)
                {
                    
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _dataThread.Abort();
        }
    }
}
