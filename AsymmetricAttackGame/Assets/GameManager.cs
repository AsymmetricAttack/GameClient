using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool isWebPlayer;
    public GameObject webPlayer;
    public GameObject hmdPlayer;
    public string m_DeviceType;

    // Start is called before the first frame update
    void Start()
    {
        GetType();
        if(m_DeviceType == "Desktop" || m_DeviceType == "Console") {
            isWebPlayer = true;
        }
        else {
            isWebPlayer = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GetType() {
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
