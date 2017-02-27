namespace Sitecore.Support.Pipelines.HttpRequest
{
    using Sitecore;
    using Sitecore.Data;
    using Sitecore.Data.ItemResolvers;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.IO;
    using Sitecore.Pipelines.HttpRequest;
    using Sitecore.SecurityModel;
    using Sitecore.Sites;
    using System;
    using System.Linq;

    public class ItemResolver : HttpRequestProcessor
    {
        private ItemPathResolver pathResolver;

        public override void Process(HttpRequestArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (((Context.Item == null) && (Context.Database != null)) && (args.Url.ItemPath.Length != 0))
            {
                Profiler.StartOperation("Resolve current item.");
                string path = MainUtil.DecodeName(args.Url.ItemPath);
                Item item = args.GetItem(path);
                if (item == null)
                {
                    path = args.Url.ItemPath;
                    item = args.GetItem(path);
                }
                SiteContext site = Context.Site;
                if (!args.LocalPath.Equals("/", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (item == null)
                    {
                        path = args.LocalPath;
                        item = args.GetItem(path);
                    }
                    if (item == null)
                    {
                        path = MainUtil.DecodeName(args.LocalPath);
                        item = args.GetItem(path);
                    }
                    string str2 = (site != null) ? site.RootPath : string.Empty;
                    if (item == null)
                    {
                        path = FileUtil.MakePath(str2, args.LocalPath, '/');
                        item = args.GetItem(path);
                    }
                    if (item == null)
                    {
                        path = MainUtil.DecodeName(FileUtil.MakePath(str2, args.LocalPath, '/'));
                        item = args.GetItem(path);
                    }
                    if (item == null)
                    {
                        string name = (site != null) ? site.StartItem : string.Empty;
                        string[] strArray = new string[] { str2, MainUtil.DecodeName(str2) };
                        string[] startItemArray = new string[] { name, MainUtil.DecodeName(name) };
                        string[] localPathArray = new string[] { args.LocalPath, MainUtil.DecodeName(args.LocalPath) };
                        foreach (string str4 in (from s1 in strArray
                                                 from s2 in startItemArray
                                                 from s3 in localPathArray
                                                 select FileUtil.MakePath(FileUtil.MakePath(s1, s2, '/'), s3, '/')).Distinct<string>())
                        {
                            item = args.GetItem(str4);
                            if (item != null)
                            {
                                break;
                            }
                        }
                    }
                    if ((item == null) || item.Name.Equals("*"))
                    {
                        item = this.ResolveUsingDisplayName(args);
                    }
                }
                if (((item == null) && args.UseSiteStartPath) && (site != null))
                {
                    item = args.GetItem(site.StartPath);
                }
                if (item != null)
                {
                    Tracer.Info("Current item is \"" + path + "\".");
                }
                Context.Item = item;
                Profiler.EndOperation();
            }
        }

        private Item ResolveFullPath(HttpRequestArgs args)
        {
            return (this.ResolvePath(args.Url.ItemPath) ?? this.ResolvePath(args.LocalPath));
        }

        private Item ResolveLocalPath(HttpRequestArgs args)
        {
            SiteContext site = Context.Site;
            if (site == null)
            {
                return null;
            }
            Item root = ItemManager.GetItem(site.RootPath, Language.Current, Sitecore.Data.Version.Latest, Context.Database, SecurityCheck.Disable);
            if (root == null)
            {
                return null;
            }
            string localPath = args.LocalPath;
            return this.PathResolver.ResolveItem(localPath, root);
        }

        private Item ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || (path[0] != '/'))
            {
                return null;
            }
            int index = path.IndexOf('/', 1);
            if (index < 0)
            {
                return null;
            }
            Item root = ItemManager.GetItem(path.Substring(0, index), Language.Current, Sitecore.Data.Version.Latest, Context.Database, SecurityCheck.Disable);
            if (root == null)
            {
                return null;
            }
            string str = path.Substring(index);
            return this.PathResolver.ResolveItem(str, root);
        }

        private Item ResolveUsingDisplayName(HttpRequestArgs args)
        {
            Item item;
            Assert.ArgumentNotNull(args, "args");
            using (new SecurityDisabler())
            {
                item = this.ResolveLocalPath(args);
                if (item == null)
                {
                    item = this.ResolveFullPath(args);
                }
                if (item == null)
                {
                    return null;
                }
            }
            return args.ApplySecurity(item);
        }

        protected ItemPathResolver PathResolver
        {
            get
            {
                if (this.pathResolver == null)
                {
                    this.pathResolver = new ContentItemPathResolver();
                }
                return this.pathResolver;
            }
            set
            {
                this.pathResolver = value;
            }
        }
    }
}
