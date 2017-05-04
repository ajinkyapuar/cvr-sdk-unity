﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CognitiveVR;
using CognitiveVR.Plugins;
using System.Text;

namespace CognitiveVR
{
    public static class SensorRecorder
    {
        private static int jsonPart = 1;
        private static Dictionary<string, List<string>> CachedSnapshots = new Dictionary<string, List<string>>();
        private static int currentSensorSnapshots = 0;

        static SensorRecorder()
        {
            CognitiveVR_Manager.SendDataEvent += SendData;
        }

        public static void RecordDataPoint(string category, float value)
        {
            if (CachedSnapshots.ContainsKey(category))
            {
                CachedSnapshots[category].Add(GetSensorDataToString(Util.Timestamp(), value));
            }
            else
            {
                CachedSnapshots.Add(category, new List<string>());
                CachedSnapshots[category].Add(GetSensorDataToString(Util.Timestamp(), value));
            }
            currentSensorSnapshots++;
            if (currentSensorSnapshots >= CognitiveVR_Preferences.Instance.SensorSnapshotCount)
            {
                SendData();
            }
        }

        public static void SendData()
        {
            if (CachedSnapshots.Keys.Count <= 0) { CognitiveVR.Util.logDebug("Sensor.SendData found no data"); return; }

            var sceneSettings = CognitiveVR_Preferences.Instance.FindScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (sceneSettings == null) { CognitiveVR.Util.logDebug("Sensor.SendData found no SceneKeySettings"); return; }

            StringBuilder sb = new StringBuilder(1024);
            sb.Append("{");
            sb.Append(JsonUtil.SetString("name", Core.UniqueID));
            sb.Append(",");
            sb.Append(JsonUtil.SetString("sessionid", CognitiveVR_Preferences.SessionID));
            sb.Append(",");
            sb.Append(JsonUtil.SetObject("timestamp", CognitiveVR_Preferences.TimeStamp));
            sb.Append(",");
            sb.Append(JsonUtil.SetObject("part", jsonPart));
            sb.Append(",");
            jsonPart++;

            sb.Append("\"data\":[");
            foreach (var k in CachedSnapshots.Keys)
            {
                sb.Append("{");
                sb.Append(JsonUtil.SetString("name", k));
                sb.Append(",");
                sb.Append("\"data\":[");
                foreach (var v in CachedSnapshots[k])
                {
                    sb.Append(v);
                    sb.Append(",");
                }
                if (CachedSnapshots.Values.Count > 0)
                    sb.Remove(sb.Length - 1, 1); //remove last comma from data array
                sb.Append("]");
                sb.Append("}");
                sb.Append(",");
            }
            if (CachedSnapshots.Keys.Count > 0)
            {
                sb.Remove(sb.Length - 1, 1); //remove last comma from sensor object
            }
            sb.Append("]}");

            CachedSnapshots.Clear();
            currentSensorSnapshots = 0;

            string url = "https://sceneexplorer.com/api/sensors/" + sceneSettings.SceneId;
            byte[] outBytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
            CognitiveVR_Manager.Instance.StartCoroutine(CognitiveVR_Manager.Instance.PostJsonRequest(outBytes, url));
        }

        #region json

        //put this into the list of saved sensor data based on the name of the sensor
        private static string GetSensorDataToString(double timestamp, double sensorvalue)
        {
            StringBuilder sb = new StringBuilder(1024);

            sb.Append("[");
            sb.Append(timestamp);
            sb.Append(",");
            sb.Append(sensorvalue);
            sb.Append("]");

            return sb.ToString();
        }

        #endregion
    }
}