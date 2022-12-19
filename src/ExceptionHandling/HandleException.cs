using System;
using System.Text;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Azure.ApiWrapper.GraphMail;
using Utility.Data.Reports;
using Utility.Logging.TimerLogs;
using Utility.Web.InputProcessors;

namespace Utility.AzFunction
{
    public class ExceptionHandling
    {
        public static BadRequestObjectResult HandleException(Exception e, HttpRequest req, TimerLog debugLog, List<string> eventLog)
        {
            try
            {
                if (eventLog != null) { eventLog.Add("Ran Into Exception"); }
                if (debugLog != null && debugLog.running) { debugLog.End(); }

                var report = new Report();

                if (debugLog != null) { report.Add(debugLog.GetHeader(), null, debugLog.GetTable()); }
                    else {report.Prepend("Debug Log not initialized", null, null);}

                if (eventLog != null) { report.Add("Event Log", String.Join(Environment.NewLine, eventLog), null); }
                    else {report.Prepend("Event Log not initialized", null, null);}
                
                report.Prepend("Exception", e.ToString(), null);
                SendExceptionEmail(req, report);
                return new BadRequestObjectResult(report.Collate(Report.SerializeType.Console));
            }
            catch (Exception g)
            {
                return new BadRequestObjectResult("Exception happened in the exception handling: \n" + g.ToString());
            }
        }

        public static void SendExceptionEmail(HttpRequest req, Report report)
        {   
            try
            {
                AzureGraphMailClient mailClient = null; 
                
                string emailAddressesRaw = null;
                string[] emailAddresses = null;
                string emailAddressesDebugRaw = null;
                string[] emailAddressesDebug = null;

                var ip = new InputProcessor(req);
                string sendEmailRaw = ip.AddAppSettingInput("sendEmail", "false");
                string sendEmailDebugRaw = ip.AddAppSettingInput("sendEmailDebug", "false");
                string debugReportOnlyOverrideRaw = ip.AddQueryInput("debugReportOnlyOverride", "false");
                ip.ValidateInputs();
                bool sendEmail = bool.Parse(sendEmailRaw);
                bool sendEmailDebug = bool.Parse(sendEmailDebugRaw);
                bool debugReportOnlyOverride = bool.Parse(debugReportOnlyOverrideRaw);

                if(debugReportOnlyOverride)
                {
                    sendEmail = false;
                    sendEmailDebug = true;
                }

                if(sendEmail || sendEmailDebug)
                {
                    string mailFromUserID = ip.AddAppSettingInput("mailFromUserID");
                    ip.ValidateInputs();
                    mailClient = new AzureGraphMailClient(mailFromUserID); 
                }

                if(sendEmail)
                {
                    emailAddressesRaw = ip.AddAppSettingInput("emailAddresses");
                    emailAddresses = emailAddressesRaw.Split(',');
                    ip.ValidateInputs();
                    mailClient.SendReportEmail("Exception in CleanupRoleAssignmentsOfDeletedUsers", emailAddresses, report.Collate(Report.SerializeType.Html));
                }

                if(sendEmailDebug)
                {
                    emailAddressesDebugRaw = ip.AddAppSettingInput("emailAddressesDebug");
                    emailAddressesDebug = emailAddressesDebugRaw.Split(',');
                    ip.ValidateInputs();
                    mailClient.SendReportEmail("Exception in CleanupRoleAssignmentsOfDeletedUsers", emailAddressesDebug, report.Collate(Report.SerializeType.Html));
                }
            }
            catch (Exception f)
            {
                report.Prepend("Exception When attempting to send email Exception", f.ToString(), null);
            }
        }
    }
}