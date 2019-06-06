﻿using protocol;
using System;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
//帧指令接收器，用于存储从服务器/单机 时发送来的帧指令.FSC=FRAMESYNCCLIENT
public class FSC:Singleton<FSC>
{
    List<TurnFrames> frameCommand = new List<TurnFrames>();//指令序列.
    public void OnReceiveCommand(TurnFrames turn)
    {
        frameCommand.Add(turn);
    }

    public TurnFrames NextTurn(int turnIndex)
    {
        if (frameCommand.Count > turnIndex && turnIndex >= 0)
        {
            return frameCommand[turnIndex];
        }

        return null;
    }

    public void Reset()
    {
        frameCommand.Clear();
    }

    public void OnDisconnected()
    {
        Reset();
    }

    public List<FrameCommand> GetCommand(int frame)
    {
        int t = frame / FrameReplay.TurnMaxFrame;
        int f = frame % FrameReplay.TurnMaxFrame;
        return GetCommand(t, f);
    }

    List<FrameCommand> cmdCache = new List<FrameCommand>();
    protected List<FrameCommand> GetCommand(int turn, int frame)
    {
        cmdCache.Clear();
        for (int i = 0; i < frameCommand[turn].commands.Count; i++)
        {
            if (frameCommand[turn].commands[i].LogicFrame == frame)
            {
                cmdCache.Add(frameCommand[turn].commands[i]);
            }
        }
        return cmdCache;
    }
}

//帧指令发送器，用于把客户端的操作发送到服务器/或FrameClient FSS=FRAMESYNCSERVER
//存储客户端操作序列.
public class FSS:Singleton<FSS>
{
    List<TurnFrames> frameCommand = new List<TurnFrames>();
    public void OnDisconnected()
    {
        Reset();
    }

    public void Reset()
    {
        frameCommand.Clear();
    }

    
    public void SyncTurn()
    {
        if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
        {
            //联机时客户端并没有操作,可以不向服务器发送之间的帧指令。但是服务器会生成默认的空操作
            if (FrameReplay.Instance.TurnIndex >= frameCommand.Count)
            {
                return;
            }
            TurnFrames t = frameCommand[FrameReplay.Instance.TurnIndex];
            UdpClientProxy.Exec((int)MeteorMsg.MsgType.SyncCommand, t);
        }
        else
        {
            //如果是单机下，所有更新者都没有操作.生成默认的空操作，填充进来
            if (FrameReplay.Instance.TurnIndex >= frameCommand.Count)
            {
                TurnFrames tAdd = new TurnFrames();
                tAdd.turnIndex = (uint)FrameReplay.Instance.TurnIndex;
                frameCommand.Add(tAdd);
            }
            TurnFrames t = frameCommand[FrameReplay.Instance.TurnIndex];
            FSC.Instance.OnReceiveCommand(t);
        }
    }

    //在指定帧推入数据.
    public void Command(int frame, MeteorMsg.MsgType message, MeteorMsg.Command command)
    {
        PushAction(frame, message, command);
    }

    //在当前帧推入指令-鼠标相对上次的偏移，会导致角色绕Y轴旋转
    public void PushMouseDelta(int playerId, float x, float y)
    {
        TurnFrames t = GetTurnByFrame(FrameReplay.Instance.NextFrame);
        FrameCommand cmd = new FrameCommand();
        cmd.command = MeteorMsg.Command.JoyStickMove;
        //cmd.command = MeteorMsg.Command.MouseDelta;
        cmd.LogicFrame = (uint)FrameReplay.Instance.NextFrame % FrameReplay.TurnMaxFrame;
        cmd.playerId = (uint)playerId;
        System.IO.MemoryStream ms = new System.IO.MemoryStream();
        Vector2_ vec = new Vector2_();
        vec.x = (int)x * 1000;
        vec.y = (int)y * 1000;
        ProtoBuf.Serializer.Serialize<Vector2_>(ms, vec);
        cmd.data = ms.ToArray();
        t.commands.Add(cmd);
    }

    public void Push(int action)
    {
        PushAction(FrameReplay.Instance.NextFrame, MeteorMsg.MsgType.SyncCommand, (MeteorMsg.Command)action);
    }

    public void PushAction(int frame, MeteorMsg.MsgType message, MeteorMsg.Command command)
    {
        TurnFrames t = GetTurnByFrame(frame);
        FrameCommand cmd = new FrameCommand();
        cmd.command = command;
        cmd.LogicFrame = (uint)frame;
        cmd.playerId = (uint)NetWorkBattle.Instance.PlayerId;

        t.commands.Add(cmd);
    }

    public TurnFrames GetTurnByFrame(int frame)
    {
        int TurnIndex = frame / FrameReplay.TurnMaxFrame;
        if (frameCommand.Count <= TurnIndex + 1)
        {
            int min = frameCommand.Count;
            for (int i = min; i < TurnIndex + 1; i++)
            {
                TurnFrames t = new TurnFrames();
                t.turnIndex = (uint)i;
                frameCommand.Add(t);
            }
        }
        return frameCommand[TurnIndex];
    }
}

//帧重播器.
public class FrameReplay : MonoBehaviour {
    public static FrameReplay Instance;
    public const int TurnMaxFrame = 5;//每个Turn5帧
    //角色更新顺序是由playerId由小到大跑.
    //场景物件顺序由物件ID由小到大跑.
    //private Stopwatch gameTurnSW;
    public bool TurnStarted;
    //返回下一帧,单机插入指令，都需要在下一帧插入
    public int NextFrame
    {
        get
        {
            return LogicFrameIndex + 1;
        }
    }
    public int LogicFrameIndex = 0;
    private int AccumilatedTime = 0;
    public static float deltaTime = 20.0f / 1000.0f;
    public int LogicFrameLength = 20;
    public int TurnIndex = 0;
    TurnFrames nowTurn;//当前的Turn


    public static event Action UpdateEvent;
    public static event Action LateUpdateEvent;

    public static void InvokeLockUpdate()
    {
        if (UpdateEvent != null)
            UpdateEvent();
    }

    public static void InvokeLateUpdate()
    {
        if (LateUpdateEvent != null)
            LateUpdateEvent();
    }
    /// <summary>
    /// 包括所有动态物体
    /// 所有需要使用网络时间驱动的游戏对象.需要实现接口IHasGameFrame，由该组件按网络时间，顺序执行每个对象的更新.
    /// </summary>

    //当场景以及物件全部加载完成，重新开局时
    public void OnBattleStart()
    {
        if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
        {
            //什么都不干，等从服务器拉取到帧后播放即可.
        }
        else
        {
            //发送同步随机种子事件
            //FSS.Instance.Command(1, MeteorMsg.MsgType.SyncCommand, MeteorMsg.Command.SyncRandomSeed);
            //发送创建主角色事件.
            //FSS.Instance.Command(2, MeteorMsg.MsgType.SyncCommand, MeteorMsg.Command.CreatePlayer);
            //向客户端发送首包.
            FSS.Instance.SyncTurn();
        }
        TurnIndex = 0;
        nowTurn = null;
        TurnStarted = true;
    }

    private void Awake()
    {
        Instance = this;
        TcpClientProxy.Init();
        UdpClientProxy.Init();
    }

    public void OnBattleFinished()
    {
        TurnStarted = false;
        AccumilatedTime = 0;
        TurnIndex = 0;
        LogicFrameIndex = 0;
        FSS.Instance.Reset();
        FSC.Instance.Reset();
    }

    public void OnDisconnected()
    {
        FSC.Instance.OnDisconnected();
        FSS.Instance.OnDisconnected();
        //如果重新开始,那么所有指令需要全部清除
        TurnStarted = false;
        TurnIndex = 0;
        LogicFrameIndex = 0;
        AccumilatedTime = 0;
    }

    //called once per unity frame
    public void Update()
    {
        ProtoHandler.Update();
        if (!TurnStarted)
        {
            if (OnUpdates != null)
                OnUpdates();
            return;
        }
        if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
        {
            //Basically same logic as FixedUpdate, but we can scale it by adjusting FrameLength
            AccumilatedTime = AccumilatedTime + Convert.ToInt32((Time.deltaTime * 1000)); //convert sec to milliseconds
                                                                                          //in case the FPS is too slow, we may need to update the game multiple times a frame
            
            while (AccumilatedTime > LogicFrameLength)
            {
                UdpClientProxy.Update();
                LogicFrame();
                //Debug.LogError("logicframe:" + LogicFrameIndex);
                AccumilatedTime = AccumilatedTime - LogicFrameLength;
            }
        }
        else
        {
            FrameReplay.deltaTime = Time.deltaTime;
            LogicFrame();
        }
    }

    public delegate void OnUpdate();
    public event OnUpdate OnUpdates;//在战斗还未开始时
    //取得对应逻辑帧的数据
    List<FrameCommand> cacheActions = new List<FrameCommand>();
    List<FrameCommand> GetAction(List<FrameCommand> acts, int logicF)
    {
        cacheActions.Clear();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].LogicFrame == logicF)
                cacheActions.Add(acts[i]);
        }
        return cacheActions;
    }

    private void LogicFrame()
    {
        //得到当前逻辑帧数据，对普通事件数据，调用对应的事件函数，对按键，在更新每个对象使，应用到每个对象上.
        if (nowTurn == null)
        {
            //等待从服务器收到接下来一个turn的信息.
            if (FSC.Instance.NextTurn(TurnIndex) == null)
                return;
            nowTurn = FSC.Instance.NextTurn(TurnIndex);
        }
        else
        {

        }

        List<FrameCommand> actions = GetAction(nowTurn.commands, LogicFrameIndex);
        //gameTurnSW.Start();

        //update game
        //SceneManager.Manager.TwoDPhysics.Update(GameFramesPerSecond);

        //Log.WriteError(string.Format("Turn:{0}, LogicFrame:{1}", nowTurn.turnIndex, LogicFrameIndex));//从这里开始，播放逻辑帧，在取得自己进入场景消息帧时，初始化主角
        for (int i = 0; i < actions.Count; i++)
        {
            switch (actions[i].command)
            {
                case MeteorMsg.Command.SyncRandomSeed:
                    SyncInitData seed = ProtoBuf.Serializer.Deserialize<SyncInitData>(new System.IO.MemoryStream(actions[i].data));
                    UnityEngine.Random.InitState((int)seed.randomSeed);
                    break;
                case MeteorMsg.Command.SpawnPlayer:
                    System.IO.MemoryStream ms = new System.IO.MemoryStream(actions[i].data);
                    PlayerEventData evt = ProtoBuf.Serializer.Deserialize<PlayerEventData>(ms);
                    GameBattleEx.Instance.OnCreateNetPlayer(evt);
                    break;
            }
        }
        if (UpdateEvent != null)
            UpdateEvent();
        if (LateUpdateEvent != null)
            LateUpdateEvent();
        LogicFrameIndex++;
        //当逻辑帧为 Turn序号
        if (LogicFrameIndex == (TurnIndex + 1) * TurnMaxFrame)
        {
            nowTurn = null;
            FSS.Instance.SyncTurn();
            TurnIndex++;
        }
    }
}