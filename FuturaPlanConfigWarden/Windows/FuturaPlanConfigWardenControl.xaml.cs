using EnvDTE;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;

namespace FuturaPlanConfigWarden.Windows {
    public partial class FuturaPlanConfigWardenControl : UserControl {

        public ObservableCollection<string> DatabaseList { get; set; }

        public string SelectedDatabase { get; set; }
        public bool OverrideConnectionString { get; set; } = true;


        private WindowState _state;

        private ProjectItem m_appConfig;
        private ProjectItem AppConfig {
            get {
                return m_appConfig;
            }

            set {
                m_appConfig = value;
            }
        }


        private XDocument AppConfigTemplate { get; set; }

        private string AppConfigPath { get; set; }

        private string m_originalConnectionString { get; } = @"Data Source=(LOCAL)\SQLEXPRESS;Initial Catalog=FuturaPlan;Trusted_Connection=True;MultipleActiveResultSets=True";
        private string m_overrideConnectionString { get; } = "Data Source=(LOCAL);Initial Catalog={0};Trusted_Connection=True;MultipleActiveResultSets=True";
        
        // These objects can be GCd and lose their listeners if we dont keep a reference to them.
        private BuildEvents m_buildEvents;
        private SolutionEvents m_solutionEvents;

        public FuturaPlanConfigWardenControl(WindowState state) {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            _state = state;
            DatabaseList = new ObservableCollection<string>();

            DataContext = this;

            m_buildEvents = _state.DTE.Events.BuildEvents;
            m_solutionEvents = _state.DTE.Events.SolutionEvents;

            m_buildEvents.OnBuildBegin += BuildStart;
            m_buildEvents.OnBuildDone += BuildEnd;

            m_solutionEvents.Opened += () => {

                if (AppConfig != null) {
                    return;
                }

                if (TryGetAppConfig(out ProjectItem aconf)) {
                    InitializeAppConfig(aconf);
                }

            };

            if (TryGetAppConfig(out ProjectItem aconf)) {
                InitializeAppConfig(aconf);
            }

            SetConnectionString(m_originalConnectionString);

            InitializeComponent();
        }

        private void InitializeAppConfig(ProjectItem aconf) {

            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            AppConfig = aconf;
            foreach (Property prop in AppConfig.Properties) {
                if (prop.Name == "LocalPath") {
                    AppConfigPath = prop.Value as string;
                }
            }

            AppConfigTemplate = XDocument.Load(AppConfigPath);
        }

        private void BuildStart(vsBuildScope Scope, vsBuildAction Action) {

            if (!OverrideConnectionString) {
                return;
            }

            switch (Action) {
                case vsBuildAction.vsBuildActionBuild:
                case vsBuildAction.vsBuildActionRebuildAll:

                    if (string.IsNullOrEmpty(SelectedDatabase)) {
                        return;
                    }

                    SetConnectionString(string.Format(m_overrideConnectionString, SelectedDatabase));
                    break;
                default:
                    break;
            }
        }

        private void BuildEnd(vsBuildScope Scope, vsBuildAction Action) {

            if (!OverrideConnectionString) {
                return;
            }

            switch (Action) {
                case vsBuildAction.vsBuildActionBuild:
                case vsBuildAction.vsBuildActionRebuildAll:
                    SetConnectionString(m_originalConnectionString);
                    break;
                default:
                    break;
            }
        }

        private void SetConnectionString(string connstring) {

            if (AppConfigTemplate == null) {
                return;
            }

            AppConfigTemplate.Root
                    .Elements("connectionStrings")
                    .Elements("add")
                    .First() // There could be more than one but we dont support that lol
                    .Attribute("connectionString")
                    .SetValue(connstring);

            AppConfigTemplate.Save(AppConfigPath);
        }

        private static string[] systemdbs = { "master", "msdb", "model", "tempdb" };
        public List<string> GetDatabaseList() {
            List<string> list = new List<string>();
            using (SqlConnection con = new("Server=localhost;Database=master;Trusted_Connection=True;")) {
                con.Open();

                using SqlCommand cmd = new SqlCommand("SELECT name from sys.databases", con);
                using IDataReader dr = cmd.ExecuteReader();

                while (dr.Read()) {
                    string db = dr[0].ToString();

                    // Dont add the system databases.
                    if (systemdbs.Any(x => x == db)) {
                        continue;
                    }

                    list.Add(dr[0].ToString());
                }
            }

            return list;
        }

        public bool TryGetAppConfig(out ProjectItem appConfig) {

            Array loadedProjects;
            appConfig = null;

            try
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                loadedProjects = (Array)_state.DTE.ActiveSolutionProjects;
            }
            catch (Exception)
            {
                return false;
            }

            foreach (Project project in loadedProjects) {
                for (int i = 0; i < project.ProjectItems.Count; i++) {
                    foreach (ProjectItem item in project.ProjectItems) {
                        if (item.Name == "App.config") {
                            appConfig = item;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private RelayCommand<object> m_refreshDatabaseListCommand;
        public RelayCommand<object> RefreshDatabaseListCommand {
            get {
                return m_refreshDatabaseListCommand ??= new RelayCommand<object>(_ => {
                    DatabaseList.Clear();
                    foreach (string dbname in GetDatabaseList()) {
                        DatabaseList.Add(dbname);
                    }
                }, _ => { 
                    return DatabaseList != null; 
                });
            }
        }
    }
}