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
}
