using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
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
    }
}
