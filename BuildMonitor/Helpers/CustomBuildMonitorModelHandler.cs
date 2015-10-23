using BuildMonitor.Models.Home;
using BuildMonitor.Models.Home.Settings;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Xml.Serialization;

namespace BuildMonitor.Helpers
{
    public class CustomBuildMonitorModelHandler : BuildMonitorModelHandlerBase
    {
        private Settings settings;

        public CustomBuildMonitorModelHandler()
        {
            InitializeSettings();
        }

        public override BuildMonitorViewModel GetModel()
        {
            var model = new BuildMonitorViewModel();

            GetTeamCityBuildsJson();

            foreach (var group in settings.Groups)
            {
                var project = new Project();
                project.Name = group.Name;

                AddBuilds(ref project, group);

                model.Projects.Add(project);
            }

            return model;
        }

        private void AddBuilds(ref Project project, Group group)
        {
            foreach (var job in group.Jobs)
            {
                var buildTypeJson = GetJsonBuildTypeById(job.Id);

                var build = new Build();
                build.Id = buildTypeJson.id;
                build.Name = job.Text ?? buildTypeJson.name;

                var url = string.Format(buildStatusUrl, build.Id);
                var buildStatusJsonString = RequestHelper.GetJson(url);
                buildStatusJson = JsonConvert.DeserializeObject<dynamic>(buildStatusJsonString ?? string.Empty);

                build.Branch = buildStatusJson.branchName ?? "default";
                build.Status = GetBuildStatusForRunningBuild(build.Id);

                if (build.Status == BuildStatus.Running)
                {
                    UpdateBuildStatusFromRunningBuildJson(build.Id);
                }
                if (build.Status == BuildStatus.Failure)
                {
                    build.FailedTests = buildStatusJson.testOccurrences.failed ?? 0;
                }

                build.UpdatedBy = GetUpdatedBy();
                build.LastRunText = GetLastRunText();
                build.IsQueued = IsBuildQueued(build.Id);

                if (build.Status == BuildStatus.Running)
                {
                    var result = GetRunningBuildBranchAndProgress(build.Id);
                    build.Branch = result[0];
                    build.Progress = result[1];
                }
                else
                {
                    build.Progress = string.Empty;
                }

                project.Builds.Add(build);
            }
        }

        private dynamic GetJsonBuildTypeById(string id)
        {
            var count = (int)buildTypesJson.count;
            for (int i = 0; i < count; i++)
            {
                if (buildTypesJson.buildType[i].id == id)
                {
                    return buildTypesJson.buildType[i];
                }
            }

            return null;
        }

        private string GetUpdatedBy()
        {
            try
            {
                if ((string)buildStatusJson.triggered.type == "user")
                {
                    return (string)buildStatusJson.triggered.user.name;
                }
                if ((string)buildStatusJson.triggered.type == "vcs")
                {
                    var numChanges = (int)buildStatusJson.lastChanges.count;
                    return (string)buildStatusJson.lastChanges.change[numChanges - 1].username;
                }
                else if ((string)buildStatusJson.triggered.type == "unknown")
                {
                    return "TeamCity";
                }
                else
                {
                    return "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private void InitializeSettings()
        {
            if (settings != null)
            {
                return;
            }

            var path = AppDomain.CurrentDomain.BaseDirectory + "/App_Data/Settings.xml";
            using (var reader = new StreamReader(path))
            {
                var serializer = new XmlSerializer(typeof(Settings));
                settings = (Settings)serializer.Deserialize(reader);
            }
        }

        private bool IsBuildQueued(string buildId)
        {
            try
            {
                var count = (int)buildQueueJson.count;
                for (int i = 0; i < count; i++)
                {
                    var build = buildQueueJson.build[i];

                    if (buildId == (string)build.buildTypeId && (string)build.state == "queued")
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}