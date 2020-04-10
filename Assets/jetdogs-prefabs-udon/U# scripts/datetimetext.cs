
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;

public class datetimetext : UdonSharpBehaviour
{
    public Text displaytext;
    [Tooltip("datetime timezone ID")]
    public string timezoneID = "";
    private string lastupdate = "";
    private TimeZoneInfo zone;


    private void Start()
    {
        if(timezoneID == null || timezoneID == "")
        {
            zone = TimeZoneInfo.Local;
        }
        else
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timezoneID);
        }
    }

    private void Update()
    {
        if(DateTime.UtcNow.ToString("hh:mm:ss") != lastupdate)
        {
            DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);

            if (timezoneID == null || timezoneID == "")
            {
                displaytext.text = time.ToString("MM/dd/yyyy hh:mm:ss");
            }
            else
            {
                displaytext.text = timezoneID + "\n" + time.ToString("MM/dd/yyyy hh:mm:ss");
            }
            

            lastupdate = DateTime.UtcNow.ToString("hh:mm:ss");

        }
        
    }
}
