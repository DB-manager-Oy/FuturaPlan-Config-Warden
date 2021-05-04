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
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FuturaPlanConfigWarden.Windows {
    public partial class FuturaPlanConfigWardenControl : UserControl, INotifyPropertyChanged {

        public ObservableCollection<string> DatabaseList { get; set; }

        private string m_selectedDatabase;
        public string SelectedDatabase {
            get {
                return m_selectedDatabase;
            }
            set {
                m_selectedDatabase = value;
                m_dbCache.Update(DatabaseList, value); // fite me irl
                OnPropertyChanged();
            }
        }
        public bool OverrideConnectionString { get; set; } = true;

        private DbCache m_dbCache;

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

        private XAttribute ConnectionStringElement {
            get {
                return AppConfigTemplate.Root
                        .Elements("connectionStrings")
                        .Elements("add")
                        .First() // There could be more than one but we dont support that lol
                        .Attribute("connectionString");
            }
        }

        private string AppConfigPath { get; set; }

        private string m_originalConnectionString { get; set; } = @"Data Source=(LOCAL)\SQLEXPRESS;Initial Catalog=FuturaPlan;Trusted_Connection=True;MultipleActiveResultSets=True";
        private string m_overrideConnectionString { get; } = "Data Source=(LOCAL);Initial Catalog={0};Trusted_Connection=True;MultipleActiveResultSets=True";

        // These objects can be GCd and lose their listeners if we dont keep a reference to them.
        private BuildEvents m_buildEvents;
        private SolutionEvents m_solutionEvents;

        public FuturaPlanConfigWardenControl(WindowState state) {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            _state = state;

            m_dbCache = new DbCache();
            if (m_dbCache.TryGetCacheData(out ObservableCollection<string> data, out string active)) {
                DatabaseList = data;
                SelectedDatabase = active;
            } else {
                DatabaseList = new ObservableCollection<string>();
            }

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
                    SetAppConfig(aconf);
                    m_originalConnectionString = ConnectionStringElement.Value;
                }

            };

            if (TryGetAppConfig(out ProjectItem aconf)) {
                SetAppConfig(aconf);
                m_originalConnectionString = ConnectionStringElement.Value;
            }

            SetConnectionString(m_originalConnectionString);

            InitializeComponent();
        }

        private void SetAppConfig(ProjectItem aconf) {
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

                    // Reload the template before every build so that modifications to the file dont get lost.
                    AppConfigTemplate = XDocument.Load(AppConfigPath);

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

            ConnectionStringElement.SetValue(connstring);

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

            try {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                loadedProjects = (Array)_state.DTE.ActiveSolutionProjects;
            } catch (Exception) {
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

        private void RestoreDatabase(string database) {

            OpenFileDialog fileDialog = new();

            if (fileDialog.ShowDialog() == false) {
                return;
            }

            using (SqlConnection cn = new("Server=localhost;Database=master;Trusted_Connection=True;")) {
                cn.Open();
                cn.StatisticsEnabled = true;

                #region step 1 SET SINGLE_USER WITH ROLLBACK
                string sql = "IF DB_ID('" + database + "') IS NOT NULL ALTER DATABASE [" + database + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                using (var command = new SqlCommand(sql, cn)) {
                    command.ExecuteNonQuery();
                }
                #endregion

                #region step 2 Restore
                sql = "RESTORE DATABASE [" + database + "] FROM DISK='" + fileDialog.FileName + "' WITH FILE = 1, NOUNLOAD";
                using (var command = new SqlCommand(sql, cn)) {
                    command.ExecuteNonQuery()
                }
                #endregion

                #region step 3 SET MULTI_USER
                sql = "ALTER DATABASE [" + database + "] SET MULTI_USER";
                using (var command = new SqlCommand(sql, cn)) {
                    command.ExecuteNonQuery();
                }
                #endregion
            }

            MessageBox.Show($"Restored {database} succesfully.", "Database restore");
        }

        private RelayCommand<object> m_refreshDatabaseListCommand;
        public RelayCommand<object> RefreshDatabaseListCommand {
            get {

                return m_refreshDatabaseListCommand ??= new RelayCommand<object>(_ => {
                    DatabaseList.Clear();

                    foreach (string dbname in GetDatabaseList().OrderBy(x => x)) {
                        DatabaseList.Add(dbname);
                    }

                    if (string.IsNullOrEmpty(SelectedDatabase)) {
                        SelectedDatabase = DatabaseList[0];
                    }

                    m_dbCache.Update(DatabaseList, SelectedDatabase);

                }, _ => DatabaseList != null);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private RelayCommand<object> m_restoreDatabaseCommand;
        public RelayCommand<object> RestoreDatabaseCommand {
            get {
                return m_restoreDatabaseCommand ??= new RelayCommand<object>(_ => {
                    RestoreDatabase(SelectedDatabase);
                }, _ => !string.IsNullOrEmpty(SelectedDatabase));
            }
        }
    }
}