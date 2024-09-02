using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public ItemObject(string creditLine, string division, int articleId, string classification, string imageUrl, string artistName)
        {
            CreditLine = creditLine;
            ArticleDivision = division;
            ArticleId = articleId;
            ArticleClassification = classification;
            ImageUrl = imageUrl;
            ArtistName = artistName;
        }
    }
}
