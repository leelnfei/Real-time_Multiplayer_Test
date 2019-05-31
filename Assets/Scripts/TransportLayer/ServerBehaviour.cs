﻿using System.Net;
using UnityEngine;

using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine.Assertions;
using Unity.Networking.Transport.Utilities;

public class ServerBehaviour : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    private NativeList<NetworkConnection> m_Connections;
    
    NetworkPipeline networkPipeline;
    
    void Start ()
	{
        NetworkEndPoint networkEndpoint = NetworkEndPoint.AnyIpv4;
        networkEndpoint.Port = 9000;

        m_Driver = new UdpNetworkDriver(new ReliableUtility.Parameters { WindowSize = 32 });
        if (m_Driver.Bind(networkEndpoint) != 0)
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        //This must use the same pipeline(s) as the client(s)
        networkPipeline = m_Driver.CreatePipeline(
            typeof(ReliableSequencedPipelineStage),
            typeof(UnreliableSequencedPipelineStage
        ));
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }
    
    void Update ()
	{
        m_Driver.ScheduleUpdate().Complete();
        
        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        // AcceptNewConnections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection))
        {
            m_Connections.Add(c);
            Debug.Log("Accepted a connection");
        }
        
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
                Assert.IsTrue(true);
            
            NetworkEvent.Type cmd;
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) !=
                   NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    var readerCtx = default(DataStreamReader.Context);
                    uint number = stream.ReadUInt(ref readerCtx);
                    
                    Debug.Log("Got " + number + " from the Client adding + 2 to it.");
                    number +=2;

                    using (var writer = new DataStreamWriter(4, Allocator.Temp))
                    {
                        writer.Write(number);
                        m_Driver.Send(networkPipeline, m_Connections[i], writer);
                    }
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    m_Connections[i] = default(NetworkConnection);
                }
            }
        }
    }
}