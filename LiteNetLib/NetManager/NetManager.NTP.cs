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
    public partial class NetManager
    {
        /// <summary>
        /// Enable nat punch messages
        /// </summary>
        public bool NatPunchEnabled = false;

        private readonly INtpEventListener _ntpEventListener;
        private readonly Dictionary<IPEndPoint, NtpRequest> _ntpRequests = new Dictionary<IPEndPoint, NtpRequest>(new IPEndPointComparer());

        private void ProcessNtpRequests(int elapsedMilliseconds)
        {
            List<IPEndPoint> requestsToRemove = null;
            foreach (var ntpRequest in _ntpRequests)
            {
                ntpRequest.Value.Send(_udpSocketv4, elapsedMilliseconds);
                if(ntpRequest.Value.NeedToKill)
                {
                    if (requestsToRemove == null)
                        requestsToRemove = new List<IPEndPoint>();
                    requestsToRemove.Add(ntpRequest.Key);
                }
            }

            if (requestsToRemove != null)
            {
                foreach (var ipEndPoint in requestsToRemove)
                {
                    _ntpRequests.Remove(ipEndPoint);
                }
            }
        }

        /// <summary>
        /// Create the requests for NTP server
        /// </summary>
        /// <param name="endPoint">NTP Server address.</param>
        public void CreateNtpRequest(IPEndPoint endPoint)
        {
            _ntpRequests.Add(endPoint, new NtpRequest(endPoint));
        }

        /// <summary>
        /// Create the requests for NTP server
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        /// <param name="port">port</param>
        public void CreateNtpRequest(string ntpServerAddress, int port)
        {
            IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, port);
            _ntpRequests.Add(endPoint, new NtpRequest(endPoint));
        }

        /// <summary>
        /// Create the requests for NTP server (default port)
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        public void CreateNtpRequest(string ntpServerAddress)
        {
            IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, NtpRequest.DefaultPort);
            _ntpRequests.Add(endPoint, new NtpRequest(endPoint));
        }
    }
}
