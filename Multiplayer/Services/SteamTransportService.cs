﻿using System;
using Godot;
using Steamworks;
using Steamworks.Data;

public class SteamTransportService : ITransportService
{
    private SocketManager serverSocket;
    private ClientConnectionManager clientConnection;
    private ServerCallbacks serverCallbacks;
    
    private Action _updateMethod;

    public SteamTransportService()
    {
        serverCallbacks = new ServerCallbacks();
    }
    
    public void Update()
    {
        _updateMethod();
    }

    private void ServerUpdate()
    {
        serverSocket.Receive();
    }
    
    private void ClientUpdate()
    {
        clientConnection.Receive();
    }

    public bool CreateServer()
    {
        serverSocket = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(0);
        
        if (serverSocket == null)
        {
            GD.PrintErr("Failed to create Steam relay server.");
            return false;
        }
        
        _updateMethod = ServerUpdate;
        TransportManager.Instance.ExecuteProcessMethodStatus(true);
        serverSocket.Interface = serverCallbacks;
        return true;
    }

    public bool ConnectToServer(string serverId)
    {
        ulong steamIdValue = ulong.Parse(serverId);
        SteamId hostSteamId = new SteamId { Value = steamIdValue };
        
        clientConnection = SteamNetworkingSockets.ConnectRelay<ClientConnectionManager>(hostSteamId, 0);
        
        if (clientConnection == null)
        {
            return false;
        }
        
        _updateMethod = ClientUpdate;
        TransportManager.Instance.ExecuteProcessMethodStatus(true);

        return true;
    }
    
    public void Disconnect()
    {
        if (serverSocket != null)
        {
            serverSocket.Close();
            serverSocket = null;
        }
        
        if (clientConnection != null)
        {
            clientConnection.Close();
            clientConnection = null;
        }
    }
    
    public void SendPacketToClients(PacketTypes.MainType mainType, byte subType, byte[] data)
    {
        var packet = PacketFactory.CreatePacket(mainType, subType, 3, data);

        if (serverSocket != null)
        {
            foreach (var client in serverSocket.Connected)
            {
                client.SendMessage(packet, SendType.Unreliable);
            }
        }
    }

    public void SendPacketToServer(PacketTypes.MainType mainType, byte subType, byte[] data)
    {
        var packet = PacketFactory.CreatePacket(mainType, subType, 2, data);
        
        if (clientConnection != null)
        {
            clientConnection.Connection.SendMessage(packet, SendType.Unreliable);
        }
    }
}

public class ServerCallbacks : ISocketManager
{
    public void OnConnecting(Connection connection, ConnectionInfo data)
    {
        connection.Accept();
        GD.Print($"Server: Player {data.Identity} is attempting to connect...");
        
        byte[] welcomeData = BitConverter.GetBytes(data.Identity.SteamId.Value);
        connection.SendMessage(PacketFactory.CreatePacket(
            PacketTypes.MainType.Lobby,
            (byte)PacketTypes.LobbyType.PlayerJoin,
            3,
            welcomeData
        ));
    }

    public void OnConnected(Connection connection, ConnectionInfo data)
    {
        GD.Print($"Server: Player {data.Identity} has successfully connected!");
    }

    public void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        GD.Print($"Server: Player {data.Identity} has disconnected.");
    }

    public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum,
        long recvTime, int channel)
    {
        GD.Print("Server: Received message from player: " + identity);
        byte[] packetData = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(data, packetData, 0, size);

        var (header, payload) = PacketFactory.ParsePacket(packetData);
        GD.Print($"Server: Received packet - Main Type: {header.MainType}, Sub Type: {header.SubType} from Player {header.PlayerIndex}, Data : {PacketFactory.GetStringFromPacketData(payload)}");
    }
}

public class ClientConnectionManager : ConnectionManager
{
    public override void OnConnecting(ConnectionInfo info)
    {
        GD.Print("Client: Attempting to connect to server...");
    }

    public override void OnConnected(ConnectionInfo info)
    {
        GD.Print("Client: Successfully connected to server!");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        GD.Print("Client: Disconnected from server.");
        TransportManager.Instance.ExecuteProcessMethodStatus(false);
    }

    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        GD.Print("Server: Received message from server.");
        byte[] packetData = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(data, packetData, 0, size);

        var (header, payload) = PacketFactory.ParsePacket(packetData);
        GD.Print($"Server: Received packet - Main Type: {header.MainType}, Sub Type: {header.SubType} from Player {header.PlayerIndex}, Data : {PacketFactory.GetStringFromPacketData(payload)}");
    }
}