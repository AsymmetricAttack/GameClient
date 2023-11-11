using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

using Debug = System.Diagnostics.Debug;

public class NetworkManager : MonoBehaviour
{
    public SocketIOUnity socket;
    public String m_DeviceType;
    public String m_UriAddress = "http://192.168.1.107:11100";
    public Text ReceivedText;
    // Start is called before the first frame update
    void Start()
    {
        //TODO: check the Uri if Valid.
        var uri = new Uri(m_UriAddress);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string>
                {
                    {"token", "UNITY" }
                }
            ,
            EIO = 4
            ,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        ///// reserved socketio events
        socket.OnConnected += (sender, e) =>
        {
            UnityEngine.Debug.Log("socket.OnConnected");
        };
        socket.OnPing += (sender, e) =>
        {
            UnityEngine.Debug.Log("Ping");
        };
        socket.OnPong += (sender, e) =>
        {
            UnityEngine.Debug.Log("Pong: " + e.TotalMilliseconds);
        };
        socket.OnDisconnected += (sender, e) =>
        {
            UnityEngine.Debug.Log("disconnect: " + e);
        };
        socket.OnReconnectAttempt += (sender, e) =>
        {
            UnityEngine.Debug.Log($"{DateTime.Now} Reconnecting: attempt = {e}");
        };
        ////

        UnityEngine.Debug.Log("Connecting...");
        socket.Connect();
        ReceivedText.text = "";
        socket.OnAnyInUnityThread((name, response) =>
        {
            //String x = response.GetValue() + "\n";
            //var x = response.GetValue<String>();
            String x = response.ToString();
            ReceivedText.text += "Received On " + name + " : " + x + "\n";// + response.GetValue().GetRawText() + "\n";
        });
    }

    // Update is called once per frame
    void Update()
    {
        //Output the device type to the console window
        UnityEngine.Debug.Log("Device type : " + m_DeviceType);
        //Check if the device running this is a console
        if (SystemInfo.deviceType == DeviceType.Console)
        {
            //Change the text of the label
            m_DeviceType = "Console";
        }

        //Check if the device running this is a desktop
        if (SystemInfo.deviceType == DeviceType.Desktop)
        {
            m_DeviceType = "Desktop";
        }

        //Check if the device running this is a handheld
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            m_DeviceType = "Handheld";
        }

        //Check if the device running this is unknown
        if (SystemInfo.deviceType == DeviceType.Unknown)
        {
            m_DeviceType = "Unknown";
        }
    }
}
