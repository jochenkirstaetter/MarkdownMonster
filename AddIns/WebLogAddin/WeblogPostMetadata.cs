using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using MarkdownMonster;
using Newtonsoft.Json;
using WebLogAddin.MetaWebLogApi;
using Westwind.Utilities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


namespace WeblogAddin
{
    public class WeblogPostMetadata : INotifyPropertyChanged
    {
        private string _title;
        private string _abstract;
        private bool _isDraft;

               
        /// <summary>
        /// The title of the post derived from the document's header
        /// </summary>
        public string Title
        {
            get { return _title; }
            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }


        /// <summary>
        /// Url that is mapped to wp_thumbnail
        /// </summary>
        public string FeaturedImageUrl { get; set; }

        /// <summary>
        /// Featured Image Id used for Wordpress
        /// </summary>
        public string FeaturedImageId { get; set; }


        /// <summary>
        /// Short abstract text for the post.
        /// </summary>
        public string Abstract
        {
            get { return _abstract; }
            set
            {
                if (value == _abstract) return;
                _abstract = value;
                OnPropertyChanged(nameof(Abstract));
            }
        }

        

        /// <summary>
        /// Keywords that are added to the post when published.
        /// Typically these are added to the document meta data.
        /// </summary>
        public string Keywords { get; set; }

        /// <summary>
        /// Categories that this post is associated with
        /// </summary>
        public string Categories { get; set; }

        /// <summary>
        /// The Weblog that this post is to be sent to
        /// </summary>
        public string WeblogName { get; set; }

        /// <summary>
        /// The post id - empty on new entries, and set to 
        /// the id generated on the server after a post 
        /// was uploaded.
        /// </summary>
        public string PostId { get; set; }

        /// <summary>
        /// Determines whether a post is published or
        /// a unpublished draft.
        /// </summary>
        public bool IsDraft
        {
            get { return _isDraft; }
            set
            {
                if (value == _isDraft) return;
                _isDraft = value;
                OnPropertyChanged(nameof(IsDraft));
            }
        }

        /// <summary>
        /// Determines whether the addin tries to infer the featured
        /// image based on the first image in the post.
        /// 
        /// If false, only explicitly set images in the meta data
        /// are used.
        /// </summary>
        [DefaultValue(true)]
        public bool InferFeaturedImage
        {
            get { return _inferFeaturedImage; }
            set
            {
                if (value == _inferFeaturedImage) return;
                _inferFeaturedImage = value;
                OnPropertyChanged(nameof(InferFeaturedImage));
            }
        }
        private bool _inferFeaturedImage = true;

        
        /// <summary>
        /// A collection of custom fields that are uploaded to the server
        /// </summary>
        public IDictionary<string,CustomField> CustomFields { get; set;}

        
        /// <summary>
        /// This should hold the sanitized markdown text
        /// stripped of the config data.
        /// </summary>
        [YamlIgnore]
        public string MarkdownBody { get; set; }


        /// <summary>
        /// This should hold the raw markdown text retrieved
        /// from the editor which will contain the meta post data
        /// </summary>
        [YamlIgnore]
        public string RawMarkdownBody { get; set; }

        public WeblogPostMetadata()
        {
            CustomFields = new Dictionary<string, CustomField>();
        }
        


        static readonly Regex YamlExtractionRegex = new Regex("^---[\n,\r\n].*?^---[\n,\r\n]", RegexOptions.Singleline | RegexOptions.Multiline);
        
        /// <summary>
        /// Strips the Markdown Meta data from the message and populates
        /// the post structure with the meta data values.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="post"></param>
        /// <param name="weblogInfo"></param>
        public static WeblogPostMetadata GetPostYamlConfigFromMarkdown(string markdown, Post post, WeblogInfo weblogInfo)
        {
            var meta = new WeblogPostMetadata()
            {
                RawMarkdownBody = markdown,
                MarkdownBody = markdown,
                WeblogName = WeblogAddinConfiguration.Current.LastWeblogAccessed,
                CustomFields = new Dictionary<string, CustomField>()
            };

            
            if (string.IsNullOrEmpty(markdown))
                return meta;

            markdown = markdown.Trim();

            if (!markdown.StartsWith("---\n") && !markdown.StartsWith("---\r"))            
                return meta;

            string extractedYaml = null;
            //var match = YamlExtractionRegex.Match(markdown);
            var match = YamlExtractionRegex.Match(markdown);
            if (match.Success)
                extractedYaml = match.Value;

            //var extractedYaml = StringUtils.ExtractString(markdown.TrimStart(), "---\n", "\n---\n",returnDelimiters: true);
            if (string.IsNullOrEmpty(extractedYaml))
                return meta;
            
            var yaml = StringUtils.ExtractString(markdown, "---", "\n---", returnDelimiters: false).Trim();            
            var input = new StringReader(yaml);

            var deserializer = new DeserializerBuilder()
                 .IgnoreUnmatchedProperties()
                 .WithNamingConvention(new CamelCaseNamingConvention())
                 .Build();

            WeblogPostMetadata yamlMeta = null;
            try
            {
                yamlMeta = deserializer.Deserialize<WeblogPostMetadata>(input);
            }
            catch 
            {
                return meta;    
            }

            if (yamlMeta == null)
                return meta;

            meta = yamlMeta;
            meta.MarkdownBody = markdown.Replace(extractedYaml,"");
            meta.RawMarkdownBody = markdown;
            
            
            post.Title = meta.Title;
            if (!string.IsNullOrEmpty(meta.Categories))
                post.Categories = meta.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (!string.IsNullOrEmpty(meta.Keywords))
                post.Tags = meta.Keywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            post.mt_excerpt = meta.Abstract;
            post.mt_keywords = meta.Keywords;
            
            if (meta.CustomFields != null)
                post.CustomFields = meta.CustomFields.Values.ToArray();

            return meta;
        }


        public string SetPostYaml()
        {
            if (RawMarkdownBody == null)
                return RawMarkdownBody;

            string markdown = RawMarkdownBody.Trim();

            var serializer = new SerializerBuilder()
                 .WithNamingConvention(new CamelCaseNamingConvention())              
                 .Build();

            // hide fields  if none are set
            var customFields = CustomFields;
            if (CustomFields != null && CustomFields.Count < 1)
                CustomFields = null;
            if (string.IsNullOrEmpty(PostId))
                PostId = null;

            string yaml = serializer.Serialize(this);

            // reset customfields
            CustomFields = customFields;

            string extractedYaml = null;
            var match = YamlExtractionRegex.Match(markdown);
            if (match.Success)
                extractedYaml = match.Value;

            if (!string.IsNullOrEmpty(extractedYaml))
                markdown = markdown.Replace(extractedYaml, "");
            else
                markdown = "\r\n" + markdown.Trim();

            markdown = "---\r\n" + yaml + "---" +                 
                markdown;

            // strip out old meta data
            string config = StringUtils.ExtractString(markdown,
                "<!-- Post Configuration -->",
                "<!-- End Post Configuration -->",
                caseSensitive: false, allowMissingEndDelimiter: true, returnDelimiters: true);

            if (!string.IsNullOrEmpty(config))
                markdown = markdown.Replace(config, "");

            return markdown;
        }

        /// <summary>
        /// Strips the Markdown Meta data from the message and populates
        /// the post structure with the meta data values.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="post"></param>
        /// <param name="weblogInfo"></param>
        /// <returns></returns>
        public static WeblogPostMetadata GetPostConfigFromMarkdown(string markdown, Post post, WeblogInfo weblogInfo)
        {
            var meta = new WeblogPostMetadata()
            {
                RawMarkdownBody = markdown,
                MarkdownBody = markdown,
                WeblogName = WeblogAddinConfiguration.Current.LastWeblogAccessed,
                CustomFields = new Dictionary<string, CustomField>()
            };

            // check for title in first line and remove it 
            // since the body shouldn't render the title
            var lines = StringUtils.GetLines(markdown, 20);
            if (lines.Length > 2 && lines[0] == "---")
            {
                var block = mmFileUtils.ExtractString(meta.MarkdownBody, "---", "\n---", returnDelimiters: true);
                if (!string.IsNullOrEmpty(block))
                {
                    meta = WeblogPostMetadata.GetPostYamlConfigFromMarkdown(markdown, post, weblogInfo);
                    meta.RawMarkdownBody = markdown;
                    if (string.IsNullOrEmpty(meta.WeblogName))
                        meta.WeblogName = WeblogAddinConfiguration.Current.LastWeblogAccessed;

                    meta.MarkdownBody = meta.MarkdownBody.Replace(block, "").Trim();


                }
            }
            else if (lines.Length > 0 && lines[0].StartsWith("# "))
            {
                if (weblogInfo.Type != WeblogTypes.Medium) // medium wants the header in the text
                    meta.MarkdownBody = meta.MarkdownBody.Replace(lines[0], "").Trim();

                meta.Title = lines[0].Trim().Substring(2);
            }


            string config = StringUtils.ExtractString(markdown,
                "<!-- Post Configuration -->",
                "<!-- End Post Configuration -->",
                caseSensitive: false, allowMissingEndDelimiter: true, returnDelimiters: true);

            if (string.IsNullOrEmpty(config))
                return meta;

            // strip the config section
            meta.MarkdownBody = meta.MarkdownBody.Replace(config, "");


            string title = StringUtils.ExtractString(config, "\n<title>", "</title>").Trim();
            if (string.IsNullOrEmpty(meta.Title))
                meta.Title = title;
            meta.Abstract = StringUtils.ExtractString(config, "\n<abstract>", "\n</abstract>").Trim();
            meta.Keywords = StringUtils.ExtractString(config, "\n<keywords>", "\n</keywords>").Trim();
            meta.Categories = StringUtils.ExtractString(config, "\n<categories>", "\n</categories>").Trim();
            meta.PostId = StringUtils.ExtractString(config, "\n<postid>", "</postid>").Trim();
            string strIsDraft = StringUtils.ExtractString(config, "\n<isDraft>", "</isDraft>").Trim();
            if (strIsDraft != null && strIsDraft == "True")
                meta.IsDraft = true;
            string weblogName = StringUtils.ExtractString(config, "\n<weblog>", "</weblog>").Trim();
            if (!string.IsNullOrEmpty(weblogName))
                meta.WeblogName = weblogName;

            string inferFeaturedImage = StringUtils.ExtractString(config, "\n<inferFeaturedImage>", "</inferFeaturedImage>");
            if (!string.IsNullOrEmpty(inferFeaturedImage))
                meta.InferFeaturedImage = inferFeaturedImage != "False" && inferFeaturedImage != "false";

            string featuredImageUrl = StringUtils.ExtractString(config, "\n<featuredImage>", "</featuredImage>");
            if (!string.IsNullOrEmpty(featuredImageUrl))
                meta.FeaturedImageUrl = featuredImageUrl.Trim();

            string featuredImageId = StringUtils.ExtractString(config, "\n<featuredImageId>", "</featuredImageId>");
            if (!string.IsNullOrEmpty(featuredImageId))
                meta.FeaturedImageId = featuredImageId.Trim();


            string customFieldsString = StringUtils.ExtractString(config, "\n<customFields>", "</customFields>", returnDelimiters: true);
            if (!string.IsNullOrEmpty(customFieldsString))
            {

                try
                {
                    var dom = new XmlDocument();
                    dom.LoadXml(customFieldsString);

                    foreach (XmlNode child in dom.DocumentElement.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element)
                        {
                            var key = child.FirstChild.InnerText;
                            var value = child.ChildNodes[1].InnerText;
                            string id = null;
                            if (child.ChildNodes.Count > 2)
                                id = child.ChildNodes[2].InnerText;

                            meta.CustomFields.Add(key, new CustomField { Key = key, Value = value, Id = id });
                        }
                    }
                }
                catch { }
            }

            post.Title = meta.Title;
            post.Categories = meta.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            post.Tags = meta.Keywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            post.mt_excerpt = meta.Abstract;
            post.mt_keywords = meta.Keywords;

            return meta;
        }

        /// <summary>
        /// This method sets the RawMarkdownBody
        /// </summary>
        /// <param name="meta"></param>
        /// <returns>Updated Markdown - also sets the RawMarkdownBody and MarkdownBody</returns>
        public string xSetConfigInMarkdown()
        {
            var meta = this;
            string markdown = meta.RawMarkdownBody;


            string customFields = null;
            if (meta.CustomFields != null && meta.CustomFields.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("<customFields>");
                foreach (var cf in meta.CustomFields)
                {
                    sb.AppendLine("\t<customField>");
                    sb.AppendLine($"\t\t<key>{cf.Key}</key>");
                    sb.AppendLine($"\t\t<value>{System.Net.WebUtility.HtmlEncode(cf.Value.Value)}</value>");
                    if (!string.IsNullOrEmpty(cf.Value.Id))
                        sb.AppendLine($"\t\t<id>{cf.Value.Id}</id>");
                    sb.AppendLine("\t</customField>");
                }
                sb.AppendLine("</customFields>");
                customFields = sb.ToString();
            }

            string origConfig = StringUtils.ExtractString(markdown, "<!-- Post Configuration -->", "<!-- End Post Configuration -->", false, false, true);
            string newConfig = $@"<!-- Post Configuration -->
<!--
```xml
<blogpost>
<title>{meta.Title}</title>
<abstract>
{meta.Abstract}
</abstract>
<categories>
{meta.Categories}
</categories>
<keywords>
{meta.Keywords}
</keywords>
<isDraft>{meta.IsDraft}</isDraft>
<weblogs>
<postid>{meta.PostId}</postid>
<weblog>
{meta.WeblogName}
</weblog>
</weblogs>
<inferFeaturedImage>{meta.InferFeaturedImage}</inferFeaturedImage>
<featuredImage>{meta.FeaturedImageUrl}</featuredImage>
<featuredImageId>{meta.FeaturedImageId}</featuredImageId>{customFields}
</blogpost>
```
-->
<!-- End Post Configuration -->";

            if (string.IsNullOrEmpty(origConfig))
            {
                markdown += "\r\n" + newConfig;
            }
            else
                markdown = markdown.Replace(origConfig, newConfig);

            meta.RawMarkdownBody = markdown;
            meta.MarkdownBody = meta.RawMarkdownBody.Replace(newConfig, "");

            return markdown;
        }
        

        #region INotify Property Changed

        public event PropertyChangedEventHandler PropertyChanged;

        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}