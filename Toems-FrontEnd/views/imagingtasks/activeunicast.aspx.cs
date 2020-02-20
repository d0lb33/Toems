﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Toems_FrontEnd.views.imagingtasks
{
    public partial class activeunicast : BasePages.ImagingTask
    {
        protected void btnCancel_Click(object sender, EventArgs e)
        {
            var control = sender as Control;
            if (control != null)
            {
                var gvRow = (GridViewRow)control.Parent.Parent;
                var dataKey = gvUcTasks.DataKeys[gvRow.RowIndex];
                if (dataKey != null)

                    Call.ActiveImagingTaskApi.Delete(Convert.ToInt32(dataKey.Value));
            }
            PopulateGrid();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            ViewState["clickTracker"] = "1";
            PopulateGrid();
            lblTotal.Text = Call.ActiveImagingTaskApi.GetActiveUnicastCount() + " Total Unicast(s)";
        }

        private void PopulateGrid()
        {
            gvUcTasks.DataSource = Call.ActiveImagingTaskApi.GetUnicasts();
            gvUcTasks.DataBind();
        }

        protected void Timer_Tick(object sender, EventArgs e)
        {
            PopulateGrid();
            lblTotal.Text = Call.ActiveImagingTaskApi.GetActiveUnicastCount() + " Total Unicast(s)";
            UpdatePanel1.Update();
        }
    }
}