﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Toems_FrontEnd.views.imagingtasks
{
    public partial class active : BasePages.ImagingTask
    {
        protected void btnCancel_Click(object sender, EventArgs e)
        {
            var control = sender as Control;
            if (control != null)
            {
                var gvRow = (GridViewRow)control.Parent.Parent;
                var dataKey = gvTasks.DataKeys[gvRow.RowIndex];
                if (dataKey != null)

                    Call.ActiveImagingTaskApi.Delete(Convert.ToInt32(dataKey.Value));
            }
            gvTasks.DataSource = Call.ActiveImagingTaskApi.GetActiveTasks();
            gvTasks.DataBind();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Call.ToemsUserApi.IsAdmin(ToemsCurrentUser.Id))
            {
                lblTotalNotOwned.Visible = false;
                btnCancelAll.Visible = true;
            }
            else
            {
                lblTotalNotOwned.Text = Call.ActiveImagingTaskApi.GetActiveNotOwned() + " Task(s) Not Visible";
                lblTotalNotOwned.Visible = true;
                btnCancelAll.Visible = false;
            }

            if (IsPostBack) return;
            ViewState["clickTracker"] = "1";
            PopulateGrid();
            lblTotal.Text = Call.ActiveImagingTaskApi.GetAllActiveCount() + " Total Tasks(s)";
        }

        private void PopulateGrid()
        {
            gvTasks.DataSource = Call.ActiveImagingTaskApi.GetActiveTasks();
            gvTasks.DataBind();
        }

        protected void Timer_Tick(object sender, EventArgs e)
        {
            PopulateGrid();
            lblTotal.Text = Call.ActiveImagingTaskApi.GetAllActiveCount() + " Total Tasks(s)";
            UpdatePanel1.Update();
        }

        protected void btnCancelAll_Click(object sender, EventArgs e)
        {
            Call.ActiveImagingTaskApi.CancelAllImagingTasks();
            PopulateGrid();
        }
    }
}