using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib.Layers;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal sealed class NetEvent
    {
        public NetEvent Next;

        public enum EType
        {
            Connect,
            Disconnect,
            Receive,
            ReceiveUnconnected,
            Error,
            ConnectionLatencyUpdated,
            Broadcast,
            ConnectionRequest,
            MessageDelivered,
            PeerAddressChanged
        }
        public EType Type;

        public NetPeer Peer;
        public IPEndPoint RemoteEndPoint;
        public object UserData;
        public int Latency;
        public SocketError ErrorCode;
        public DisconnectReason DisconnectReason;
        public ConnectionRequest ConnectionRequest;
        public DeliveryMethod DeliveryMethod;
        public byte ChannelNumber;
        public readonly NetPacketReader DataReader;

        public NetEvent(NetManager manager)
        {
            DataReader = new NetPacketReader(manager, this);
        }
    }

    public partial class NetManager
    {
        private Queue<NetEvent> _netEventsProduceQueue = new Queue<NetEvent>();
        private Queue<NetEvent> _netEventsConsumeQueue = new Queue<NetEvent>();

        private NetEvent _netEventPoolHead;

        private void CreateEvent(
            NetEvent.EType type,
            NetPeer peer = null,
            IPEndPoint remoteEndPoint = null,
            SocketError errorCode = 0,
            int latency = 0,
            DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed,
            ConnectionRequest connectionRequest = null,
            DeliveryMethod deliveryMethod = DeliveryMethod.Unreliable,
            byte channelNumber = 0,
            NetPacket readerSource = null,
            object userData = null)
        {
            NetEvent evt;
            bool unsyncEvent = UnsyncedEvents;

            if (type == NetEvent.EType.Connect)
                Interlocked.Increment(ref _connectedPeersCount);
            else if (type == NetEvent.EType.MessageDelivered)
                unsyncEvent = UnsyncedDeliveryEvent;

            lock(_eventLock)
            {
                evt = _netEventPoolHead;
                if (evt == null)
                    evt = new NetEvent(this);
                else
                    _netEventPoolHead = evt.Next;
            }

            evt.Type = type;
            evt.DataReader.SetSource(readerSource, readerSource?.GetHeaderSize() ?? 0);
            evt.Peer = peer;
            evt.RemoteEndPoint = remoteEndPoint;
            evt.Latency = latency;
            evt.ErrorCode = errorCode;
            evt.DisconnectReason = disconnectReason;
            evt.ConnectionRequest = connectionRequest;
            evt.DeliveryMethod = deliveryMethod;
            evt.ChannelNumber = channelNumber;
            evt.UserData = userData;

            if (unsyncEvent || _manualMode)
            {
                ProcessEvent(evt);
            }
            else
            {
                lock(_netEventsProduceQueue)
                    _netEventsProduceQueue.Enqueue(evt);
            }
        }

        internal void RecycleEvent(NetEvent evt)
        {
            evt.Peer = null;
            evt.ErrorCode = 0;
            evt.RemoteEndPoint = null;
            evt.ConnectionRequest = null;
            lock(_eventLock)
            {
                evt.Next = _netEventPoolHead;
                _netEventPoolHead = evt;
            }
        }

        /// <summary>
        /// Receive all pending events. Call this in game update code
        /// In Manual mode it will call also socket Receive (which can be slow)
        /// </summary>
        public void PollEvents()
        {
            if (_manualMode)
            {
                if (_udpSocketv4 != null)
                    ManualReceive(_udpSocketv4, _bufferEndPointv4);
                if (_udpSocketv6 != null && _udpSocketv6 != _udpSocketv4)
                    ManualReceive(_udpSocketv6, _bufferEndPointv6);
                ProcessDelayedPackets();
                return;
            }
            if (UnsyncedEvents)
                return;
            lock (_netEventsProduceQueue)
            {
                (_netEventsConsumeQueue, _netEventsProduceQueue) = (_netEventsProduceQueue, _netEventsConsumeQueue);
            }

            while(_netEventsConsumeQueue.Count > 0)
                ProcessEvent(_netEventsConsumeQueue.Dequeue());
        }
    }
}
