using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Web;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace API_Aggregator;

public class API_Aggregator_Main
{
    private static readonly HttpClient client = new HttpClient();
    private const string ApiKey = "6b920e03-1991-458c-92d8-f75913dce8b8";
    private const string HarvardBaseUrl = "https://api.harvardartmuseums.org/object";
    private const string METBaseURL = "https://collectionapi.metmuseum.org/public/collection/v1/search";
    private const string METObjectLookup = "https://collectionapi.metmuseum.org/public/collection/v1/objects/";
    private const int MaxResults = 150;

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        //  CloudWatch Logs
        var log = context.Logger;
        log.Log($"request = {JsonConvert.SerializeObject(request, Formatting.Indented)}");
        log.Log($"requestBody = {JsonConvert.SerializeObject(request.Body, Formatting.Indented)}");

        //  Initial Error Trap - Incorrect Call Method
        if (ReqMethodChecker(request))
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

            //  requestObject Creation
            var requestObject = new RequestObject
            {
                Keyword = requestBody["keyword"]?.ToString(),
                Title = requestBody["title"]?.ToString(),
                HasImage = requestBody["hasimage"]?.Value<int>() != 0,
                Location = requestBody["location"],
                Classification = requestBody["classification"],
                Medium = requestBody["medium"]
            };

            // Param Creation Through Class Methods
            string searchQueryHarvard = requestObject.HarvardParamGen();
            string searchQueryMET = requestObject.METParamGen();

            //  Cloudwatch Logging - MET & Harvard Params + Incoming Body Params
            log.Log($"\nParsed parameters: keyword='{requestObject.Keyword}', medium='{requestObject.Medium}', hasImage={requestObject.HasImage}, " + $"location='{requestObject.Location}', classification='{requestObject.Classification}', title='{requestObject.Title}'");
            log.Log($"\nHarvard Finished String Query : {searchQueryHarvard}");
            log.Log($"\nMET Finished String Query : {searchQueryMET}");

            //  MET & Harvard Calls
            var harvardResults = await HarvardCall(searchQueryHarvard, context);
            var metResults = await METCall(searchQueryMET, context);
            //  Concatenation Of The Results
            var combinedResults = harvardResults.Concat(metResults).ToList();
            //  JSONifying The Concatenated Results
            string jsonResponse = JsonConvert.SerializeObject(combinedResults);

            //  Cloudwatch Logging
            log.Log($"\nBOTH CALLS COMPLETED => Harvard : {harvardResults.Count} | MET : {metResults.Count} | Combined : {combinedResults.Count}");

            //  Successful Exit / API Response
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = jsonResponse,
            };
        }
        catch (Exception ex)
        {
            //  Cloudwatch Logging
            log.Log($"An error occurred while processing the request : {ex.Message}");

            //  Error Exit / API Response
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Body = JsonConvert.SerializeObject(new { error = "An error occurred while processing the request." })
            };
        }
    }

    //  Harvard API Call & Data Formatting
    public async Task<List<ItemObject>> HarvardCall(string requestParam, ILambdaContext context)
    {
        //  Setting Up Vars
        var log = context.Logger;
        var resultList = new List<ItemObject>();
        string nextUrl = null;

        try
        {
            while (resultList.Count < MaxResults)
            {
                do
                {
                    //  Query Creation
                    var query = HttpUtility.ParseQueryString(requestParam);
                    query["apikey"] = ApiKey;

                    //  URI Creation
                    var uriBuilder = new UriBuilder(nextUrl ?? HarvardBaseUrl);
                    if (nextUrl == null) {uriBuilder.Query = query.ToString();}

                    //  CLoudwatch Logging
                    log.LogLine($"\nHarvard API Request URL: {uriBuilder.Uri}");

                    //  Making the HTTP request & converting the response into a JObject
                    JObject jsonResponse = await ResponseToJSON(uriBuilder);

                    //  Sets nextUrl to the new nextUrl, null is caught by the while exception
                    nextUrl = (string)jsonResponse["info"]["next"];

                    //  Cloudwatch Logging
                    log.LogLine($"\nHarvard API Called Successfully : {jsonResponse["info"]["totalrecords"]}");

                    // Using ResponseIterator to process the records
                    var newItems = ResponseIterator((JArray)jsonResponse["records"], MaxResults - resultList.Count, log.LogLine);
                    resultList.AddRange(newItems);

                    // Cloudwatch Logging
                    log.Log($"\nItems Cleaned & Formatted : {resultList.Count} - Harvard API");

                } while (nextUrl != null && resultList.Count < MaxResults);

                //  Breaking Once All Pages Have Been Paginated Through
                if (nextUrl == null)
                {
                    break;
                }
            }

            return resultList;
        }
        catch (Exception ex)
        {
            //  Cloudwatch Logging
            log.LogLine($"API Call Error: Error Calling Harvard API");
            log.LogLine($"Error Message : {ex.Message}");

            //  Returning Empty List To Allow MET To Run Still
            return new List<ItemObject>();
        }
    }

    //  MET API Call & Data Formatting
    public async Task<List<ItemObject>> METCall(string requestParam, ILambdaContext context)
    {
        //  Setting Up Vars
        var log = context.Logger;
        var query = HttpUtility.ParseQueryString(requestParam);
        var uriBuilder = new UriBuilder(METBaseURL) {Query = query.ToString()};

        //  Cloudwatch Logging
        log.LogLine($"\nMET API Search Request URL: {uriBuilder.Uri}");

        try
        {
            //  Making the HTTP request & converting the response into a JObject - ItemIDs
            JObject jsonResponse = await ResponseToJSON(uriBuilder);
            int[] metIDs = jsonResponse["objectIDs"].ToObject<int[]>();

            //  Cloudwatch Logging
            log.Log("\nMET object IDs successfully retrieved.");

            ////  Setting Up Vars
            List<ItemObject> resultList = new List<ItemObject>();
            bool[] processedIDs = new bool[metIDs.Length];
            int processedCount = 0;

            // Iterating through retrieved item IDs with a max of 150 to be processed (Successful not attempted)
            for (int i = 0; i < metIDs.Length && resultList.Count < MaxResults; i++)
            {
                // Checking For Item Duplication
                if (processedIDs[i]) { break; }

                try
                {
                    //  Item Specific HTTP Call Made
                   var responseItem = await METRequest($"{METObjectLookup}/{metIDs[i]}", log.LogLine, metIDs[i]);

                    //  responseItem Added To resultList
                   resultList.Add(responseItem);
                }
                catch (Exception ex)
                {
                    //  Cloudwatch Logging
                    log.Log($"\nError Calling Object [MET] : {ex.Message} - Item ID : {metIDs[i]}");
                }
                finally
                {
                    //  Counters Updated
                    processedIDs[i] = true;
                    processedCount++;
                }
            }
            //  Cloudwatch Logging
            log.Log($"\nMET items Cleaned & Formatted : {resultList.Count}");
            return resultList;
        }
        catch (Exception ex)
        {
            //  Cloudwatch Logging
            log.LogLine($"\nAPI Call Error: Error Calling MET API");
            log.LogLine($"\nError Message : {ex.Message}");

            //  Returning An Empty List To Allow Completion Of Call
            return new List<ItemObject>();
        }
    }

    //  Method Checking Function - Main
    public bool ReqMethodChecker(APIGatewayHttpApiV2ProxyRequest request)
    {
        string usedMethod = request.RequestContext.Http.Method;
        return usedMethod != "POST";
    }

    //  Request Call & JSONifying - Both
    public async Task<JObject> ResponseToJSON(UriBuilder uriBuilder)
    {
        using (HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri))
        {
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseBody);
        }
    }

    //  Request Call & JSONifying - Item not ItemIDs - MET
    public async Task<JObject> MET_ItemToJSON(string itemString)
    {
        using (HttpResponseMessage response = await client.GetAsync(itemString))
        {
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseBody);
        }
    }

    public List<ItemObject> ResponseIterator(JArray records, int maxItems, Action<string> logAction)
    {
        // resultList initialising 
        var resultList = new List<ItemObject>();

        foreach (var item in records)
        {
            //  Limit response length for speed
            if (resultList.Count >= maxItems)
            {
                break;
            }

            //  responseItem constructor
            ItemObject responseItem = new ItemObject(
                item["creditline"]?.ToString(),
                item["division"]?.ToString(),
                Convert.ToInt32(item["id"]),
                item["classification"]?.ToString(),
                ExtractImageUrl(item),
                ExtractArtistName(item),
                item["medium"]?.ToString(),
                item["title"]?.ToString(),
                item["dated"]?.ToString(),
                item["url"]?.ToString(),
                item["century"]?.ToString(),
                ExtractArtistBirthplace(item)
            );

            //  Adding the responseItem to the resultList
            resultList.Add(responseItem);

            //  Cloudwatch Logging
            HarvardCompleteLog(logAction, item["id"].ToString());
        }

        return resultList;
    }

    //  Function for the sake of DRY - Harvard
    public void HarvardCompleteLog(Action<string> logAction, string itemId)
    {
        logAction($"\nItem Formatted - Harvard API - ItemID : {itemId}");
        logAction("\nItem Formatted - Harvard API");
    }

    //  Function for the sake of DRY - MET
    public void METCompleteLog(Action<string> logAction, string itemId)
    {
        logAction($"\nItem Formatted - MET API - ItemID : {itemId}");
        logAction("\nItem Formatted - MET API");
    }

    //  Safe access function - Harvard
    public string ExtractImageUrl(JToken item)
    {
        var imagesArray = item["images"] as JArray;
        return imagesArray?.Count > 0 && imagesArray[0]["baseimageurl"] != null
            ? imagesArray[0]["baseimageurl"].ToString()
            : null;
    }

    //  Safe access function - Harvard
    public string ExtractArtistName(JToken item)
    {
        var peopleArray = item["people"] as JArray;
        return peopleArray?.Count > 0 && peopleArray[0]["name"] != null
            ? peopleArray[0]["name"].ToString()
            : null;
    }

    //  Safe access function - Harvard
    public string ExtractArtistBirthplace(JToken item)
    {
        var peopleArray = item["people"] as JArray;
        return peopleArray?.Count > 0 && peopleArray[0]["birthplace"] != null
            ? peopleArray[0]["culture"].ToString()
            : null;
    }

    //  Safe Access Function - MET
    public string ExtractImageUrl_MET(JObject itemJSONResponse)
    {
        if (itemJSONResponse == null || itemJSONResponse["primaryImage"] == null)
        {
            return null;
        }

        string primaryImage = itemJSONResponse["primaryImage"].ToString();
        return !string.IsNullOrEmpty(primaryImage) ? primaryImage : null;
    }

    //  Safe Access Function - MET
    public string ExtractArtistName_MET(JObject itemJSONResponse)
    {
        if (itemJSONResponse == null ||
            itemJSONResponse["constituents"] == null ||
            !itemJSONResponse["constituents"].HasValues)
        {
            return null;
        }

        var constituents = itemJSONResponse["constituents"] as JArray;
        return constituents?[0]?["name"]?.ToString();
    }

    //  Century Calculator - MET
    public string CalculateCentury(JToken objectDate)
    {
        if (string.IsNullOrEmpty(objectDate?.ToString()))
            return "Unknown Century";

        string yearString = objectDate.ToString();

        if (yearString.Length != 4 || !int.TryParse(yearString.Substring(0, 2), out int century))
            return "Invalid Year";

        century += 1;

        int lastDigit = century % 10;
        int lastTwoDigits = century % 100;

        string suffix = lastTwoDigits >= 11 && lastTwoDigits <= 13 ? "th" :
            lastDigit == 1 ? "st" :
            lastDigit == 2 ? "nd" :
            lastDigit == 3 ? "rd" : "th";

        return $"{century}{suffix} Century";
    }

    //  responseItem Creation - MET
    public async Task<ItemObject> METRequest(string METItemURL , Action<string> logAction, int metID)
    {
        //  Making the HTTP request & converting the response into a JObject - Actual Items 
        JObject itemJSONResponse = await MET_ItemToJSON(METItemURL);

        ItemObject responseItem = new ItemObject(
            itemJSONResponse["creditLine"]?.ToString(),
            itemJSONResponse["department"]?.ToString(),
            Convert.ToInt32(itemJSONResponse["objectID"]),
            itemJSONResponse["objectName"]?.ToString(),
            ExtractImageUrl_MET(itemJSONResponse),
            ExtractArtistName_MET(itemJSONResponse),
            itemJSONResponse["medium"]?.ToString(),
            itemJSONResponse["title"]?.ToString(),
            itemJSONResponse["objectDate"]?.ToString(),
            itemJSONResponse["objectURL"]?.ToString(),
            CalculateCentury(itemJSONResponse["objectDate"]),
            itemJSONResponse["artistNationality"]?.ToString()
        );
        METCompleteLog(logAction, metID.ToString());

        return responseItem;
    }
}

//Testing Merge Fix