using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace API_Aggregator;

public class Function
{
    private static readonly HttpClient client = new HttpClient();
    private const string ApiKey = "6b920e03-1991-458c-92d8-f75913dce8b8";
    private const string HarvardBaseUrl = "https://api.harvardartmuseums.org/object";
    private const string METBaseURL = "https://collectionapi.metmuseum.org/public/collection/v1/search";
    private const string METObjectLookup = "https://collectionapi.metmuseum.org/public/collection/v1/objects/";

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        // CloudWatch Logs
        var log = context.Logger;
        //log.Log($"context = {JsonConvert.SerializeObject(context, Formatting.Indented)}");
        //log.Log($"request = {JsonConvert.SerializeObject(request, Formatting.Indented)}");

        // Initial Error Trap - Incorrect Call Method
        if (request.RequestContext.Http.Method is not "GET")
        {
            log.Log("INCORRECT METHOD : Endpoint called with incorrect request method.");
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 404,
                Body = $"Invalid API Method Used : {request.RequestContext.Http.Method}"
            };
        }

        try
        {
            var harvardResults = await HarvardCall("Sunflower", context);
            var metResults = await METCall("Sunflower", context);
            var combinedResults = harvardResults.Concat(metResults).ToList();
            string jsonResponse = JsonConvert.SerializeObject(combinedResults);

            log.Log($"\nBOTH CALLS COMPLETED => Harvard : {harvardResults.Count} | MET : {metResults.Count} | Combined : {combinedResults.Count}");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = jsonResponse,
            };
        }
        catch (Exception ex)
        {
            log.Log($"An error occurred while processing the request : {ex.Message}");
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Body = JsonConvert.SerializeObject(new { error = "An error occurred while processing the request." })
            };
        }
    }

    //  Harvard API Call & Data Formatting
    private async Task<List<ItemObject>> HarvardCall(string requestParam, ILambdaContext context)
    {
        var log = context.Logger;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["classification"] = "Paintings|Prints|Photographs";
        query["keyword"] = requestParam;
        query["apikey"] = ApiKey;

        var uriBuilder = new UriBuilder(HarvardBaseUrl)
        {
            Query = query.ToString()
        };

        try
        {
            HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject jsonResponse = JObject.Parse(responseBody);
            int totalRecords = (int)jsonResponse["info"]["totalrecords"];
            log.LogLine($"Harvard API Called Successfully : {totalRecords}");

            List<ItemObject> resultList = new List<ItemObject>();
            foreach (var item in jsonResponse["records"])
            {
                string imageURL = null;
                var imagesArray = item["images"] as JArray;
                if (imagesArray != null && imagesArray.Count > 0 && imagesArray[0]["baseimageurl"] != null)
                {
                    imageURL = imagesArray[0]["baseimageurl"].ToString();
                }

                string artistName = null;
                var peopleArray = item["people"] as JArray;
                if (peopleArray != null && peopleArray.Count > 0 && peopleArray[0]["name"] != null)
                {
                    artistName = peopleArray[0]["name"].ToString();
                }

                ItemObject responseItem = new ItemObject(
                    item["creditline"]?.ToString(),
                    item["division"]?.ToString(),
                    Convert.ToInt32(item["id"]),
                    item["classification"]?.ToString(),
                    imageURL,
                    artistName
                );

                resultList.Add(responseItem);
            }

            log.Log($"Items Cleaned & Formatted : {resultList.Count}");
            return resultList;
        }
        catch (Exception ex)
        {
            log.LogLine($"API Call Error: Error Calling Harvard API");
            log.LogLine($"Error Message : {ex.Message}");
            List<ItemObject> resultList = new List<ItemObject>();
            return resultList;
        }
    }

    //  MET API Call & Data Formatting
    private async Task<List<ItemObject>> METCall(string requestParam, ILambdaContext context)
    {
        var log = context.Logger;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["q"] = requestParam;

        var uriBuilder = new UriBuilder(METBaseURL)
        {
            Query = query.ToString()
        };

        try
        {
            HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject jsonResponse = JObject.Parse(responseBody);
            int[] metIDs = jsonResponse["objectIDs"].ToObject<int[]>();
            log.Log("\nMET object IDs successfully retrieved.");

            List<ItemObject> resultList = new List<ItemObject>();
            foreach (int id in metIDs)
            {
                await Task.Delay(750);

                try
                {
                    HttpResponseMessage itemResponse = await client.GetAsync($"{METObjectLookup}/{id}");
                    itemResponse.EnsureSuccessStatusCode();
                    string itemResponseBody = await itemResponse.Content.ReadAsStringAsync();
                    JObject itemJSONResponse = JObject.Parse(itemResponseBody);

                    string imageURL = !string.IsNullOrEmpty(itemJSONResponse["primaryImage"].ToString())
                        ? itemJSONResponse["primaryImage"].ToString()
                        : null;

                    string artistName = itemJSONResponse["constituents"] != null && itemJSONResponse["constituents"].HasValues
                        ? itemJSONResponse["constituents"][0]["name"].ToString()
                        : null;

                    ItemObject responseItem = new ItemObject(
                        itemJSONResponse["creditLine"]?.ToString(),
                        itemJSONResponse["department"]?.ToString(),
                        Convert.ToInt32(itemJSONResponse["objectID"]),
                        itemJSONResponse["objectName"]?.ToString(),
                        imageURL,
                        artistName
                    );
                    resultList.Add(responseItem);
                }
                catch (Exception ex)
                {
                    log.Log($"\nError Calling Obeject [MET] : {ex.Message} - Item ID : {id}");
                }
            }

            log.Log($"\nMET items Cleaned & Formatted : {resultList.Count}");
            return resultList;
        }
        catch (Exception ex)
        {
            log.LogLine($"\nAPI Call Error: Error Calling MET API");
            log.LogLine($"\nError Message : {ex.Message}");
            List<ItemObject> resultList = new List<ItemObject>();
            return resultList;
        }
    }


}
