﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Newtonsoft.Json;
using TriggerService;

namespace TriggerServiceRole
{
    public class TriggerConfig
    {
        private CloudStorageAccount _account;
        private string _error;

        private CloudBlob _blob; // where map is persisted.

        public TriggerConfig()
        {
            // !!! Get from config
            try
            {
                var val = RoleEnvironment.GetConfigurationSettingValue("MainStorage");
                _account = CloudStorageAccount.Parse(val);
            }
            catch (Exception e)
            {
                _error = "Couldn't initialize storage:" + e.Message;
            }
        }

        public string GetConfigInfo()
        {
            if (_error != null)
            {
                return _error;
            }
            var account = this.GetAccount();
            return account.Credentials.AccountName;

        }

        public CloudStorageAccount GetAccount()
        {
            return _account;
        }

        public CloudQueue GetDeltaQueue()
        {
            CloudQueueClient client = _account.CreateCloudQueueClient();
            var q = client.GetQueueReference("triggerdeltaq");
            q.CreateIfNotExist();
            return q;
        }

        private CloudBlob GetTriggerMapBlob()
        {
            if (_blob == null)
            {
                CloudBlobClient client = this.GetAccount().CreateCloudBlobClient();
                CloudBlobContainer container = client.GetContainerReference("triggerservice");
                container.CreateIfNotExist();
                CloudBlob blob = container.GetBlobReference("store.txt");
                _blob = blob;
            }
            return _blob;
        }

        // Called under a lock. 
        public void Save(ITriggerMap map)
        {
            string content = JsonConvert.SerializeObject(map, JsonCustom.SerializerSettings);
            GetTriggerMapBlob().UploadText(content);
        }

        public ITriggerMap Load()
        {
            string content = GetBlobContents(GetTriggerMapBlob());
            if (content != null)
            {
                var result = JsonConvert.DeserializeObject<TriggerMap>(content, JsonCustom.SerializerSettings);
                return result;
            }
            else
            {
                return new TriggerMap(); // empty 
            }
        }

        [DebuggerNonUserCode]
        private static string GetBlobContents(CloudBlob blob)
        {
            try
            {
                string content = blob.DownloadText();
                return content;
            }
            catch
            {
                return null; // not found
            }
        }    
    }
}