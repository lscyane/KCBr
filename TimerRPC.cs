﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KCB2
{
    public class TimerRPCManager
    {
        /// <summary>
        /// 入渠情報
        /// </summary>
        class NDockInfo
        {
            public int DockNum { get; private set; }
            public string ShipName { get; private set; }
            public DateTime FinishTime { get; private set; }

            public NDockInfo(KCB2.MemberData.Dock.NDock dock)
            {
                DockNum = dock.Order;
                ShipName = dock.Name;
                FinishTime = dock.Finish;
            }

            public override string ToString()
            {
                return string.Format("NDockInfo DockNum:{0} ShipName:{1} Finish:{2}",
                    DockNum, ShipName, FinishTime);
            }
        }

        /// <summary>
        /// 遠征情報
        /// </summary>
        class MissionInfo
        {
            public int FleetNum { get; private set; }
            public string FleetName { get; private set; }
            public string MissionName { get; private set; }
            public DateTime FinishTime { get; private set; }

            public MissionInfo(KCB2.MemberData.Deck.Fleet fleet)
            {
                Debug.WriteLine(fleet.ToString());
                FleetNum = fleet.Num;
                FleetName = fleet.Name;
                MissionName = fleet.Mission;
                FinishTime = fleet.MissionFinish;
            }

            public override string ToString()
            {
                return string.Format("MissionInfo Fleet:{0}({1}) Mission:{2} Finish:{3}",
                    FleetName, FleetNum, MissionName, FinishTime);
            }

        }

        MissionInfo[] _missionList = new MissionInfo[4];
        NDockInfo[] _ndockList = new NDockInfo[4];
        int _ndockCount = 0;
        int _deckCount = 0;
        string _memberID = "";

        ChannelFactory<KCB.RPC.IUpdateNotification> _notifyFactory;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TimerRPCManager()
        {
            ServerHost = Properties.Settings.Default.NotificationServer;
        }

        string _serverHost = "";
        public string ServerHost
        {
            get { return _serverHost; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    SwitchToNamedPipe();
                else
                    SwitchToNetTcpServer(value);

                _serverHost = value;
            }
        }

        private void SwitchToNetTcpServer(string host)
        {
            var bind = new NetTcpBinding(SecurityMode.None);
            bind.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            _notifyFactory = new ChannelFactory<KCB.RPC.IUpdateNotification>(
                     bind,
                    new EndpointAddress(string.Format("net.tcp://{0}/kcb-update-channel", host))
                    );
            var defCred = _notifyFactory.Endpoint.Behaviors.Find<System.ServiceModel.Description.ClientCredentials>();
            defCred.UserName.UserName = "hoge";
            defCred.UserName.Password = "fuga";

            Debug.WriteLine("Notify target:TCP:" + host);
        }

        private void SwitchToNamedPipe()
        {
            _notifyFactory = new ChannelFactory<KCB.RPC.IUpdateNotification>(
                      new NetNamedPipeBinding(),
                    new EndpointAddress("net.pipe://localhost/kcb-update-channel")
                    );
            Debug.WriteLine("Notify target:NamedPipe");
        }

        /// <summary>
        /// 修理入渠情報をタイマに反映するRPC呼びだし
        /// </summary>
        /// <param name="dockData"></param>
        public void UpdateNDock(KCB2.MemberData.Dock dockData)
        {
            foreach (var dock in dockData.RepairDock)
            {
                var info = new NDockInfo(dock);
                _ndockList[dock.Order - 1] = info;
                System.Diagnostics.Debug.WriteLine(info);
                RPCUpdateNDock(info);
            }
        }

        /// <summary>
        /// 遠征情報をタイマに反映するRPC呼び出し
        /// </summary>
        /// <param name="deckData"></param>
        public void UpdateMission(IEnumerable<MemberData.Deck.Fleet> deckList)
        {
            lock (deckList)
            {
                foreach (var fleet in deckList)
                {
                    var info = new MissionInfo(fleet);
                    _missionList[info.FleetNum - 1] = info;
                    System.Diagnostics.Debug.WriteLine(info);
                    RPCUpdateMission(info);
                }
            }
        }

        /// <summary>
        /// キャッシュしている情報をRPCでタイマに送信
        /// </summary>
        public void UpdateTimerState()
        {
            RPCUpdateParameters(_memberID,_ndockCount,_deckCount);

            foreach (var info in _missionList)
                RPCUpdateMission(info);

            foreach (var info in _ndockList)
                RPCUpdateNDock(info);
        }

        /// <summary>
        /// UpdateNDockをWCF RPCで呼び出す
        /// </summary>
        /// <param name="info"></param>
        /// <returns>失敗したらfalse</returns>
        void RPCUpdateNDock(NDockInfo info)
        {
            if (info == null)
                return;
            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    /* RPCが失敗するとproxy objectに失敗フラグが立つようで、
                     * 再度callすると以下の例外が飛ぶ。
                     * なので、proxy objectは毎回生成することにした。
                     * 
                     * CommunicationObjectFaultedException 
                     * 通信オブジェクト System.ServiceModel.Channels.ServiceChannel は、
                     * 状態が Faulted であるため通信に使用できません。
                     */
                    var notifyProxy = _notifyFactory.CreateChannel();
                    notifyProxy.UpdateNDock(info.DockNum, info.ShipName ?? "", info.FinishTime);
                    ((IClientChannel)notifyProxy).Close();
                }
                catch (EndpointNotFoundException exp)
                {
                    Debug.WriteLine("UpdateNDock:EndpointNotFoundException\n"
                        + exp.ToString());
                }
            });

            return;
        }

        /// <summary>
        /// UpdateMissionをWCF RPCで呼び出す
        /// </summary>
        /// <param name="info"></param>
        /// <returns>失敗したらfalse</returns>
        void RPCUpdateMission(MissionInfo info)
        {
            if (info == null)
                return;
            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
           {
               try
               {
                    //上に同じ
                    var notifyProxy = _notifyFactory.CreateChannel();
                   notifyProxy.UpdateMission(info.FleetNum, info.FleetName ?? "",
                       info.MissionName ?? "", info.FinishTime);
                   ((IClientChannel)notifyProxy).Close();
               }
               catch (EndpointNotFoundException exp)
               {
                   Debug.WriteLine("UpdateMission:EndpointNotFoundException\n"
                       + exp.ToString());
               }
           });

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="basicInfo"></param>
        /// <returns></returns>
        public void UpdateParameters(KCB2.MemberData.Basic basicInfo)
        {
            _deckCount = basicInfo.Deck;
            _ndockCount = basicInfo.NDock;
            _memberID = basicInfo.MemberID;
            RPCUpdateParameters(_memberID,_ndockCount, _deckCount);
        }

        void RPCUpdateParameters(string memberID,int nDockCount,int deckCount)
        {
            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
           {
               try
               {
                    //上に同じ
                    var notifyProxy = _notifyFactory.CreateChannel();
                   notifyProxy.UpdateParameters(memberID, nDockCount, deckCount);
                   ((IClientChannel)notifyProxy).Close();
               }
               catch (EndpointNotFoundException exp)
               {
                   Debug.WriteLine("UpdateParametersn:EndpointNotFoundException\n"
                       + exp.ToString());
               }
           });
        }


        int _condTimerFleetNo = 0;
        string _condTimerFleetName = "";
        DateTime _condTimerFinishTime = DateTime.MinValue;

        public void UpdateConditionTimer(DeckMemberList.DeckStatus deckStatus)
        {
            if (_condTimerFleetNo == deckStatus.FleetNo && _condTimerFleetName == deckStatus.FleetName
                 && _condTimerFinishTime == deckStatus.RecoverTime)
                return;

            _condTimerFleetNo = deckStatus.FleetNo;
            _condTimerFleetName = deckStatus.FleetName;
            _condTimerFinishTime = deckStatus.RecoverTime;


            RPCUpdateConditionTimer(deckStatus.FleetNo, deckStatus.FleetName,
                deckStatus.RecoverTime);
        }

        void RPCUpdateConditionTimer(int fleetNum, string fleetName,  DateTime finishTime)
        {

            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
           {
               try
               {
                    //上に同じ
                    var notifyProxy = _notifyFactory.CreateChannel();
                   notifyProxy.UpdateConditionTimer(fleetNum, fleetName, finishTime);
                   ((IClientChannel)notifyProxy).Close();
               }
               catch (EndpointNotFoundException exp)
               {
                   Debug.WriteLine("UpdateConditionTimern:EndpointNotFoundException\n"
                       + exp.ToString());
               }
           });
        }

        #region Mutex操作関数

        const string mutexName = "KCBTimer existing flag";
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle,
           string lpName);
        const UInt32 MUTEX_ALL_ACCESS = 0x1F0001;
        const UInt32 SYNCHRONIZE = 0x00100000;
        const int ERROR_FILE_NOT_FOUND = 2;
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        #endregion

        /// <summary>
        /// WCFサーバの存在をmutexで確認する。Mutex.TryOpenExistingは.NET 4.5以降なので
        /// 自力でP/Invokeする
        /// </summary>
        /// <returns>存在してたらtrue</returns>
        public bool ExistWCFServer
        {
            get
            {
                if (string.IsNullOrEmpty(_serverHost))
                {
                    IntPtr hMutex;
                    if ((hMutex = OpenMutex(SYNCHRONIZE, false, mutexName)) != IntPtr.Zero)
                    {
                        //mutexが開けた
                        Debug.WriteLine("WCFサーバの存在を確認しました。");
                        CloseHandle(hMutex);
                        return true;
                    }

                    if (Marshal.GetLastWin32Error() == ERROR_FILE_NOT_FOUND)
                    {
                        //mutexが存在しない
                        Debug.WriteLine("WCFサーバは存在しません。");
                        return false;
                    }

                    Debug.WriteLine("TimerRPC.IsExistWCFServer:謎のエラー"
                        + Marshal.GetLastWin32Error().ToString());
                    return false;
                }
                else
                {
                    //Net.Tcpの時は存在していると仮定してパケット投げる
                    return true;
                }
            }
        }

        /// <summary>
        /// 同期用ミューテックスを取得
        /// </summary>
        public static bool WaitForWCFStartup()
        {
            var mutex = Mutex.OpenExisting(mutexName);

            ///3000ミリ秒待機
            bool bRet = mutex.WaitOne(3000);
            if (bRet)
            {
                //シグナルになっていた場合は解放して終了
                mutex.ReleaseMutex();
                return true;
            }
            return false;
        }

        /// <summary>
        /// タイマを終了させるコマンドををWCF RPCで呼び出す
        /// </summary>
        /// <param name="info"></param>
        /// <returns>失敗したらfalse</returns>
        public void ShutdownTimer()
        {
            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
           {
               try
               {
                    /* RPCが失敗するとproxy objectに失敗フラグが立つようで、
                     * 再度callすると以下の例外が飛ぶ。
                     * なので、proxy objectは毎回生成することにした。
                     * 
                     * CommunicationObjectFaultedException 
                     * 通信オブジェクト System.ServiceModel.Channels.ServiceChannel は、
                     * 状態が Faulted であるため通信に使用できません。
                     */
                   var notifyProxy = _notifyFactory.CreateChannel();
                   notifyProxy.ShutdownTimer();

                    /*明示的に閉じようとすると
                     *追加情報: パイプ パイプを閉じています。 (232, 0xe8) への書き込みエラーが発生しました。
                     *とでる
                     */
                    //                ((IClientChannel)notifyProxy).Close();
                }
               catch (EndpointNotFoundException exp)
               {
                   Debug.WriteLine("ShutdownTimer:EndpointNotFoundException\n"
                       + exp.ToString());
               }
           });

        }

        /// <summary>
        /// タイマの設定画面を表示させる
        /// </summary>
        /// <returns></returns>
        public void ShowTimerPreferenceDlg()
        {
            if (!ExistWCFServer)
                return;

            Task.Factory.StartNew(() =>
           {
               try
               {
                    /* RPCが失敗するとproxy objectに失敗フラグが立つようで、
                     * 再度callすると以下の例外が飛ぶ。
                     * なので、proxy objectは毎回生成することにした。
                     * 
                     * CommunicationObjectFaultedException 
                     * 通信オブジェクト System.ServiceModel.Channels.ServiceChannel は、
                     * 状態が Faulted であるため通信に使用できません。
                     */
                   var notifyProxy = _notifyFactory.CreateChannel();
                   notifyProxy.ShowPreferenceForm();
                   ((IClientChannel)notifyProxy).Close();
               }
               catch (EndpointNotFoundException exp)
               {
                   Debug.WriteLine("ShowPreferenceForm:EndpointNotFoundException\n"
                       + exp.ToString());
               }
           });
        }

        /// <summary>
        /// 戦闘終了を通知する
        /// </summary>
        /// <returns></returns>
        public void RPCFinishBattle(string type)
        {
            if (!Properties.Settings.Default.NotifyFinishBattle)
                return;

            if (!ExistWCFServer)
                return;

            Debug.WriteLine("終戦通知:" + type);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    /* RPCが失敗するとproxy objectに失敗フラグが立つようで、
                     * 再度callすると以下の例外が飛ぶ。
                     * なので、proxy objectは毎回生成することにした。
                     * 
                     * CommunicationObjectFaultedException 
                     * 通信オブジェクト System.ServiceModel.Channels.ServiceChannel は、
                     * 状態が Faulted であるため通信に使用できません。
                     */
                    var notifyProxy = _notifyFactory.CreateChannel();
                    notifyProxy.FinishBattle(type);
                    ((IClientChannel)notifyProxy).Close();
                }
                catch (EndpointNotFoundException exp)
                {
                    Debug.WriteLine("RPCFinishBattle:EndpointNotFoundException\n"
                        + exp.ToString());
                }
            });
        }

    }
}
