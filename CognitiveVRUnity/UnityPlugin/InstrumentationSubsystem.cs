﻿using System.Collections.Generic;

namespace CognitiveVR
{
    /*public class TransactionSnapshot
    {
        public string category;
        public Dictionary<string, object> properties;
        public float[] position = new float[3] { 0, 0, 0 };
        public double timestamp;

        public TransactionSnapshot(string cat, Dictionary<string, object> props, float[] pos, double time)
        {
            category = cat;
            properties = props;
            position = pos;
            timestamp = time;
        }
    }*/

    public static class InstrumentationSubsystem
    {
        //public static List<TransactionSnapshot> CachedTransactions = new List<TransactionSnapshot>();
        //string builder for 'data'. put into container when 'packaged', then 'sent'
        public static System.Text.StringBuilder TransactionBuilder = new System.Text.StringBuilder();

        //used for unique identifier for sceneexplorer file names
        private static int partCount = 1;

        public static void SetMaxTransactions(int max)
        {
            EventDepot.maxCachedTransactions = max;
        }

        public static void SendCachedTransactions()
        {
            EventDepot.SendCachedTransactions();
        }

        public static void init()
        {
            Util.cacheCurrencyInfo();
        }

        /// <summary>
        /// call this when threshold for transactions is reached
        /// format transactions to be sent as the body of a webrequest
        /// this can be called manually, but it is automatically called when
        /// </summary>
        private static string PackageData()
        {
            //PackageData(CoreSubsystem.UniqueID, CoreSubsystem.SessionTimeStamp, CoreSubsystem.SessionID);
            string userid = CoreSubsystem.UniqueID;
            double timestamp = CoreSubsystem.SessionTimeStamp;
            string sessionId = CoreSubsystem.SessionID;

            CognitiveVR.Util.logDebug("package transaction event data");
            //when thresholds are reached, etc
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("{");

            //header
            builder.Append(JsonUtil.SetString("userid", userid));
            builder.Append(",");

            builder.Append(JsonUtil.SetObject("timestamp", timestamp));
            builder.Append(",");
            builder.Append(JsonUtil.SetString("sessionid", sessionId));
            builder.Append(",");
            builder.Append(JsonUtil.SetObject("part", partCount));
            partCount++;
            builder.Append(",");

            //events
            builder.Append("\"data\":[");

            builder.Append(TransactionBuilder.ToString());

            builder.Remove(builder.Length-1, 1); //remove the last comma
            builder.Append("]");

            builder.Append("}");

            //clear the transaction builder
            TransactionBuilder = new System.Text.StringBuilder();
            return builder.ToString();
        }

        public delegate void EventsThresholdHandler(string packagedTransactionEvents); //package data
        /// <summary>
        /// this is called when data thresholds are reached/hmd is removed and just before level is loaded and application quit
        /// puts data into reasonable sized byte[] to be uploaded to the server
        /// </summary>
        public static event EventsThresholdHandler EventDataThresholdEvent;
        public static void OnEventDataThresholdEvent()
        {
            if (EventDataThresholdEvent != null)
            {
                string s = PackageData();
                EventDataThresholdEvent(s);
            }
        }

        private static void SetTransaction(string category, Dictionary<string, object>  properties, float[] position, double timestamp)
        {
            TransactionBuilder.Append("{");
            TransactionBuilder.Append(JsonUtil.SetString("name", category));
            TransactionBuilder.Append(",");
            TransactionBuilder.Append(JsonUtil.SetObject("time", timestamp));
            TransactionBuilder.Append(",");
            TransactionBuilder.Append(JsonUtil.SetVector("point", position));


            if (properties != null && properties.Keys.Count > 0)
            {
                TransactionBuilder.Append(",");
                TransactionBuilder.Append("\"properties\":{");
                foreach (var v in properties)
                {
                    if (v.Value.GetType() == typeof(string))
                    {
                        TransactionBuilder.Append(JsonUtil.SetString(v.Key, (string)v.Value));
                    }
                    else
                    {
                        TransactionBuilder.Append(JsonUtil.SetObject(v.Key, v.Value));
                    }
                    TransactionBuilder.Append(",");
                }
                TransactionBuilder.Remove(TransactionBuilder.Length - 1, 1); //remove last comma
                TransactionBuilder.Append("}"); //close properties object
            }

            TransactionBuilder.Append("}"); //close transaction object
            TransactionBuilder.Append(",");
        }

        public static void beginTransaction(string category, string timeoutMode, double timeout, string transactionId, Dictionary<string, object> properties, float[] position)
        {
            SetTransaction(category, properties, position, Util.Timestamp());

            new CoreSubsystem.DataPointBuilder("datacollector_beginTransaction")
            .setArg(category)
            .setArg(timeoutMode)
            .setArg(timeout)
            .setArg(transactionId)
            .setArg(properties)
            .send();
        }

        public static void updateTransaction(string category, int progress, string transactionId, Dictionary<string, object> properties)
        {
            new CoreSubsystem.DataPointBuilder("datacollector_updateTransaction")
            .setArg(category)
            .setArg(progress)
            .setArg(transactionId)
            .setArg(properties)
            .send();
        }

        public static void endTransaction(string category, string result, string transactionId, Dictionary<string, object> properties, float[] position)
        {
            SetTransaction(category, properties, position, Util.Timestamp());

            new CoreSubsystem.DataPointBuilder("datacollector_endTransaction")
            .setArg(category)
            .setArg(result)
            .setArg(transactionId)
            .setArg(properties)
            .send();
        }

        /**
         * Updates state information about the user.
         *
         * @param properties A key-value object representing the user state we want to update. This can be a nested object structure.
         */
        public static void updateUserState(IDictionary<string, object> properties)
        {
            new CoreSubsystem.DataPointBuilder("datacollector_updateUserState").setArg(properties).send();
        }

        /**
         * Updates state information about a device.
         *
         * @param properties A key-value object representing the device state we want to update. This can be a nested object structure.
         */
        public static void updateDeviceState(IDictionary<string, object> properties)
        {
            new CoreSubsystem.DataPointBuilder("datacollector_updateDeviceState").setArg(properties).send();
        }

        /**
         * Update a collection balance for a user.
         *
         * @param name                  The name of the collection.
         * @param balance               The new balance of the collection.
         * @param balanceModification   The change in balance being recorded.  To reduce the balance, specify a negative number.
         * @param isCurrency            Whether or not this collection represents a currency in the application.
         */
        public static void updateCollection(string name, double balance, double balanceModification, bool isCurrency)
        {
            new CoreSubsystem.DataPointBuilder("datacollector_updateCollection")
            .setArg(name)
            .setArg(balance)
            .setArg(balanceModification)
            .setArg(isCurrency)
            .send();
        }
    }
}
