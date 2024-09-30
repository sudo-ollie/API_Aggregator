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
            //  Creatinbg List
            var queryParams = new List<string>();

            //  Adding Keyword - Null Check Although It Wont Be Null As FE Checks For It
            if (!string.IsNullOrEmpty(Keyword))
                queryParams.Add($"q={HttpUtility.UrlEncode(Keyword)}");

            //  Medium Adding & Checking / Formatting If An Array
            if (!string.IsNullOrWhiteSpace(Medium.ToString()))
            {
                string mediumValue = FormatQueryParameter(Medium);
                queryParams.Add($"medium={mediumValue}");
            }

            //  Image Adding
            queryParams.Add($"hasImages={HasImage.ToString().ToLower()}");

            //  Location Adding & Checking / Formatting If An Array
            if (!string.IsNullOrWhiteSpace(Location.ToString()))
            {
                string locationValue = FormatQueryParameter(Location);
                queryParams.Add($"place={locationValue}");
            }

            //  Classification Adding & Checking / Formatting If An Array
            //  MET Doesn't Support As A Query Param So Have Omitted
            if (!string.IsNullOrWhiteSpace(Classification.ToString()))
            {
                string classificationValue = FormatQueryParameter(Classification);
                queryParams.Add($"classification={classificationValue}");
            }

            //  Adding Title If Title Is Present
            if (!string.IsNullOrEmpty(Title))
                queryParams.Add($"title={HttpUtility.UrlEncode(Title)}");

            //  Form The Param String If There Are Any
            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        public string HarvardParamGen()
        {
            //  Creating List
            var queryParams = new List<string>();

            //  Adding Keyword - Null Check Although It Wont Be Null As FE Checks For It
            if (!string.IsNullOrEmpty(Keyword))
                queryParams.Add($"q={HttpUtility.UrlEncode(Keyword)}");

            //  Medium Adding & Checking / Formatting If An Array
            if (!string.IsNullOrWhiteSpace(Medium.ToString()))
            {
                string mediumValue = FormatQueryParameter(Medium);
                queryParams.Add($"medium={mediumValue}");
            }
            else
            {
                queryParams.Add("medium=any");
            }

            //  Image Adding
            queryParams.Add($"hasimage={Convert.ToInt32(HasImage)}");

            //  Location Adding & Checking / Formatting If An Array
            if (!string.IsNullOrWhiteSpace(Location.ToString()))
            {
                string locationValue = FormatQueryParameter(Location);
                queryParams.Add($"geoLocation={locationValue}");
            }

            //  Adding Title If Title Is Present
            if (!string.IsNullOrEmpty(Title))
                queryParams.Add($"title={HttpUtility.UrlEncode(Title)}");

            //  Form The Param String If There Are Any
            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        private static string FormatQueryParameter(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return string.Empty;

            if (token.Type == JTokenType.Array)
            {
                return string.Join("|", token.Select(item => HttpUtility.UrlEncode(item.ToString())));
            }

            return HttpUtility.UrlEncode(token.ToString());
        }
    }

}
