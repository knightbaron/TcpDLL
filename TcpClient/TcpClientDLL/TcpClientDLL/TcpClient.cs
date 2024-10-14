using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Collections.Concurrent;

namespace TcpClientDLL
{
    /// <summary>
    /// TcpClientライブラリ
    /// </summary>
    public class TcpClientLib
    {

        #region 受信イベント

        /// <summary>
        /// TcpClient受信イベント
        /// </summary>
        public event EventHandler<TcpClientRcvInfo> TcpRecvEvent;

        /// <summary>
        /// TCP受信通知スレッド
        /// </summary>
        private Thread TcpRcvEventThread = null;

        /// <summary>
        /// TCP受信Queue
        /// </summary>
        private ConcurrentQueue<TcpClientRcvInfo> TcpRcvEventQueue = new ConcurrentQueue<TcpClientRcvInfo>();

        /// <summary>
        /// 受信Queueロック
        /// </summary>
        private Object LockTcpRcvEventQeuue = new Object();

        #endregion

        #region TcpClient

        /// <summary>
        /// 接続先TcpServerIPアドレス
        /// </summary>
        private IPAddress ServerIpAddr = null;

        /// <summary>
        /// 接続先TcpServerポート番号
        /// </summary>
        private int ServerPortNo = 0;

        /// <summary>
        /// TcpClient
        /// </summary>
        private TcpClient Client = null;

        /// <summary>
        /// エンコード
        /// </summary>
        private Encoding ENCODING;

        /// <summary>
        /// エンコード文字列：UTF8
        /// </summary>
        public const string ENCODE_UTF8 = "UTF8";

        /// <summary>
        /// エンコード文字列：UTF8
        /// </summary>
        public const string ENCODE_SJIS = "SJIS";


        /// <summary>
        /// 接続タイムアウト時間
        /// </summary>
        private const int ConnectTimeOutMiliSec = 10000;

        /// <summary>
        /// 送受信バッファサイズ
        /// </summary>
        private const int BUFFER_SIZE = 2048;

        /// <summary>
        /// 区切り文字（将来的には設定可能できるように）
        /// </summary>
        private string SPLIT_STR = "\r\n";

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


        #region コンストラクタ
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TcpClientLib()
        {

        }
        #endregion


        #region 初期化処理
        /// <summary>
        /// 初期化処理(接続は実施しません)
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="portno"></param>
        /// <param name="encode"></param>
        /// <param name="IsLogOut"></param>
        /// <param name="LogDir"></param>
        /// <param name="LogName"></param>
        /// <returns></returns>
        public bool Init(string ip , int portno , string encode ,bool IsLogOut = true, string LogDir = "", string LogName = "")
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

                    //ログライブラリ初期化処理
                }
                #endregion


                #region TcpClient設定確認

                //IpAddr
                if (!IPAddress.TryParse(ip, out ServerIpAddr))
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
                ServerPortNo = portno;

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
                #endregion

                #region 受信通知スレッド

                TcpRcvEventThread = new Thread(new ThreadStart(TcpRcvEventThreadProc));
                TcpRcvEventThread.Start();

                #endregion

                //接続は実施しない
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
        /// <returns></returns>
        public void End()
        {
            try
            {
                LogWrite(String.Format("[End] Accpet"));

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


                #region TcpClient

                try
                {
                    if (Client != null)
                    {
                        Client.Close();
                        Client.Dispose();
                    }
                }
                catch(Exception e) 
                {
                    LogWrite(String.Format("[End] TcpClient Close Exception\n{0}", e.ToString()));
                }
                #endregion


                #region ログ

                try
                {

                }
                catch { }

                #endregion

            }
            catch (Exception e)
            {
                LogWrite(String.Format("[End] Exception\n{0}", e.ToString()));
            }
        }
        #endregion


        #region 接続要求
        /// <summary>
        /// TcpServerに接続要求
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            //受信Queueの内容をクリアする（再接続するので、過去の受信内容は消しておこう）
            lock(LockTcpRcvEventQeuue)
            {
                int count = 0;
                //Queueのクリア
                for(int i = 0;i < count;i++) { TcpRcvEventQueue.TryDequeue(out _); }
            }

            //接続は同期的に行う
            tryConnect();
            bool Connected = false;

            if (Client != null)
            {
                Connected = Client.Connected;
            }

            return Connected;
        }

        /// <summary>
        /// 接続実施
        /// </summary>
        private async void tryConnect()
        {
            try
            {
                if (Client != null && Client.Client != null && Client.Connected)
                {
                    //すでに接続してたら 再接続させない
                    LogWrite("[Connect] Already Connected with TcpServer");
                    return;
                }

                try
                {
                    using (Client = new TcpClient())
                    {
                        //KeepAliveは未使用（将来的には実装）

                        ////Keepaliveを使う場合
                        //Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        //byte[] tcp_keepalive = new byte[12];
                        //BitConverter.GetBytes((Int32)1).CopyTo(tcp_keepalive, 0);//onoffスイッチ.
                        //BitConverter.GetBytes((Int32)2000).CopyTo(tcp_keepalive, 4);//wait time.(ms)
                        //BitConverter.GetBytes((Int32)500).CopyTo(tcp_keepalive, 8);//interval.(ms)
                        //                                                           // keep-aliveのパラメータ設定
                        //Client.Client.IOControl(IOControlCode.KeepAliveValues, tcp_keepalive, null);


                        var task = Client.ConnectAsync(ServerIpAddr, ServerPortNo);
                        if (await Task.WhenAny(task, Task.Delay(ConnectTimeOutMiliSec)) != task)
                        {
                            //タイムアウトの例外
                            throw new SocketException(10060);
                        }

                        //接続完了
                        LogWrite("[Connect] Connect Success");


                        //受信タスクを開始
                        _ = Recievewait_Async();

                        return;
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 10060)
                    {
                        LogWrite(String.Format("[Connect] Connect Timeout"));
                        return;
                    }
                    else
                    {
                        LogWrite(String.Format("[Connect] Connect SocketException\n{0}", ex.ToString()));
                        return;
                    }
                }
                catch (Exception e)
                {
                    LogWrite(String.Format("[Connect] Connect Exception\n{0}", e.ToString()));
                    return;
                }
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[Connect] Exception\n{0}", e.ToString()));
                return;
            }
        }
        #endregion

        #region 切断要求
        /// <summary>
        /// 切断要求
        /// </summary>
        public void DisConnect()
        {
            try
            {
                LogWrite(String.Format("[DisConnect] Accept"));
                //切断処理
                Client.Close();
                Client.Dispose();
                Client = null;

                LogWrite(String.Format("[DisConnect] DisConnect Success"));
            }
            catch (Exception e)
            {
                LogWrite(String.Format("[DisConnect] Exception\n{0}",e.ToString()));
            }
        }
        #endregion


        #region 送信
        /// <summary>
        /// 送信
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool Send(string message)
        {
            try
            {
                LogWrite(String.Format("[Send] Accept"));

                if (Client ==  null)
                {
                    LogWrite(String.Format("[Send] TcpClient is NULL"));
                    return false;
                }

                if (!Client.Connected)
                {
                    LogWrite(String.Format("[Send] TcpClient DisConnected with TcpServer"));
                    return false;
                }

                if (message == null)
                {
                    LogWrite(String.Format("[Send] Send Buffer is NULL"));
                    return false;
                }

                NetworkStream ns = null;
                bool SendSuccess = false;
                try
                {
                    string sendBuf = message + SPLIT_STR;

                    byte[] message_byte = ENCODING.GetBytes(message + "\r\n");

                    do
                    {
                        ns.Write(message_byte, 0, message_byte.Length);
                    } while (ns.DataAvailable);

                    LogWrite(String.Format("TcpSend : {0}", sendBuf));

                    LogWrite(String.Format("[Send] Success"));

                    SendSuccess = true;

                }
                catch(Exception e)
                {
                    LogWrite(String.Format("[Send] Send Exception\n{0}", e.ToString()));
                }
                finally
                {
                    if (ns != null) { ns.Close(); }
                }

                return SendSuccess;
            }
            catch(Exception e)
            {
                LogWrite(String.Format("[Send] Exception\n{0}",e.ToString()));
                return false;
            }
            
        }
        #endregion

        #region OLD


        ////接続開始
        //private async Task ConnectStartAsync(string ip, int port)
        //{

        //    //if (Client != null && Client.Client != null && Client.Connected)
        //    //{
        //    //    //すでに接続してたら 再接続させない
        //    //    return;
        //    //}
        //    //if (connecting_flg)
        //    //{
        //    //    //すでに接続処理中なら接続処理させない
        //    //    return;
        //    //}

        //    //Debug.WriteLine($"Connectスレッド:{Thread.CurrentThread.ManagedThreadId}");


        //    ////TcpClientのインスタンスを作成
        //    //Client = new TcpClient();


        //    ////接続開始
        //    //try
        //    //{
        //    //    connecting_flg = true;//接続処理中フラグ ON
        //    //    //接続 (非同期実行)
        //    //    await Client.ConnectAsync(ip, port);
        //    //}
        //    //catch (System.Net.Sockets.SocketException)
        //    //{
        //    //    //接続失敗 (失敗すると この例外が発生する)
        //    //    connecting_flg = false;//接続処理中フラグ OFF
        //    //    Client.Close();
        //    //    //ConnectNgCB();//Form1にコールバック
        //    //    return;
        //    //}
        //    //catch
        //    //{
        //    //    //ごくまれにSystem.NullReferenceExceptionなど起きる
        //    //    connecting_flg = false;//接続処理中フラグ OFF
        //    //    Client.Close();
        //    //    //ConnectNgCB();//Form1にコールバック
        //    //    return;
        //    //}

        //    ////接続成功
        //    //connecting_flg = false;//接続処理中フラグ OFF
        //    ////受信タスクを開始
        //    //_ = Recievewait_Async();

        //}
        #endregion


        #region 受信
        //非同期でクライアントから文字列受信を待ち受ける
        private async Task Recievewait_Async()
        {
            LogWrite(String.Format("[RecieveWait] RecvWait Start"));

            MemoryStream ms = null;
            NetworkStream ns = null;
            int result_size = 0;
            bool IsConnected = true;

            try
            {
                ns = Client.GetStream();

                while(EndApp.Count == 0)
                {

                    //受信処理
                    try
                    {
                        ms = new MemoryStream();
                        byte[] result_bytes = new byte[BUFFER_SIZE];

                        do
                        {
                            result_size = 0;
                            try
                            {
                                //受信 (非同期実行)
                                result_size = await ns.ReadAsync(result_bytes, 0, result_bytes.Length);

                                if (result_size == 0)
                                {
                                    IsConnected = false;
                                    LogWrite(String.Format("[RecieveWait] Detect DisConnect By Client due to Read Size ZERO"));
                                    break;
                                }
                            }
                            catch (System.IO.IOException ex)
                            {
                                //LANケーブルが抜けたときKeepaliveによってこの例外が発生する

                                LogWrite(string.Format("[RecieveWait] Read Exception\n{0}", ex.ToString()));
                                IsConnected = false;
                            }

                            catch (Exception ex)
                            {
                                LogWrite(string.Format("[RecieveWait] Read Exception\n{0}", ex.ToString()));
                                IsConnected = false;
                            }

                            //受信成功
                            ms.Write(result_bytes, 0, result_size);

                        } while (ns.DataAvailable);
                    }
                    catch(Exception e)
                    {
                        LogWrite(string.Format("[RecieveWait] Read Exception\n{0}", e.ToString()));
                        IsConnected = false;
                    }

                    //受信解析
                    try
                    {
                        //切断判定
                        if (!IsConnected)
                        {
                            LogWrite(String.Format("[RecieveWait] Determine DisConnect"));
                            break;
                        }

                        //受信成功
                        string rcvBuff = ENCODING.GetString(ms.ToArray());

                        if (rcvBuff == null)
                        {
                            LogWrite(String.Format("[RecieveWait] Recv Data is NULL"));
                            continue;
                        }
                        else
                        {
                            LogWrite(String.Format("TcpRecv : {0}", rcvBuff));

                            RecvAnalyze(rcvBuff);
                        }
                    }
                    catch (Exception e)
                    {
                        LogWrite(String.Format("[RecieveWait] Analyze Exception\n{0}", e.ToString()));
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

            catch(Exception e)
            {
                LogWrite(String.Format("[Send] Exception\n{0}", e.ToString()));
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
            }
        }


        /// <summary>
        /// 受信内容解析
        /// </summary>
        /// <param name="rcvBuff"></param>
        private void RecvAnalyze(string rcvBuff)
        {
            try
            {
                if (rcvBuff == null)
                {
                    LogWrite(String.Format("[RecvAnalyze] RecvBuff is NULL"));
                    return;
                }

                //区切り文字で分割
                string[] lines = rcvBuff.Split(new string[] { SPLIT_STR }, StringSplitOptions.None);

                lock (LockTcpRcvEventQeuue)
                {
                    foreach (string rcv in lines)
                    {
                        TcpClientRcvInfo info = new TcpClientRcvInfo(rcv);
                        TcpRcvEventQueue.Enqueue(info);
                    }
                }
            }
            catch(Exception e)
            {
                LogWrite(String.Format("[RecvAnalyze] Exception\n{0}",e.ToString()));
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
                TcpClientRcvInfo info = null;

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

                        if ((info == null) | (info.RcvBuffer == null))
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


    /// <summary>
    /// Tcp受信情報
    /// </summary>
    public class TcpClientRcvInfo
    {
        /// <summary>
        /// 受信日時
        /// </summary>
        public DateTime Date;

        /// <summary>
        /// 受信内容
        /// </summary>
        public string RcvBuffer = null;

        /// <summary>
        /// Tcp受信
        /// </summary>
        /// <param name="buf"></param>
        public TcpClientRcvInfo(string buf)
        {
            Date = DateTime.Now;
            RcvBuffer = buf;
        }
    }

}
