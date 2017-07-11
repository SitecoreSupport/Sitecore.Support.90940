// --------------------------------------------------------------------------------------------------------------------
// <copyright file="090 - ItemResolver.cs" company="Sitecore">
//   Copyright (c) Sitecore. All rights reserved.
// </copyright>
// <summary>
//   Resolves the current item from the query string path.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Support.Pipelines.HttpRequest
{
  using System.Linq;
  using Sitecore.Configuration;
  using Sitecore.Data.ItemResolvers;
  using Sitecore.Data.Items;
  using Sitecore.Data.Managers;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.IO;
  using Sitecore.Pipelines.HttpRequest;
  using Sitecore.SecurityModel;
  using Sitecore.Sites;

  /// <summary>
  /// Resolves the current item from the query string path.
  /// </summary>
  public class ItemResolver : HttpRequestProcessor
  {
    /// <summary>
    /// Item path resolver.
    /// </summary>
    private ItemPathResolver pathResolver;

    /// <summary>
    /// Gets or sets item path resolver.
    /// </summary>
    /// <value>Item path resolver.</value>
    [NotNull]
    protected ItemPathResolver PathResolver
    {
      get
      {
        if (this.pathResolver == null)
        {
          ItemPathResolver resolver = new ContentItemPathResolver();
          this.pathResolver = (Settings.ItemResolving.FindBestMatch & MixedItemNameResolvingMode.Enabled) == MixedItemNameResolvingMode.Enabled ? new MixedItemNameResolver(resolver) : resolver;
        }

        return this.pathResolver;
      }

      set
      {
        Debug.ArgumentNotNull(value, "value");

        this.pathResolver = value;
      }
    }

    #region Public methods

    /// <summary>
    /// Runs the processor.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public override void Process([NotNull] HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      if (Context.Item != null || Context.Database == null || args.Url.ItemPath.Length == 0)
      {
        return;
      }

      Profiler.StartOperation("Resolve current item.");

      // decoded path
      string path = MainUtil.DecodeName(args.Url.ItemPath);
      Item item = args.GetItem(path);

      //Check if permission is denied for requested item then show Permission Denied page  and skip checking


      // absolute path without decode
      if (item == null && !args.PermissionDenied)
      {
        path = args.Url.ItemPath;
        item = args.GetItem(path);
      }
      // rooted local path
      SiteContext site = Context.Site;

      string rootPath = site != null ? site.RootPath : string.Empty;


      // local path
      if (item == null && !args.PermissionDenied)
      {
        path = args.LocalPath;
        item = args.GetItem(path);
      }

      // encoded local path
      if (item == null && !args.PermissionDenied)
      {
        path = MainUtil.DecodeName(args.LocalPath);
        item = args.GetItem(path);
      }



      if (item == null && !args.PermissionDenied)
      {
        path = FileUtil.MakePath(rootPath, args.LocalPath, '/');
        item = args.GetItem(path);
      }

      // encoded rooted local path
      if (item == null && !args.PermissionDenied)
      {
        path = MainUtil.DecodeName(FileUtil.MakePath(rootPath, args.LocalPath, '/'));
        item = args.GetItem(path);
      }

      if (item == null && !args.PermissionDenied)
      {
        string startItem = site != null ? site.StartItem : string.Empty;

        // combinations of decoded/encoded rootPath/startItem/localPath
        var rootPathArray = new[] { rootPath, MainUtil.DecodeName(rootPath) };
        var startItemArray = new[] { startItem, MainUtil.DecodeName(startItem) };
        var localPathArray = new[] { args.LocalPath, MainUtil.DecodeName(args.LocalPath) };

        var pathList = from s1 in rootPathArray
                       from s2 in startItemArray
                       from s3 in localPathArray
                       select FileUtil.MakePath(FileUtil.MakePath(s1, s2, '/'), s3, '/');

        foreach (var pathElem in pathList.Distinct())
        {
          item = args.GetItem(pathElem);

          if (item != null)
          {
            break;
          }
        }
      }

      if ((item == null || item.Name.Equals("*")) && !args.PermissionDenied)
      {
        var displayNameItem = ResolveUsingDisplayName(args);
        if (displayNameItem != null)
        {
          item = displayNameItem;
        }
      }

      if (item == null && args.UseSiteStartPath && site != null && !args.PermissionDenied)
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

    #endregion

    #region Private methods

    /// <summary>
    /// Resolves the full path.
    /// </summary>
    /// <param name="args">The args.</param>
    /// <returns></returns>
    [CanBeNull]
    Item ResolveFullPath([NotNull] HttpRequestArgs args)
    {
      Debug.ArgumentNotNull(args, "args");

      return this.ResolvePath(args.Url.ItemPath) ?? this.ResolvePath(args.LocalPath);
    }

    /// <summary>
    /// Resolves the path based on the url eg: args.Url.ItemPath or args.LocalPath.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    Item ResolvePath(string path)
    {
      if (string.IsNullOrEmpty(path) || path[0] != '/')
      {
        return null;
      }

      int index = path.IndexOf('/', 1);

      if (index < 0)
      {
        return null;
      }

      Item root = ItemManager.GetItem(path.Substring(0, index), Language.Current, Data.Version.Latest, Context.Database, SecurityCheck.Disable);

      if (root == null)
      {
        return null;
      }

      string subPath = path.Substring(index);

      return this.PathResolver.ResolveItem(subPath, root);
    }

    /// <summary>
    /// Resolves the local path.
    /// </summary>
    /// <param name="args">The args.</param>
    /// <returns></returns>
    [CanBeNull]
    Item ResolveLocalPath([NotNull] HttpRequestArgs args)
    {
      Debug.ArgumentNotNull(args, "args");

      SiteContext site = Context.Site;

      if (site == null)
      {
        return null;
      }

      Item root = ItemManager.GetItem(site.RootPath, Language.Current, Data.Version.Latest, Context.Database, SecurityCheck.Disable);

      if (root == null)
      {
        return null;
      }

      string path = args.LocalPath;

      return this.PathResolver.ResolveItem(path, root);
    }

    /// <summary>
    /// Resolves the display name of the using.
    /// </summary>
    /// <param name="args">The args.</param>
    /// <returns></returns>
    [CanBeNull]
    Item ResolveUsingDisplayName([NotNull] HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      Item result;

      using (new SecurityDisabler())
      {
        result = ResolveLocalPath(args);

        if (result == null)
        {
          result = ResolveFullPath(args);
        }

        if (result == null)
        {
          return null;
        }
      }

      return args.ApplySecurity(result);
    }

    #endregion
  }
}