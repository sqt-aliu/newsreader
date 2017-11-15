using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NewsReader
{
    public partial class NewsGrid : UserControl
    {
        private DataTable _newsTable;
        public NewsGrid()
        {
            InitializeComponent();

            _newsTable = new DataTable();
            _newsTable.Columns.Add("Ticker", typeof(string));
            _newsTable.Columns.Add("Title", typeof(string));
            _newsTable.Columns.Add("Url", typeof(string));
            _newsTable.Columns.Add("Source", typeof(string));
            _newsTable.Columns.Add("Published", typeof(DateTime));
            _newsTable.AcceptChanges();

            gridNews.AutoGenerateColumns = false;
            gridNews.DataSource = _newsTable.AsDataView();

            DataGridViewTextBoxColumn dgcolTicker = new DataGridViewTextBoxColumn()
            {
                Name = "Ticker",
                DataPropertyName = "Ticker",
                HeaderText = "Ticker",
                Width = 80
            };
            DataGridViewLinkColumn dgcolTitle = new DataGridViewLinkColumn()
            {
                Name = "Title",
                DataPropertyName = "Title",
                HeaderText = "Title",
                Width = 800
            };
            DataGridViewTextBoxColumn dgcolUrl = new DataGridViewTextBoxColumn()
            {
                Name = "Url",
                DataPropertyName = "Url",
                HeaderText = "Url",
                Visible = false
            };
            DataGridViewTextBoxColumn dgcolSource = new DataGridViewTextBoxColumn()
            {
                Name = "Source",
                DataPropertyName = "Source",
                HeaderText = "Source",
                Width = 100
            };
            DataGridViewTextBoxColumn dgcolPublished = new DataGridViewTextBoxColumn()
            {
                Name = "Published",
                DataPropertyName = "Published",
                HeaderText = "Published",
                Width = 120
            };
            gridNews.Columns.Add(dgcolTicker);
            gridNews.Columns.Add(dgcolTitle);
            gridNews.Columns.Add(dgcolUrl);
            gridNews.Columns.Add(dgcolSource);
            gridNews.Columns.Add(dgcolPublished);
            gridNews.CellContentClick += GridNews_CellContentClick;
            gridNews.Sort(gridNews.Columns["Ticker"], ListSortDirection.Ascending);
        }

        private void GridNews_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            else if (gridNews.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewLinkCell)
            {
                DataRowView drv = gridNews.Rows[e.RowIndex].DataBoundItem as DataRowView;
                if (drv != null)
                {
                    string colLink = drv["Url"].ToString();
                    System.Diagnostics.Process.Start(colLink);
                }
            }
        }

        public DataTable DataSource
        {
            get
            {
                return _newsTable;
            }
            set
            {
                _newsTable = value;
            }
        }

        private void Export_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = String.Format("{0}-{1}.csv", DateTime.Today.ToString("yyyy-MM-dd"), this.Parent.Text);
                sfd.Filter = "Csv files (*.csv)|*.csv";
                sfd.OverwritePrompt = true;
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    IEnumerable<string> columnNames = DataSource.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
                    sb.AppendLine(string.Join(@""",""", columnNames));
                    foreach (DataRow row in DataSource.Rows)
                    {
                        IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                        sb.AppendLine("\"" + string.Join(@""",""", fields) + "\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString());
                    System.Diagnostics.Process.Start(sfd.FileName);

                }
            }
        }

        private void ExportHtml_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = String.Format("{0}-{1}.html", DateTime.Today.ToString("yyyy-MM-dd"), this.Parent.Text);
                sfd.Filter = "Excel files (*.html)|*.html";
                sfd.OverwritePrompt = true;
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("<html>");
                    sb.AppendLine("<head>");
                    sb.AppendLine("<style type='text/css'>");
                    sb.AppendLine("html * { font-family: Tahoma, serif; font-size:8pt; }");
                    sb.AppendLine("table { border: 1px solid #666666; }");
                    sb.AppendLine("</style>");
                    sb.AppendLine("</head>");
                    sb.AppendLine("<body>");

                    sb.AppendLine("<table>");
                    sb.AppendLine("<tr>");
                    sb.Append("<td>Ticker</td>");
                    sb.Append("<td>Title</td>");
                    sb.Append("<td>Published</td>");
                    sb.AppendLine("</tr>");

                    foreach (DataRow row in DataSource.Rows)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendFormat("<td>{0}</td>", row["Ticker"]);
                        sb.AppendFormat("<td><a href='{0}'>{1}</a></td>", row["Url"], row["Title"]);
                        sb.AppendFormat("<td>{0}</td>", row["Published"]);
                        sb.AppendLine("</tr>");
                    }

                    sb.AppendLine("</table>");

                    sb.AppendLine("</body>");
                    sb.AppendLine("</html>");
                    File.WriteAllText(sfd.FileName, sb.ToString());
                    System.Diagnostics.Process.Start(sfd.FileName);

                }
            }
        }
    }
}
