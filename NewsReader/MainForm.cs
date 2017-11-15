using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace NewsReader
{
    public partial class MainForm : Form
    {
        IDictionary<string, NewsGrid> tabGridDict = new Dictionary<string, NewsGrid>();
        List<NewsRecord> newRecordsHK = new List<NewsRecord>();
        public delegate void OnNewsEventHandler(string tab, NewsRecord[] newsRecords);
        public event OnNewsEventHandler OnNewsEvent;
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Setup_TabPages();
           
            this.BeginInvoke(new MethodInvoker(delegate { RunAll_Click(sender, e); }));

            this.OnNewsEvent += HandleNewEvents;
        }

        private void HandleNewEvents(string tab, NewsRecord[] newsRecords)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    HandleNewEvents(tab, newsRecords);
                }));
            }
            else
            {
                foreach (NewsRecord newsRecord in newsRecords)
                {
                    DataRow newRow = tabGridDict[tab].DataSource.NewRow();
                    newRow["Ticker"] = newsRecord.Security;
                    newRow["Title"] = newsRecord.Title;
                    newRow["Url"] = newsRecord.Url;
                    newRow["Source"] = newsRecord.Source;
                    newRow["Published"] = newsRecord.Published;
                    tabGridDict[tab].DataSource.Rows.Add(newRow);
                }

                labelLastRefresh.Text = String.Format("Last Refreshed @ {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            }
        }

        private void Setup_TabPages()
        {
            string tabs = ConfigurationManager.AppSettings["Tabs"];
            foreach (string tab in tabs.Split(','))
            {
                NewsGrid newsGrid = new NewsGrid()
                {
                    Dock = DockStyle.Fill
                };
                TabPage tabPage = new TabPage(tab);
                tabPage.Controls.Add(newsGrid);
                tabNews.TabPages.Add(tabPage);

                tabGridDict[tab] = newsGrid;
            }
        }

        private DateTime GetPreviousBusinessDay()
        {
            if (DateTime.Today.DayOfWeek == DayOfWeek.Monday)
            {
                return DateTime.Today.Subtract(new TimeSpan(3, 0, 0, 0));
            }
            return DateTime.Today.Subtract(new TimeSpan(1, 0, 0, 0));
        }

        private void RunAll_Click(object sender, EventArgs e)
        {
            DateTime prevDay = GetPreviousBusinessDay();
            string tabs = ConfigurationManager.AppSettings["Tabs"];
            foreach (string tab in tabs.Split(','))
            {
                string filePath = ConfigurationManager.AppSettings[tab];
                if (File.Exists(filePath))
                {
                    tabGridDict[tab].DataSource.Clear();

                    if (tab.Contains("HK"))
                    {
                        newRecordsHK.AddRange(CaptureHKNews(prevDay));
                    }
                    string[] secList = File.ReadAllLines(filePath);
                    foreach (string secItem in secList)
                    {
                        System.Diagnostics.Debug.WriteLine(String.Format("========= {0} =========", secItem));

                        (new Thread(new ThreadStart(delegate () { CaptureNews(secItem, tab, prevDay); }))).Start();
   
                    }
                }
                else
                {
                    MessageBox.Show(String.Format("File not found!\nThe following file {0} for {1} cannot be found.  Please check configuration file and whether the file exists", filePath, tab), "File not found!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CaptureNews(string security, string tab, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            if (security.EndsWith(":US"))
            {
                // SeekingAlpha
                newsRecords.AddRange(CaptureSeekingAlpha(security, cutoff));
                // Google News
                newsRecords.AddRange(CaptureGoogleNews(security, cutoff));
                // Nasdaq
                newsRecords.AddRange(CaptureNasdaq(security, cutoff));
                // Bloomberg
                newsRecords.AddRange(CaptureBloomberg(security, cutoff));
            }
            else if (security.EndsWith(":HK"))
            {
                // Google News
                newsRecords.AddRange(CaptureGoogleNews(security, cutoff));
                // Yahoo Finance
                newsRecords.AddRange(CaptureYahooFinance(security, cutoff));
                // Bloomberg
                newsRecords.AddRange(CaptureBloomberg(security, cutoff));

                // AA Stocks
                newsRecords.AddRange(CaptureAAStock(security, cutoff));
                // HK News
                newsRecords.AddRange(CaptureHKNews(security));
            }

            OnNewsEvent?.Invoke(tab, newsRecords.ToArray());
        }

        #region Seeking Alpha
        private NewsRecord[] CaptureSeekingAlpha(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string shortticker = security.Replace(":US", "");
            string url = String.Format("https://seekingalpha.com/api/sa/combined/{0}.xml", shortticker);

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 10000;
                using (WebResponse response = request.GetResponse())
                {
                    using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        foreach (SyndicationItem item in feed.Items)
                        {
                            if (item.PublishDate.DateTime >= cutoff)
                            {
                                if (item.Links.Count > 0)
                                {
                                    NewsRecord newsRecord = new NewsRecord()
                                    {
                                        Security = security,
                                        Title = item.Title.Text,
                                        Url = item.Links[0].Uri.AbsoluteUri,
                                        Source = "Seeking Alpha",
                                        Published = item.PublishDate.DateTime
                                    };

                                    newsRecords.Add(newsRecord);
                                    System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                                }
                            }
                        }

                        reader.Close();
                    }
                }
               
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return newsRecords.ToArray();
        }
        #endregion

        #region Google News
        private NewsRecord[] CaptureGoogleNews(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string shortticker = security.Replace(":US", "").Replace(":HK", "");
            string url = String.Format("https://www.google.com/finance/company_news?q={0}&output=rss", shortticker);

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 10000;
                using (WebResponse response = request.GetResponse())
                {
                    using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        foreach (SyndicationItem item in feed.Items)
                        {
                            if (item.PublishDate.DateTime >= cutoff)
                            {
                                if (item.Links.Count > 0)
                                {
                                    NewsRecord newsRecord = new NewsRecord()
                                    {
                                        Security = security,
                                        Title = item.Title.Text,
                                        Url = item.Links[0].Uri.AbsoluteUri,
                                        Source = "Google News",
                                        Published = item.PublishDate.DateTime
                                    };

                                    newsRecords.Add(newsRecord);
                                    System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                                }
                            }
                        }

                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
           
            return newsRecords.ToArray();
        }
        #endregion

        #region Yahoo Finance News
        private NewsRecord[] CaptureYahooFinance(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string shortticker = security;
            if (security.EndsWith(":US"))
                shortticker = security.Replace(":US", "");
            else if (security.EndsWith(":HK"))
                shortticker = security.Replace(":HK", "").PadLeft(4, '0') + ".HK";

            string url = String.Format("https://feeds.finance.yahoo.com/rss/2.0/headline?s={0}", shortticker);

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 10000;
                using (WebResponse response = request.GetResponse())
                {
                    using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        foreach (SyndicationItem item in feed.Items)
                        {
                            if (item.PublishDate.DateTime >= cutoff)
                            {
                                if (item.Links.Count > 0)
                                {
                                    NewsRecord newsRecord = new NewsRecord()
                                    {
                                        Security = security,
                                        Title = item.Title.Text,
                                        Url = item.Links[0].Uri.AbsoluteUri,
                                        Source = "Yahoo Finance",
                                        Published = item.PublishDate.DateTime
                                    };

                                    newsRecords.Add(newsRecord);
                                    System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                                }
                            }
                        }

                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return newsRecords.ToArray();
        }
        #endregion

        #region Nasdaq
        private NewsRecord[] CaptureNasdaq(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string shortticker = security.Replace(":US", "");
            string url = String.Format("http://articlefeeds.nasdaq.com/nasdaq/symbols?symbol={0}", shortticker);

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 10000;
                using (WebResponse response = request.GetResponse())
                {
                    using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        foreach (SyndicationItem item in feed.Items)
                        {
                            if (item.PublishDate.DateTime >= cutoff)
                            {
                                if (item.Links.Count > 0)
                                {
                                    NewsRecord newsRecord = new NewsRecord()
                                    {
                                        Security = security,
                                        Title = item.Title.Text,
                                        Url = item.Links[0].Uri.AbsoluteUri,
                                        Source = "Nasdaq",
                                        Published = item.PublishDate.DateTime
                                    };

                                    newsRecords.Add(newsRecord);
                                    System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                                }
                            }
                        }

                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return newsRecords.ToArray();
        }
        #endregion

        #region Bloomberg News
        private NewsRecord[] CaptureBloomberg(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string url = String.Format("https://www.bloomberg.com/quote/{0}", security);
            try
            {
                var web = new HtmlAgilityPack.HtmlWeb();
                var doc = web.Load(url);
                var articles = doc.DocumentNode.SelectNodes("//article");
                if (articles != null)
                {
                    foreach (var article in articles)
                    {
                        try
                        {
                            string articleTitle = article.SelectNodes("a/div/div").First().InnerText;
                            string articlePublish = article.SelectNodes("a/div/div").Skip(1).FirstOrDefault().InnerText;
                            DateTime articlePublishDt = DateTime.Now;
                            if (articlePublish.Contains("seconds ago"))
                            {
                                string articlePublishSec = articlePublish.Replace(" seconds ago", "").Trim();
                                articlePublishDt = DateTime.Now.Subtract(new TimeSpan(0, 0, 0, Convert.ToInt32(articlePublishSec)));
                            }
                            else if (articlePublish.Contains("minutes ago"))
                            {
                                string articlePublishMin = articlePublish.Replace(" minutes ago", "").Trim();
                                articlePublishDt = DateTime.Now.Subtract(new TimeSpan(0, 0, Convert.ToInt32(articlePublishMin), 0));
                            }
                            else if (articlePublish.Contains("hours ago"))
                            {
                                string articlePublishHr = articlePublish.Replace(" hours ago", "").Trim();
                                articlePublishDt = DateTime.Now.Subtract(new TimeSpan(0, Convert.ToInt32(articlePublishHr), 0, 0));
                            }
                            else if (articlePublish.Contains("a day ago"))
                            {
                                articlePublishDt = DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0));
                            }
                            else if (articlePublish.Length > 10)
                            {
                                articlePublishDt = DateTime.ParseExact(articlePublish, "MMMM d, yyyy", null);
                            }
                            else
                            {
                                articlePublishDt = DateTime.ParseExact(articlePublish, "m/d/yyyy", null);
                            }
                            string articleLink = article.SelectNodes("a").First().Attributes["href"].Value;

                            if (articlePublishDt >= cutoff)
                            {
                                NewsRecord newsRecord = new NewsRecord()
                                {
                                    Security = security,
                                    Title = articleTitle,
                                    Url = articleLink,
                                    Source = "Bloomberg",
                                    Published = articlePublishDt
                                };

                                newsRecords.Add(newsRecord);
                                System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message + " " + article.OuterHtml);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return newsRecords.ToArray();
        }
        #endregion

        #region AAStocks News
        private NewsRecord[] CaptureAAStock(string security, DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string shortticker = security.Replace(":HK", "").PadLeft(5, '0');
            string url = String.Format("http://www.aastocks.com/en/stocks/analysis/stock-aafn/{0}/0/hk-stock-news/1", shortticker);
            try
            {
                var web = new HtmlAgilityPack.HtmlWeb();
                var doc = web.Load(url);
                var articles = doc.DocumentNode.SelectNodes("//div[contains(@class, 'newshead2')]");
                var timestamps = doc.DocumentNode.SelectNodes("//div[contains(@class, 'newstime2')]");
                if (articles.Count() == timestamps.Count())
                {
                    for (int i=0;i<articles.Count();i++)
                    {
                        NewsRecord newsRecord = new NewsRecord();
                        newsRecord.Security = security;
                        newsRecord.Source = "AAStocks";
                        newsRecord.Title = articles[i].InnerText;
                        newsRecord.Url = articles[i].SelectSingleNode("a").GetAttributeValue("href", "");
                        if (!newsRecord.Url.Contains("http"))
                        {
                            newsRecord.Url = "http://www.aastocks.com" + newsRecord.Url;
                        }
                        newsRecord.Published = DateTime.ParseExact(timestamps[i].InnerText, "yyyy/MM/dd HH:mm", null);

                        if (newsRecord.Published >= cutoff)
                        {
                            newsRecords.Add(newsRecord);
                            System.Diagnostics.Debug.WriteLine(newsRecord.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return newsRecords.ToArray();
        }

        #endregion

        #region HK Exchange News
        private NewsRecord[] CaptureHKNews(DateTime cutoff)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            string url = "http://www.hkexnews.hk/listedco/listconews/mainindex/SEHK_LISTEDCO_DATETIME_SEVEN.HTM";
            try
            {
                var web = new HtmlAgilityPack.HtmlWeb();
                var doc = web.Load(url);
                var articles = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'row0')]");
                if (articles != null)
                {
                    foreach (var article in articles)
                    {
                        try
                        {
                            NewsRecord newsRecord = new NewsRecord();
                            newsRecord.Published = DateTime.ParseExact(article.SelectNodes("td")[0].InnerText.Substring(0, 10) + " " + article.SelectNodes("td")[0].InnerText.Substring(10, 5), "dd/MM/yyyy HH:mm", null);
                            newsRecord.Security = Convert.ToInt64(article.SelectNodes("td")[1].InnerText).ToString() + ":HK";
                            newsRecord.Title = article.SelectNodes("td")[3].InnerText;
                            newsRecord.Source = "HKEx News";
                            newsRecord.Url = "http://www.hkexnews.hk" + article.SelectNodes("td")[3].SelectSingleNode("a").GetAttributeValue("href", "");
                            if (newsRecord.Published >= cutoff)
                            {
                                newsRecords.Add(newsRecord);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message + " " + article.OuterHtml);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return newsRecords.ToArray();
        }

        private NewsRecord[] CaptureHKNews(string security)
        {
            List<NewsRecord> newsRecords = new List<NewsRecord>();
            foreach (NewsRecord newsRecord in newRecordsHK)
            {
                if (newsRecord.Security == security)
                {
                    newsRecords.Add(newsRecord);
                }
            }
            return newsRecords.ToArray();
        }
        #endregion

        #region News Record Class
        public class NewsRecord
        {
            public string Security { get; set; }
            public string Title { get; set; }
            public string Url { get; set; }
            public string Source { get; set; }
            public DateTime Published { get; set; }
            public override string ToString()
            {
                return String.Format("security='{0}';title='{1}',url='{2}',source='{3}';published='{4}'", Security, Title, Url, Source, Published);
            }
        }
        #endregion

        private void Refresh_Tick(object sender, EventArgs e)
        {
            RunAll_Click(sender, e);
        }

        private void RefreshInterval_Change(object sender, EventArgs e)
        {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            if (clickedItem != null)
            {
                foreach (ToolStripMenuItem refreshItem in refreshEveryToolStripMenuItem.DropDownItems)
                {
                    refreshItem.Checked = (refreshItem == clickedItem);
                    if (refreshItem == clickedItem)
                    {
                        switch (refreshItem.Text)
                        {
                            case "4 Hours":
                                timerRefresh.Interval = 4 * 60 * 60 * 1000;
                                break;
                            case "8 Hours":
                                timerRefresh.Interval = 8 * 60 * 60 * 1000;
                                break;
                            case "12 Hours":
                                timerRefresh.Interval = 12 * 60 * 60 * 1000;
                                break;
                            case "1 Day":
                                timerRefresh.Interval = 24 * 60 * 60 * 1000;
                                break;
                        }
                    }
                }
            }
            
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.OnNewsEvent -= HandleNewEvents;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
