﻿using System;
using System.Collections.Generic;
using Toems_Common;
using Toems_Common.Entity;
using Toems_FrontEnd.BasePages;

namespace Toems_FrontEnd.views.admin
{
    public partial class views_admin_email : Admin
    {
        protected void btnTestMessage_Click(object sender, EventArgs e)
        {
            Call.SettingApi.SendEmailTest();
            EndUserMessage = "Test Message Sent";
        }

        protected void btnUpdateSettings_OnClick(object sender, EventArgs e)
        {
            var listSettings = new List<EntitySetting>
            {
                new EntitySetting
                {
                    Name = "Smtp Server",
                    Value = txtSmtpServer.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Server").Id
                },
                new EntitySetting
                {
                    Name = "Smtp Port",
                    Value = txtSmtpPort.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Port").Id
                },
                new EntitySetting
                {
                    Name = "Smtp Username",
                    Value = txtSmtpUsername.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Username").Id
                },
                new EntitySetting
                {
                    Name = "Smtp Mail From",
                    Value = txtSmtpFrom.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Mail From").Id
                },
                new EntitySetting
                {
                    Name = "Smtp Mail To",
                    Value = txtSmtpTo.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Mail To").Id
                },
                new EntitySetting
                {
                    Name = "Smtp Ssl",
                    Value = ddlSmtpSsl.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Ssl").Id
                }
            };
            if (!string.IsNullOrEmpty(txtSmtpPassword.Text))
                listSettings.Add(new EntitySetting
                {
                    Name = "Smtp Password Encrypted",
                    Value = txtSmtpPassword.Text,
                    Id = Call.SettingApi.GetSetting("Smtp Password Encrypted").Id
                });

            var chkValue = chkEnabled.Checked ? "1" : "0";
            listSettings.Add(new EntitySetting
            {
                Name = "Smtp Enabled",
                Value = chkValue,
                Id = Call.SettingApi.GetSetting("Smtp Enabled").Id
            });

            EndUserMessage = Call.SettingApi.UpdateSettings(listSettings)
                ? "Successfully Updated Settings"
                : "Could Not Update Settings";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            RequiresAuthorization(AuthorizationStrings.Administrator);
            if (IsPostBack) return;

            txtSmtpServer.Text = GetSetting(SettingStrings.SmtpServer);
            txtSmtpPort.Text = GetSetting(SettingStrings.SmtpPort);
            ddlSmtpSsl.SelectedValue = GetSetting(SettingStrings.SmtpSsl);
            txtSmtpUsername.Text = GetSetting(SettingStrings.SmtpUsername);
            txtSmtpPassword.Text = GetSetting(SettingStrings.SmtpPassword);
            txtSmtpFrom.Text = GetSetting(SettingStrings.SmtpMailFrom);
            txtSmtpTo.Text = GetSetting(SettingStrings.SmtpMailTo);

            if (GetSetting(SettingStrings.SmtpEnabled) == "1")
                chkEnabled.Checked = true;
        }
    }
}