// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using OPCUAFunctions.Entities;

namespace OPCUAFunctions
{
    public class ProcessOPCPublisherEventsToADT
    {
        private static HttpClient _httpClient = new HttpClient();
        private static string _adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static string _logLevelString = Environment.GetEnvironmentVariable("LOG_LEVEL");
        private static Int32 _logLevel = 100;
        private static string _mappingUrl = Environment.GetEnvironmentVariable("JSON_MAPPINGFILE_URL");
        private static List<NodeTwinMap> _nodeTwinMapList = null;

        [FunctionName("ProcessOPCPublisherEventsToADT")]
        public void Run([EventGridTrigger] EventGridEvent message, ILogger log)
        {
            //if (_logMe) log.LogInformation(message.Data.ToString());

            _logLevel = isNumeric(_logLevelString) ? Convert.ToInt32(_logLevelString) : 100;

            if (message == null || message.Data == null)
            {
                log.LogError("Message data is empty, exiting run");
                return;
            }

            //if (_logMe) log.LogInformation(message.Data.ToString());

            if (_adtServiceUrl == null)
            {
                log.LogError("Application setting \"ADT_SERVICE_URL\" not set, exiting run");
                return;
            }

            JObject msg = (JObject)JsonConvert.DeserializeObject(message.Data.ToString());
            string body = msg["body"].ToString();

            // handle base64decode
            // if in local debug mode, use a hardcoded decoded string
            body = this.base64DecodeString(body);

            if (_logLevel >= 300) log.LogInformation($"body:\r\n{body}");

            // take body of message and build a strongly typed list of nodes
            List<TwinDto> dto = new List<TwinDto>();
            List<string> twins = new List<string>();

            List<Node> nodes = JsonConvert.DeserializeObject<List<Node>>(body);
            dto = new List<TwinDto>();

            // get node to twin mapping
            IList<NodeTwinMap> mapping = this.GetNodeToTwinMapping(_mappingUrl, log);

            // if mapping object is empty, something went wrong
            if (mapping == null)
            {
                log.LogError($"Mapping file did not load from url or cache. Please check the mapping file url.");
                return;
            }

            // build a simplified format of our list
            // this will make it easier to sort, order, clean
            foreach (Node node in nodes)
            {
                // get node id
                string nodeId = this.getValueFromSplit('=', node.NodeId, 1);

                // get mapping information by the node
                NodeTwinMap map = mapping.Where(x => x.NodeId == nodeId).Single<NodeTwinMap>();

                dto.Add(
                    new TwinDto()
                    {
                        NodeId = nodeId,
                        TwinId = map.TwinId,
                        ModelId = map.ModelId,
                        PropertyName = map.Property,
                        PropertyValue = node.Value.Value,
                        TimeStamp = Convert.ToDateTime(node.Value.SourceTimeStamp)
                    }
                );
            }

            if (_logLevel >= 300) log.LogInformation($"dto:\r\n{dto.ToString()}");

            // check to make sure the dto has values
            if (dto == null || dto.Count == 0)
            {
                log.LogError($"Empty list of twins, exiting run");
                return;
            }

            // get a distinct list of twins
            twins = dto.Select(x => x.TwinId).Distinct().ToList();

            DigitalTwinsClient client;
            DefaultAzureCredential credentials;

            // Authenticate with Digital Twins
            credentials = new DefaultAzureCredential();
            client = new DigitalTwinsClient(new Uri(_adtServiceUrl), credentials, new DigitalTwinsClientOptions { Transport = new HttpClientTransport(_httpClient) });

            JsonPatchDocument updateTwinData;

            // loop through each twin in the list
            foreach (string twin in twins)
            {
                // create new patch document
                updateTwinData = new JsonPatchDocument();

                // get properties for the twin
                // this lets us get all of our properties per twin
                List<TwinDto> items = dto.Where(x => x.TwinId == twin).ToList<TwinDto>();
                string modelId = items.Select(x => x.ModelId).FirstOrDefault<string>();

                // loop through each property and build patch document
                foreach (TwinDto item in items)
                {
                    if (isNumeric(item.PropertyValue))
                    {
                        updateTwinData.AppendAdd($"/{item.PropertyName}", double.Parse(item.PropertyValue, CultureInfo.InvariantCulture.NumberFormat));
                    }
                    else
                    {
                        updateTwinData.AppendAdd($"/{item.PropertyName}", item.PropertyValue);
                    }
                }

                if (_logLevel >= 200) log.LogInformation($"patch document for {twin}...\r\n{updateTwinData}");

                try
                {
                    // check to see if twin already exists or not, if not, we should create it before doing a patch
                    BasicDigitalTwin dtid = client.GetDigitalTwin<BasicDigitalTwin>(twin);
                }
                catch (Exception ex)
                {
                    log.LogInformation($"'{twin}' does not exist ({ex.Message}). Creating new twin of model '{modelId}'...");

                    client.CreateOrReplaceDigitalTwin(twin, new BasicDigitalTwin()
                    {
                        Id = twin,
                        Metadata = { ModelId = modelId }
                    });
                }

                try
                {
                    // update twin with full patch document
                    var result = client.UpdateDigitalTwinAsync(twin, updateTwinData).Result;

                    log.LogInformation($"Successfully updated twin: {twin}");
                }
                catch (Exception ex)
                {
                    log.LogError($"Error updating twin: {ex.Message}");
                }

                updateTwinData = null;
            }

            client = null;
            credentials = null;
        }

        /// <summary>
        /// check to see if value is numeric (float)
        /// </summary>
        /// <param name="value">string value</param>
        /// <returns>boolean</returns>
        private bool isNumeric(string value)
        {
            double output;
            return double.TryParse(value, out output);
        }

        /// <summary>
        /// decode a string if it is base64 encoded. If it is plain text, return that plain text
        /// </summary>
        /// <param name="text">encoded string</param>
        /// <returns>string</returns>
        private string base64DecodeString(string text)
        {
            if (this.isBase64Encoded(text))
            {
                byte[] data = System.Convert.FromBase64String(text);
                string decodedText = System.Text.ASCIIEncoding.ASCII.GetString(data);

                return decodedText;
            }
            else
            {
                return text;
            }
        }

        /// <summary>
        /// check to see if the string is base64encoded
        /// </summary>
        /// <param name="base64String">encoded string</param>
        /// <returns>bool</returns>
        private bool isBase64Encoded(string base64String)
        {
            if (string.IsNullOrEmpty(base64String) || base64String.Length % 4 != 0 || base64String.Contains(" ") || base64String.Contains("\t") || base64String.Contains("\r") || base64String.Contains("\n"))
                return false;
            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch (Exception)
            {
                // Handle the exception
            }

            return false;
        }

        private string getValueFromSplit(string item, int pos = 0)
        {
            return this.getValueFromSplit('/', item, pos);
        }

        private string getValueFromSplit(char seperator, string item, int pos = 0)
        {
            string[] subs = item.Split(seperator);

            return subs[pos];
        }

        /// <summary>
        /// get mapping file list from json configuration file or cache
        /// </summary>
        /// <param name="url">url for mapping file</param>
        /// <param name="log">logger</param>
        /// <returns>List<NodeTwinMap> Object</returns>
        private IList<NodeTwinMap> GetNodeToTwinMapping(string url, ILogger log)
        {
            if (_nodeTwinMapList == null)
            {
                log.LogInformation($"mapping file cache expired, loading from actual json file.");

                string json = new WebClient().DownloadString(url);

                if (_logLevel >= 300) log.LogInformation($"url: '{url}'...\r\n{json}\r\n");

                _nodeTwinMapList = JsonConvert.DeserializeObject<List<NodeTwinMap>>(json);
            }

            return _nodeTwinMapList;
        }
    }
}
