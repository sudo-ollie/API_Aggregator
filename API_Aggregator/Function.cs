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
        log.Log($"request = {JsonConvert.SerializeObject(request, Formatting.Indented)}");
        log.Log($"requestBody = {JsonConvert.SerializeObject(request.Body, Formatting.Indented)}");

        // Initial Error Trap - Incorrect Call Method
        if (request.RequestContext.Http.Method != "POST")
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
            //  Pull Options Off Request Body
            var requestBody = JObject.Parse(request.Body);

            var requestObject = new RequestObject
            {
                Keyword = requestBody["keyword"]?.ToString(),
                Title = requestBody["title"]?.ToString(),
                HasImage = requestBody["hasimage"]?.Value<int>() != 0,
                Location = requestBody["location"],
                Classification = requestBody["classification"],
                Medium = requestBody["medium"]
            };

            string searchQueryHarvard = requestObject.HarvardParamGen();
            string searchQueryMET = requestObject.METParamGen();

            log.Log($"\nParsed parameters: keyword='{requestObject.Keyword}', medium='{requestObject.Medium}', hasImage={requestObject.HasImage}, " + $"location='{requestObject.Location}', classification='{requestObject.Classification}', title='{requestObject.Title}'");

            log.Log($"\nHarvard Finished String Query : {searchQueryHarvard}");
            log.Log($"\nMET Finished String Query : {searchQueryMET}");

            var harvardResults = await HarvardCall(searchQueryHarvard, context);
            var metResults = await METCall(searchQueryMET, context);
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
        var resultList = new List<ItemObject>();
        string nextUrl = null;

        try
        {
            while (resultList.Count < 250)
            {
                do
                {
                    var query = HttpUtility.ParseQueryString(requestParam);
                    query["apikey"] = ApiKey;
                    var uriBuilder = new UriBuilder(nextUrl ?? HarvardBaseUrl);
                    if (nextUrl == null)
                    {
                        uriBuilder.Query = query.ToString();
                    }
                    log.LogLine($"\nHarvard API Request URL: {uriBuilder.Uri}");
                    HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(responseBody);
                    nextUrl = (string)jsonResponse["info"]["next"];
                    int totalRecords = (int)jsonResponse["info"]["totalrecords"];
                    log.LogLine($"\nHarvard API Called Successfully : {totalRecords}");
                    foreach (var item in jsonResponse["records"])
                    {
                        if (resultList.Count >= 250)
                        {
                            break;
                        }

                        string imageURL = null;
                        var imagesArray = item["images"] as JArray;
                        if (imagesArray?.Count > 0 && imagesArray[0]["baseimageurl"] != null)
                        {
                            imageURL = imagesArray[0]["baseimageurl"].ToString();
                        }
                        string artistName = null;
                        string artistBirthplace = null;
                        var peopleArray = item["people"] as JArray;
                        if (peopleArray?.Count > 0 && peopleArray[0]["name"] != null && peopleArray[0]["birthplace"] != null)
                        {
                            artistName = peopleArray[0]["name"].ToString();
                            artistBirthplace = peopleArray[0]["culture"].ToString();
                        }
                        ItemObject responseItem = new ItemObject(
                            item["creditline"]?.ToString(),
                            item["division"]?.ToString(),
                            Convert.ToInt32(item["id"]),
                            item["classification"]?.ToString(),
                            imageURL,
                            artistName,
                            item["medium"]?.ToString(),
                            item["title"]?.ToString(),
                            item["dated"]?.ToString(),
                            item["url"]?.ToString(),
                            item["century"]?.ToString(),
                            artistBirthplace
                        );
                        resultList.Add(responseItem);
                        log.Log("\nItem Formatted - Harvard API");
                    }
                    log.Log($"\nItems Cleaned & Formatted : {resultList.Count} - Harvard API");
                } while (nextUrl != null && resultList.Count < 250);

                if (nextUrl == null)
                {
                    break;
                }
            }

            return resultList;
        }
        catch (Exception ex)
        {
            log.LogLine($"API Call Error: Error Calling Harvard API");
            log.LogLine($"Error Message : {ex.Message}");
            return new List<ItemObject>();
        }
    }

    //  MET API Call & Data Formatting
    private async Task<List<ItemObject>> METCall(string requestParam, ILambdaContext context)
    {
        var log = context.Logger;
        var query = HttpUtility.ParseQueryString(requestParam);

        var uriBuilder = new UriBuilder(METBaseURL)
        {
            Query = query.ToString()
        };
        log.LogLine($"\nMET API Search Request URL: {uriBuilder.Uri}");

        try
        {
            HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject jsonResponse = JObject.Parse(responseBody);
            int[] metIDs = jsonResponse["objectIDs"].ToObject<int[]>();
            log.Log("\nMET object IDs successfully retrieved.");

            List<ItemObject> resultList = new List<ItemObject>();
            while (resultList.Count < 250)
            {
                foreach (int id in metIDs)
                {
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

                        Func<JToken, string> centuryCalculator = objectDate =>
                        {
                            //  Null Check
                            if (string.IsNullOrEmpty(objectDate?.ToString()))
                                return "Unknown Century";

                            //  Checking for a valid date & then substringing the first 2chars
                            string yearString = objectDate.ToString();
                            if (yearString.Length != 4 || !int.TryParse(yearString.Substring(0, 2), out int century))
                                return "Invalid Year";

                            //  Basic ternary to catch 21st
                            century += 1;
                            string suffix = century == 21 ? "st" : "th";
                            return $"{century}{suffix} Century";
                        };

                        string calculatedCentury = centuryCalculator(itemJSONResponse["objectDate"]);

                        ItemObject responseItem = new ItemObject(
                            itemJSONResponse["creditLine"]?.ToString(),
                            itemJSONResponse["department"]?.ToString(),
                            Convert.ToInt32(itemJSONResponse["objectID"]),
                            itemJSONResponse["objectName"]?.ToString(),
                            imageURL,
                            artistName,
                            itemJSONResponse["medium"]?.ToString(),
                            itemJSONResponse["title"]?.ToString(),
                            itemJSONResponse["objectDate"]?.ToString(),
                            itemJSONResponse["objectURL"]?.ToString(),
                            calculatedCentury,
                            itemJSONResponse["artistNationality"]?.ToString()
                        );
                        resultList.Add(responseItem);
                        log.Log("\nItem Formatted - MET API");
                    }
                    catch (Exception ex)
                    {
                        log.Log($"\nError Calling Obeject [MET] : {ex.Message} - Item ID : {id}");
                    }
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