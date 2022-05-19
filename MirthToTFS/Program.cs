using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Services.Common;
using MirthConnectFX;
using MirthConnectFX.Model;
using MirthToTFS;
using MirthToTFS.Models.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MirthConnect
{
    class Program
    {
        private static string domain = "";    // domain of user

        // TFS Configurations
        /*
         * tfsLocation -    Location TFS Server is at
         * tfsUser -        TFS User (Typically AD account)
         * tfsPassword -    TFS Password
         * workspaceText -  Workspace changes will be attached to
         */
        private static string tfsLocation = "http://{ip}:8080/tfs/";
        private static string tfsUser = "";
        private static string tfsPassword = "";
        private static string workspaceText = "MirthVersionControl";

        // Mirth Credentials
        private static string mirthUser = "admin";
        private static string mirthPass = "admin";

        private static DateTime startDate;
        private static DateTime endDate;
        private static string ImplementationFolder = @"C:\MirthVersionControl\OnePartner\Mirth";

        static void Main(string[] args)
        {
            DateTime lastRun;
            startDate = new DateTime(2022, 05, 19, 0, 0, 0);
            lastRun = startDate;
            var fileLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            while (true)
            {
                startDate = lastRun;
                endDate = DateTime.Now;

                SaveChangesForMirthServer(MirthServers.PROD_HOSTED);
                SaveChangesForMirthServer(MirthServers.TEST_NEWHOSTED);
                lastRun = endDate;
                Thread.Sleep(5000);
            }
        }

        private static void SaveChangesForMirthServer(string mirthServer)
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = MirthConnectClient.Create(mirthServer);   //Replace with setting in app.config

            var session = client.Login(mirthUser, mirthPass, "0.0.0");

            var events = client.Events.GetEvents(null, null, startDate, endDate, null, 100, null, "", "Update", "SUCCESS");

            if (events.Count > 0)
            {

                var listOfChannelsChanged = new List<EventDescription>();
                var listOfTemplatesChanged = new List<EventDescription>();
                foreach (var e in events)
                {
                    foreach (var entry in e.Attributes.Entries)
                    {
                        if (entry.Key == "updatedCodeTemplates")
                        {
                            listOfTemplatesChanged.Add(new EventDescription(entry.Value));
                        }
                        else if (entry.Key == "channel")
                        {
                            listOfChannelsChanged.Add(new EventDescription(entry.Value));
                        }
                    }
                }

                var filesToUpload = new List<string>();
                var channelsUpdated = new List<string>();
                var codeTemplatesUpdated = new List<string>();
                foreach (var eventDesc in listOfChannelsChanged.Distinct())
                {
                    if (!String.IsNullOrEmpty(eventDesc.Name))
                    {
                        var channel = client.Channels.GetRawChannelXML(eventDesc.Id);
                        channelsUpdated.Add(channel);

                        var fileUpload = ImplementationFolder + $"\\{mirthServer}\\MirthChannels\\" + eventDesc.Name + ".xml";

                        filesToUpload.Add(fileUpload);
                        System.IO.File.WriteAllText(fileUpload, channel);
                    }
                }

                foreach (var eventDesc in listOfTemplatesChanged.Distinct())
                {
                    if (!String.IsNullOrEmpty(eventDesc.Name))
                    {
                        var template = client.CodeTemplates.GetRawCodeTemplateXML(eventDesc.Id);
                        codeTemplatesUpdated.Add(template);

                        var fileUpload = ImplementationFolder + $"\\{mirthServer}\\MirthCodeTemplates\\" + eventDesc.Name + ".xml";
                        filesToUpload.Add(fileUpload);
                        System.IO.File.WriteAllText(fileUpload, template);
                    }
                }

                // Add to tfs with name of channel and code template
                NetworkCredential credential = new NetworkCredential(tfsUser, tfsPassword, domain);

                // Get a reference to our Team Foundation Server.
                using (var tpc = new TfsTeamProjectCollection(new Uri(tfsLocation), credential))
                {
                    tpc.Connect(Microsoft.TeamFoundation.Framework.Common.ConnectOptions.IncludeServices);

                    // Get a reference to Version Control.
                    VersionControlServer versionControl = tpc.GetService<VersionControlServer>();

                    // Create a workspace.
                    //Workspace workspace = versionControl.CreateWorkspace("MirthVersionControl", versionControl.AuthorizedUser);
                    Workspace workspace = versionControl.GetWorkspace(workspaceText, versionControl.AuthorizedUser);
                    Workstation.Current.EnsureUpdateWorkspaceInfoCache(versionControl, versionControl.AuthenticatedUser);
                    foreach (var fileName in filesToUpload)
                    {
                        workspace.PendAdd(fileName, true);
                    }

                    var checkinComment = "";
                    PendingChange[] pendingChanges = workspace.GetPendingChanges();
                    Console.WriteLine("  Your current pending changes:");
                    foreach (PendingChange pendingChange in pendingChanges)
                    {
                        Console.WriteLine("    path: " + pendingChange.LocalItem +
                                          ", change: " + PendingChange.GetLocalizedStringForChangeType(pendingChange.ChangeType));
                        checkinComment += pendingChange.FileName.Replace(".xml", "");

                        if (pendingChange != pendingChanges.LastOrDefault())
                            checkinComment += ", ";
                    }

                    Console.WriteLine("\r\n— Checkin the items we added.\r\n");
                    try
                    {
                        int changesetNumber = workspace.CheckIn(pendingChanges, "Updates made to " + checkinComment);
                        Console.WriteLine("  Checked in changeset " + changesetNumber);

                    }
                    catch (Exception ex)
                    {
                        //TODO: change check-in comment to be descriptive
                        //TODO: create a folder for each mirth server
                        //TODO: ensure that it only checks in items in a specific folder
                        Console.WriteLine(ex);
                    }


                    try
                    {
                        //workspace.Map("$/Implementation/HIE/OnePartner/MirthChannels", ImplementationFolder + @"\HIE\OnePartner\MirthChannels");
                    }
                    finally
                    {
                    }
                }
            }
        }

        private static void DisplayAllChannelStatus(IMirthConnectClient client)
        {
            var status = client.ChannelStatus.GetChannelStatus();
            foreach (var channelStatus in status)
                Console.Write("{0}\r\n ({1}) {2}\r\n\r\n", channelStatus.Name, channelStatus.ChannelId, channelStatus.State);
        }

        private static void DisplayChannelStatus(IMirthConnectClient client, string channelId)
        {
            var status = client.ChannelStatus.GetChannelStatus();
            var channel = status.Single(x => x.ChannelId == channelId);

            Console.Write("{0}\r\n ({1}) {2}\r\n\r\n", channel.Name, channel.ChannelId, channel.State);
        }
    }
}
