﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MarkdownMonster;

using Westwind.Utilities.Configuration;

namespace WeblogAddin
{
    //public class xWeblogApp
    //{
    //    public static WeblogAddinConfiguration Current;


    //    static WeblogApp()
    //    {
    //        Current = new WeblogAddinConfiguration();
    //        Current.Initialize();
    //    }
    //}

    public class WeblogAddinConfiguration : AppConfiguration, INotifyPropertyChanged
    {
        public static WeblogAddinConfiguration Current;

        static WeblogAddinConfiguration()
        {
            Current = new WeblogAddinConfiguration();
            Current.Initialize();
        }

        public WeblogAddinConfiguration()
        {
            Weblogs = new Dictionary<string, WeblogInfo>();
        }



        public Dictionary<string,WeblogInfo> Weblogs
        {
            get { return _weblogs; }
            set
            {
                if (value == _weblogs) return;
                _weblogs = value;
                OnPropertyChanged(nameof(Weblogs));
            }
        }
        private Dictionary<string,WeblogInfo> _weblogs;
        


        public string LastWeblogAccessed
        {
            get { return _lastWeblogAccessed; }
            set
            {
                if (value == _lastWeblogAccessed) return;
                _lastWeblogAccessed = value;
                OnPropertyChanged(nameof(LastWeblogAccessed));
            }
        }
        private string _lastWeblogAccessed;


        public string PostsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_postsFolder))
                {
                    var basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "DropBox");
                    if (!Directory.Exists(basePath))
                        basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    basePath = basePath + "\\Markdown Monster Weblog Posts";
                    if (!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);
                    _postsFolder = basePath;
                }
                return _postsFolder;
            }
            set
            {
                _postsFolder = value;
            }
        }
        private string _postsFolder;


        /// <summary>
        /// When true renders links to open externally.
        /// </summary>
        public bool RenderLinksOpenExternal
        {
            get { return _RenderLinksOpenExternal; }
            set
            {
                if (value == _RenderLinksOpenExternal) return;
                _RenderLinksOpenExternal = value;
                OnPropertyChanged(nameof(RenderLinksOpenExternal));
            }
        }
        private bool _RenderLinksOpenExternal = true;

        
        protected override IConfigurationProvider OnCreateDefaultProvider(string sectionName, object configData)
        {
            var provider = new JsonFileConfigurationProvider<WeblogAddinConfiguration>()
            {
                JsonConfigurationFile = Path.Combine(mmApp.Configuration.CommonFolder, "WebLogAddIn.json")                                
            };

            if (!File.Exists(provider.JsonConfigurationFile))
            {
                if (!Directory.Exists(Path.GetDirectoryName(provider.JsonConfigurationFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(provider.JsonConfigurationFile));
            }

            return provider;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}