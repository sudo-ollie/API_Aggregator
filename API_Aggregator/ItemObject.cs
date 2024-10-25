using Newtonsoft.Json.Linq;
using System.Web;

namespace API_Aggregator
{
    public class ItemObject
    {
        public string CreditLine { get; set; }
        public string ArticleDivision { get; set; }
        public int ArticleId { get; set; }
        public string ArticleClassification { get; set; }
        public string ImageUrl { get; set; }
        public string ArtistName { get; set; }
        public string Technique { get; set; }
        public string Title { get; set; }
        public string Date { get; set; }
        public string ItemURL { get; set; }
        public string Century { get; set; }
        public string ArtistNationality { get; set; }

        public ItemObject(
            string creditLine,
            string articleDivision,
            int articleId,
            string articleClassification,
            string imageUrl,
            string artistName,
            string technique,
            string title,
            string date,
            string itemURL,
            string century,
            string artistNationality)
        {
            CreditLine = creditLine;
            ArticleDivision = articleDivision;
            ArticleId = articleId;
            ArticleClassification = articleClassification;
            ImageUrl = imageUrl;
            ArtistName = artistName;
            Technique = technique;
            Title = title;
            Date = date;
            ItemURL = itemURL;
            Century = century;
            ArtistNationality = artistNationality;
        }
    }
    public class RequestObject
    {
        public string Keyword { get; set; }
        public string Title { get; set; }
        public bool HasImage { get; set; }
        public JToken Location { get; set; }
        public JToken Classification { get; set; }
        public JToken Medium { get; set; }
        public string METQuery { get; set; }
        public string HarvardQuery { get; set; }

        public static string HandleClassification(JToken classification)
        {
            Console.WriteLine($"\nClassification : {classification}");
            Console.WriteLine($"\nClassification : {classification.ToString()}");
            return FormatQueryParameter(classification);
        }

        public static string HandleLocation(JToken location)
        {
            Console.WriteLine($"\nLocation : {location}");
            Console.WriteLine($"\nLocation : {location.ToString()}");
            return FormatQueryParameter(location);
        }

        public static string HandleMedium(JToken medium)
        {
            Console.WriteLine($"\nMedium : {medium}");
            Console.WriteLine($"\nMedium : {medium.ToString()}");
            return FormatQueryParameter(medium);
        }

        public string METParamGen()
        {
            var queryParams = new List<string>();

            AddIfNotEmpty(queryParams, "q", Keyword);
            AddIfNotEmpty(queryParams, "medium", Medium);
            queryParams.Add($"hasImages={HasImage.ToString().ToLower()}");
            AddIfNotEmpty(queryParams, "place", Location);
            AddIfNotEmpty(queryParams, "classification", Classification);
            AddIfNotEmpty(queryParams, "title", Title);

            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        public string HarvardParamGen()
        {
            var queryParams = new List<string>();

            AddIfNotEmpty(queryParams, "q", Keyword);
            AddIfNotEmpty(queryParams, "medium", Medium, "any");
            queryParams.Add($"hasimage={Convert.ToInt32(HasImage)}");
            AddIfNotEmpty(queryParams, "geoLocation", Location);
            AddIfNotEmpty(queryParams, "title", Title);

            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        private void AddIfNotEmpty(List<string> queryParams, string paramName, JToken value, string defaultValue = null)
        {
            if (!IsNullOrEmpty(value))
            {
                string formattedValue = FormatQueryParameter(value);
                queryParams.Add($"{paramName}={formattedValue}");
            }
            else if (defaultValue != null)
            {
                queryParams.Add($"{paramName}={defaultValue}");
            }
        }

        private static bool IsNullOrEmpty(JToken token)
        {
            return token == null ||
                   token.Type == JTokenType.Null ||
                   (token.Type == JTokenType.Array && !token.HasValues) ||
                   (token.Type == JTokenType.String && string.IsNullOrWhiteSpace(token.ToString()));
        }

        private static string FormatQueryParameter(JToken token)
        {
            if (token.Type == JTokenType.Array)
            {
                return string.Join("|", token.Where(item => !string.IsNullOrWhiteSpace(item.ToString()))
                                            .Select(item => HttpUtility.UrlEncode(item.ToString())));
            }
            return HttpUtility.UrlEncode(token.ToString());
        }
    }

}