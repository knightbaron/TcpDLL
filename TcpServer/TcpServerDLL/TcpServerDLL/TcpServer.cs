using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;

namespace TcpServerDLL
{
    public class TcpServerLib
    {
        #region 受信イベント

        //Tcp受信時のコールバック
        //public delegate void RecieveDataCallback(string name, string data);
        //public event RecieveDataCallback TcpRecvEvent;

        public event EventHandler<TcpServerRcvInfo> TcpRecvEvent;

        /// <summary>
        /// TCP受信通知スレッド
        /// </summary>
        private Thread TcpRcvEventThread = null;

        /// <summary>
        /// TCP受信Queue
        /// </summary>
        private ConcurrentQueue<TcpServerRcvInfo> TcpRcvEventQueue = new ConcurrentQueue<TcpServerRcvInfo>();

        private Object LockTcpRcvEventQeuue = new Object();

        #endregion

        #region TcpServer設定

        /// <summary>
        /// IPアドレス
        /// </summary>
        private IPAddress ServerIPAddr;

        /// <summary>
        /// 待ち受けポート番号
        /// </summary>
        private int PortNo = 0;

        /// <summary>
        /// エンコード
        /// </summary>
        private Encoding ENCODING;


        /// <summary>
        /// TCPSERVER
        /// </summary>
        private TcpListener LISTENER;


        /// <summary>
        /// エンコード文字列：UTF8
        /// </summary>
        public const string ENCODE_UTF8 = "UTF8";

        /// <summary>
        /// エンコード文字列：UTF8
        /// </summary>
        public const string ENCODE_SJIS = "SJIS";

        /// <summary>
        /// 接続待機状態
        /// </summary>
        private bool ListeningFlg = false;

        /// <summary>
        /// 接続待機有無
        /// <para> True : 接続待機中</para>
        /// </summary>
        private bool IsListening
        {
            get
            {
                return ListeningFlg;
            }
        }

        /// <summary>
        /// 送受信バッファサイズ
        /// </summary>
        private const int BUFFER_SIZE = 2048;

        /// <summary>
        /// 区切り文字（将来的には設定可能できるように）
        /// </summary>
        private string SPLIT_STR = "\r\n";

        #endregion

        #region クライアント管理情報

        /// <summary>
        /// 接続数(接続クライアントのナンバリングに使用)
        /// </summary>
        private int connected_number;


        /// <summary>
        /// TCPClientリスト
        /// TCPClientリスト
        /// </summary>
        private List<ClientInfo> CLIENT_LIST;

        private object LockClientList = new object();

        #endregion

        #region ログ

        /// <summary>
        /// ログのデフォルト名称
        /// </summary>
        public const string LogDefaultDirName = "Log\\";

        /// <summary>
        /// ログのデフォルト名称
        /// </summary>
        public const string LogDefaultFileName = "TcpServerLib";

        #endregion

        /// <summary>
        /// 終了用Queue
        /// </summary>
        private ConcurrentQueue<bool> EndApp = new ConcurrentQueue<bool>();


        #region 初期化終了処理
        //コンストラクタ
        public TcpServerLib()
        {

        }
        #endregion


        #region 初期化

        /// <summary>
        /// 初期化処理（受信待機開始）
        /// </summary>
        /// <param name="ip">IPアドレス</param>
        /// <param name="portno">ポート番号</param>
        /// <param name="encode">エンコード文字列（UTF8 or SJIS)</param>
        /// <param name="IsLogOut">ログ出力有無</param>
        /// <param name="LogDir">ログ出力先絶対パス</param>
        /// <param name="LogName">ログファイル名称</param>
        /// <returns></returns>
        public bool Init(string ip, int portno, string encode, bool IsLogOut = true, string LogDir = "", string LogName = "")
        {
            try
            {
                #region ログ

                if (IsLogOut)
                {
                    //ログフォルダ
                    string LogDirectory = "";

                    //ログフォルダ未設定または指定フォルダが存在しない
                    if ((LogDir == String.Empty) | !Directory.Exists(LogDir))
                    {
                        //環境パス取得
                        string CurrentPath = "";
                        LogDirectory = CurrentPath + "/" + LogDefaultDirName;
                    }
                    else
                    {
                        LogDirectory = LogDir;
                    }

                    //ログ名称が未設定
                    string LogFileName = "";
                    if (LogName == String.Empty)
                    {
                        LogFileName = LogDefaultFileName;
                    }
                    else
                    {
                        LogFileName = LogName;
                    }
                }
                #endregion


                #region クライアント情報の初期化

                //接続No初期化
                connected_number = 0;
                //接続中のクライアントリスト作成
                CLIENT_LIST = new List<ClientInfo>();

                #endregion


                #region TCPServer初期化

                try
                {
                    //IpAddr
                    if (!IPAddress.TryParse(ip, out ServerIPAddr))
                    {
                        LogWrite(String.Format("[Init] Invalid IpAddr Setting (IpAddr : {0})", ip));
                        return false;
                    }

                    //PortNo
                    if ((portno <= 0) | (portno > 65535))
                    {
                        LogWrite(String.Format("[Init] Invalid PortNo Setting (PortNo : {0})", portno));
                        return false;
                    }
                    PortNo = portno;

                    //エンコード文字列
                    if (encode == ENCODE_UTF8)
                    {
                        ENCODING = Encoding.UTF8;
                    }
                    else if (encode == ENCODE_SJIS)
                    {
                        ENCODING = Encoding.GetEncoding(932);
                    }
                    else
                    {
                        LogWrite(String.Format("[Init] Invalid Encode Setting (Encode : {0})", encode));
                        return false;
                    }

                    //待受け用のリスナーインスタンス作成
                    LISTENER = new TcpListener(ServerIPAddr, PortNo);

                    //接続待機開始
                    LISTENER.Start();

                    //接続待ちタスク開始
                    _ = Acceptwait_Async();
                }
                catch (Exception e)
                {
                    LogWrite(String.Format("[Init] TcpServer Start Exception\n{0}", e.ToString()));
                    return false;
                }


                #endregion


                #region 受信通知スレッド

                TcpRcvEventThread = new Thread(new ThreadStart(TcpRcvEventThreadProc));
                TcpRcvEventThread.Start();

                #endregion

                return true;

            }
            catch (Exception e)
            {
                LogWrite(String.Format("[Init] Exception\n{0}", e.ToString()));
                return false;
            }
        }
        #endregion


        #region 終了処理

        /// <summary>
        /// 終了処理
        /// </summary>
        public void End()
        {
            LogWrite("[End] Accept");

            EndApp.Enqueue(true);
            Task.Delay(500).Wait();

            try
            {
                if (TcpRcvEventThread != null)
                {
                    TcpRcvEventThread.Abort();
                }
            }
            catch { }
            try
            {
                try
                {
                    //接続中のすべてのクライアントに対して、切断実施
                    DisconnectAllClients();

                    //接続待機終了
                    LISTENER.Stop();
                }
                catch (Exception e)
                {
                    LogWrite(String.Format("[End] TcpServer Close Exception\n{0}", e.ToString()));
                }
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[End] Exception\n{0}", e.ToString()));
            }

        }


        //接続済みクライアントを全切断
        private void DisconnectAllClients()
        {
            //クライアント分まわし、Closeして切断
            foreach (ClientInfo cd in CLIENT_LIST)
            {
                cd.Client.Close();
            }

            lock (LockClientList)
            {
                //接続しているクライアントリストをクリア
                CLIENT_LIST.Clear();
            }
        }
        #endregion


        #region 接続待機
        //非同期でクライアントからの接続を待ち受ける
        private async Task Acceptwait_Async()
        {
            int id = 0;
            try
            {
                id = Thread.CurrentThread.ManagedThreadId;
                LogWrite(String.Format("[AcceptWait] Activate (ThreadID : {0})", id));
                while (EndApp.Count == 0)
                {
                    try
                    {
                        TcpClient client = null;

                        try
                        {
                            ListeningFlg = true;
                            //接続待ち (非同期実行)
                            client = await LISTENER.AcceptTcpClientAsync();
                        }
                        catch (System.ObjectDisposedException ex)
                        {
                            LogWrite(String.Format("[AcceptWait] End Conform in AcceptWait\n{0}", ex.ToString()));
                            ListeningFlg = false;
                            //listennerをstopさせた場合は終わらせる
                            return;
                        }

                        //KeepAliveなし（将来敵には設定できたほうがいいか？）

                        //if (true)//Keepaliveを使う場合
                        //{
                        //    //Keepaliveを使う場合
                        //    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        //    byte[] tcp_keepalive = new byte[12];
                        //    BitConverter.GetBytes((Int32)1).CopyTo(tcp_keepalive, 0);//onoffスイッチ.
                        //    BitConverter.GetBytes((Int32)2000).CopyTo(tcp_keepalive, 4);//wait time.(ms)
                        //    BitConverter.GetBytes((Int32)500).CopyTo(tcp_keepalive, 8);//interval.(ms)
                        //                                                               // keep-aliveのパラメータ設定
                        //    client.Client.IOControl(IOControlCode.KeepAliveValues, tcp_keepalive, null);
                        //}

                        //接続あり

                        //クライアントの追加
                        //ClientInfo client_data = new ClientInfo();
                        //client_data.Client = client;
                        //client_data.No = connected_number++;
                        string ip = "";
                        int portno = 0;




                        try
                        {
                            ip = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                            if (!int.TryParse(((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Port.ToString(), out portno))
                            {
                                LogWrite(String.Format("[AcceptWait] Fail to Get Client Infomation"));
                            }
                        }
                        catch (Exception e)
                        {
                            LogWrite(String.Format("[AcceptWait] Get Client Infomation Exception\n{0}", e.ToString()));
                            continue;
                        }

                        int connectNo = connected_number++;
                        ClientInfo client_data = new ClientInfo(client, connectNo, ip, portno);


                        lock (LockClientList)
                        {
                            CLIENT_LIST.Add(client_data);
                        }

                        //受信タスクを開始
                        _ = Recievewait_Async(client, client_data);
                    }
                    catch (Exception e)
                    {
                        LogWrite(String.Format("[AcceptWait] AcceptWaitException\n{0}", e.ToString()));
                    }

                }
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[AcceptWait] AcceptWaitException\n{0}", e.ToString()));
            }

            LogWrite(String.Format("[AcceptWait] Dectivate (ThreadID : {0})", id));
        }
        #endregion


        #region 受信処理
        //非同期でクライアントから文字列受信を待ち受ける
        private async Task Recievewait_Async(TcpClient client, ClientInfo client_data)
        {
            string log = String.Format("[RecieveWait] ID : {0} -> ", client_data.No);

            Debug.WriteLine("[受信待ち](接続No:{0})", client_data.No);

            LogWrite(String.Format("{0}RecvWait Start", log));

            bool IsConnected = true;

            MemoryStream ms = null;
            NetworkStream ns = null;

            try
            {
                ns = client.GetStream();

                while (EndApp.Count == 0)
                {
                    ms = new MemoryStream();
                    byte[] result_bytes = new byte[BUFFER_SIZE];

                    //受信関連
                    try
                    {
                        do
                        {
                            int result_size = 0;
                            try
                            {
                                //受信 (非同期実行)
                                result_size = await ns.ReadAsync(result_bytes, 0, result_bytes.Length);

                                if (result_size == 0)
                                {
                                    IsConnected = false;

                                    LogWrite(String.Format("{0} Detect DisConnect By Client due to Read Size ZERO"));
                                    break;
                                }
                            }

                            catch (System.IO.IOException ex)
                            {
                                //LANケーブルが抜けたときKeepaliveによってこの例外が発生する
                                LogWrite(String.Format("{0}Read Exception\n{1}", log, ex.ToString()));
                                IsConnected = false;
                                break;
                            }

                            ms.Write(result_bytes, 0, result_size);

                        } while (ns.DataAvailable);
                    }
                    catch (Exception e)
                    {
                        LogWrite(String.Format("{0}Read Exception\n{1}", log, e.ToString()));
                        IsConnected = false;
                    }

                    //受信解析
                    try
                    {
                        //切断判定
                        if (!IsConnected)
                        {
                            LogWrite(String.Format("{0}Determine DisConnect", log));
                            break;
                        }

                        //受信成功
                        string rcvBuff = ENCODING.GetString(ms.ToArray());

                        LogWrite(String.Format("TcpRecv : {0}", rcvBuff));

                        //コマンド部やデータ部などの文字列操作(最終的にはForm1へコールバックしたりする)
                        RecvAnalyze(client_data, rcvBuff);
                    }
                    catch (Exception e)
                    {
                        LogWrite(String.Format("{0}Analyze Exception\n{1}", log, e.ToString()));
                    }
                    finally
                    {
                        //一応メモリ破棄
                        if (ms != null)
                        {
                            ms.Close();
                            ms.Dispose();
                            ms = null;
                        }
                    }
                }
            }

            catch (Exception e)
            {
                LogWrite(String.Format("{0}Exception\n{1}", log, e.ToString()));
            }

            finally
            {
                if (ms != null)
                {
                    ms.Close();
                    ms.Dispose();
                    ms = null;
                }

                if (ns != null)
                {
                    ns.Close();
                    ns.Dispose();
                    ns = null;
                }

                lock (LockClientList)
                {
                    //接続情報を削除
                    CLIENT_LIST.Remove(client_data);
                }
            }
        }

        /// <summary>
        /// 受信内容解析
        /// </summary>
        /// <param name="no"></param>
        /// <param name="name"></param>
        /// <param name="rcvBuff"></param>
        private void RecvAnalyze(ClientInfo rcvInfo, string rcvBuff)
        {
            //区切り文字で分割
            string[] lines = rcvBuff.Split(new string[] { SPLIT_STR }, StringSplitOptions.None);

            lock (LockTcpRcvEventQeuue)
            {
                foreach (string rcv in lines)
                {
                    TcpServerRcvInfo info = new TcpServerRcvInfo(rcvInfo.Client, rcv);
                    TcpRcvEventQueue.Enqueue(info);
                }
            }
        }

        /// <summary>
        /// TCP受信イベント通知スレッド
        /// </summary>
        private void TcpRcvEventThreadProc()
        {
            LogWrite("[TcpRcvEvent] Activate");

            try
            {
                int QueueCount = 0;
                TcpServerRcvInfo info = null;

                while (EndApp.Count == 0)
                {
                    try
                    {
                        QueueCount = 0;
                        lock (LockTcpRcvEventQeuue)
                        {
                            QueueCount = TcpRcvEventQueue.Count;
                        }

                        //Queueなしの場合は待機後Queue待機に戻る
                        if (QueueCount == 0)
                        {
                            Task.Delay(100).Wait();
                            continue;
                        }

                        //受信Queueあり
                        info = null;

                        if (!TcpRcvEventQueue.TryDequeue(out info))
                        {
                            LogWrite(String.Format("[TcpRcvEvent] TryDequeue Error"));
                            continue;
                        }

                        if ((info == null) | (info.Client == null) | (info.RcvBuffer == null))
                        {
                            LogWrite(String.Format("[TcpRcvEvent] Data NULL"));
                            continue;
                        }

                        //受信イベント登録あり
                        if (TcpRecvEvent != null)
                        {
                            //登録もとに通知
                            TcpRecvEvent(this, info);
                        }

                    }
                    catch (Exception e)
                    {
                        LogWrite(String.Format("[TcpRcvEvent] Exception\n{0}", e.ToString()));
                    }
                }
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[TcpRcvEvent] Exception\n{0}", e.ToString()));
            }

            LogWrite("[TcpRcvEvent] DeActivate");
        }

        #endregion


        #region 送信

        /// <summary>
        /// 指定先へ送信
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="portno"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool Send(string ip, int portno, string message)
        {
            try
            {
                //IPアドレスとポート番号がクライアント情報に存在するか確認
                TcpClient client = null;

                try
                {
                    lock (LockClientList)
                    {
                        for (int i = 0; i < CLIENT_LIST.Count; i++)
                        {
                            if ((CLIENT_LIST[i].PortNo == portno)
                                & (CLIENT_LIST[i].IpAddr == ip))
                            {
                                client = CLIENT_LIST[i].Client;
                                break;
                            }
                        }
                    }

                    if (client == null)
                    {
                        LogWrite(String.Format("[Send] DstInfo is Not Exist (Ip : {0} , PortNo : {1})", ip, portno));
                        return false;
                    }
                }
                catch (Exception e)
                {
                    LogWrite(String.Format("[Send] Find DstInfo Exception\n{0}", e.ToString()));
                    return false;
                }

                //送信先あり
                if (BufferSend(client, message))
                {
                    LogWrite(String.Format("[Send] Success"));
                    return true;
                }
                else
                {
                    LogWrite(String.Format("[Send] Fail"));
                    return false;
                }

            }
            catch (Exception e)
            {
                LogWrite(String.Format("[Send] Exception\n{0}", e.ToString()));
                return false;
            }

        }



        //クライアントに文字列送信
        private bool BufferSend(TcpClient client, string message)
        {
            bool result = false;
            try
            {
                string send = message + SPLIT_STR;
                byte[] message_byte = ENCODING.GetBytes(send);

                NetworkStream ns = null;
                try
                {
                    ns = client.GetStream();
                    do
                    {
                        ns.Write(message_byte, 0, message_byte.Length);
                    } while (ns.DataAvailable);

                    //送信成功
                    LogWrite(String.Format("TcpSend : {0}", send));

                    result = true;
                }
                catch (System.InvalidOperationException ex)
                {
                    //切断と同時に送信した場合この例外が発生する
                    LogWrite(String.Format("[BufferSend] InvalidOperationException\n{0}", ex.ToString()));
                }
                catch (Exception e)
                {
                    LogWrite(String.Format("[BufferSend] Exception\n{0}", e.ToString()));
                }
                finally
                {
                    if (ns != null)
                    {
                        ns.Close();
                        ns.Dispose();
                    }
                }

                //送信結果
                return result;
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[BufferSend] Exception\n{0}", e.ToString()));
                return false;
            }
        }


        #endregion


        #region ログ
        /// <summary>
        /// ログ書き込み
        /// </summary>
        /// <param name="log"></param>
        private void LogWrite(string log)
        {
            try
            {
                if (log != null)
                {
                    Debug.Print(log);


                }
            }
            catch { }
        }
        #endregion
    }
}
