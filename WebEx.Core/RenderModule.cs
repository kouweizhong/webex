﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;

namespace WebEx.Core
{

    /// <summary>
    /// Summary description for WebExHtmlExtensions
    /// </summary>
    public static class WebExHtmlRenderModuleExtensions
    {
        public const string ModulesFolder = "webex.modules";
        public const string webexViewExtension = "webex:viewext";
        public const string webexModuleInstance = "webex:moduleInstance";
        public static string GetViewExtension(HttpApplicationStateBase app)
        {
            //var ext = app[webexViewExtension] as string;
            //if (string.IsNullOrEmpty(ext))
            //    ext = "cshtml";

            return app[webexViewExtension] as string;
        }

        private static string GetModuleFolder(IModule module)
        {
            return GetModuleFolder(module.GetType());
        }
        private static string GetModuleFolder(string moduleName)
        {
            if (moduleName.EndsWith("module", StringComparison.InvariantCultureIgnoreCase))
                moduleName = moduleName.Substring(0, moduleName.Length - 6);
            return moduleName;
        }
        private static string GetModuleFolder(Type module)
        {
            return GetModuleFolder(module.Name);
        }
        private static bool TryGetProp(object model, IModuleView view, ref string val)
        {
            if (model == null)
                return false;

            if (view == null || string.IsNullOrEmpty(view.Value) || view.Value == Contracts.DefaultView)
                return false;

            var props = from k in model.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.GetProperty)
                        where string.Equals(view.Value, k.Name, StringComparison.InvariantCultureIgnoreCase) && k.PropertyType == typeof(string)
                        select k;

            var prop = props.FirstOrDefault();

            if (prop != null)
            {
                val = prop.GetValue(model) as string;
                return true;
            }

            return false;
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, IModule module, IDictionary<string, object> args,
            IModuleView view, object model, string moduleInstanceId)
        {
            return helper.RenderModule(module, args, view, model, moduleInstanceId, null);
        }
        internal static MvcHtmlString RenderModule(this HtmlHelper helper, IModule module, IDictionary<string, object> args,
            IModuleView view, object model, string moduleInstanceId, CachedModule cm)
        {
            //string.IsNullOrEmpty(view)?(IModuleView)new DefaultView():new ModuleView(view)
            //if (module == null)
            //{
            //    return helper.RenderModuleManual(null, view, moduleModel);
            //}

            if (module != null && view != null)
            {
                var modelModule = module as IModuleWithModel;
                var mstr = view as ModuleViewString;
                if (mstr != null)
                {
                    return new MvcHtmlString(mstr.Value);
                }
                else
                {
                    if (modelModule != null && model == null)
                        model = modelModule.Model;

                    var moduleFolder = GetModuleFolder(module);
                    var r = helper.RenderModule(moduleFolder, view, cm, model, moduleInstanceId, args);
                    if (r == null)
                    {
                        var curViewPath = helper.GetViewPath();
                        if (!string.IsNullOrEmpty(curViewPath))
                        {
                            var dir = System.IO.Path.GetDirectoryName(curViewPath);
                            moduleFolder = System.IO.Path.Combine(dir, moduleFolder);
                        }
                        r = helper.RenderModule(moduleFolder, view, cm, model, moduleInstanceId, args);
                    }

                    return r;
                }
            }
            return null;
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleFolder, IModuleView view, object model = null,
            string moduleInstanceId = null, IDictionary<string, object> args = null)
        {
            return helper.RenderModule(moduleFolder, view, null, model, moduleInstanceId, args);
        }
        internal static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleFolder, IModuleView view, CachedModule cm, object model = null,
            string moduleInstanceId = null, IDictionary<string, object> args = null)
        {
            var exts = new string[] { };
            var ext = GetViewExtension(helper.ViewContext.HttpContext.Application);
            if (string.IsNullOrEmpty(ext))
                exts = new[] { "cshtml", "vbhtml" };
            else
                exts = new[] { ext.Trim('.') };

            foreach (var extension in exts)
            {
                MvcHtmlString res;
                string viewName;
                if (RenderModuleInternal(helper, moduleFolder, view, model, extension, out res, moduleInstanceId, args, out viewName, cm?.Folder))
                {
                    if (cm != null)
                        cm.Folder = System.IO.Path.GetDirectoryName(viewName);

                    var cssViewName = viewName.Replace(extension, "css." + extension);
                    if ((cm == null || cm.GetView(Contracts.CSSView, helper) == null))
                    {
                        if (helper.PartialViewExists(cssViewName, model))
                            helper.RegisterInlineModule(Contracts.CSSView, cssViewName, model);
                        else
                        {
                            var fb = view.GetFallBack();
                            if (fb != null)
                            {
                                var views = new[] { "index", "default" };
                                if (!fb.IsDefault() && !string.IsNullOrEmpty(fb.Value))
                                    views = new[] { fb.Value };

                                foreach (var v in views)
                                {
                                    cssViewName = ReplaceView(cssViewName, v);
                                    if (helper.PartialViewExists(cssViewName, model))
                                    {
                                        helper.RegisterInlineModule(Contracts.CSSView, cssViewName, model);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    var jsViewName = viewName.Replace(extension, "js." + extension);
                    if ((cm == null || cm.GetView(Contracts.JavascriptView, helper) == null))
                    {
                        if (helper.PartialViewExists(jsViewName, model))
                            helper.RegisterInlineModule(Contracts.JavascriptView, jsViewName, model);
                        else
                        {
                            var fb = view.GetFallBack();
                            if (fb != null)
                            {
                                var views = new[] { "index", "default" };
                                if (!fb.IsDefault() && !string.IsNullOrEmpty(fb.Value))
                                    views = new[] { fb.Value };

                                foreach (var v in views)
                                {
                                    jsViewName = ReplaceView(jsViewName, v);
                                    if (helper.PartialViewExists(jsViewName, model))
                                    {
                                        helper.RegisterInlineModule(Contracts.JavascriptView, jsViewName, model);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                        

                    return res;
                }
            }

            //support for regular partial views
            var hasExt = !string.IsNullOrEmpty(System.IO.Path.GetExtension(moduleFolder));
            if (hasExt) exts = new[] { System.IO.Path.GetExtension(moduleFolder).Trim('.') };
            var defViews = new[] { "index", "default" };
            foreach (var defView in defViews)
            {
                foreach (var extension in exts)
                {
                    var viewPath = moduleFolder.TrimEnd('/');
                    if (view.IsDefault())
                    {
                        viewPath += "/" + defView;
                    }
                    else if (!view.IsEmpty())
                    {
                        viewPath += "/" + view.Value;
                    }

                    if (!hasExt)
                        viewPath += "." + extension;

                    if (helper.PartialViewExists(viewPath, model))
                    {
                        if (string.IsNullOrEmpty(moduleInstanceId))
                            moduleInstanceId = Guid.NewGuid().ToString();

                        MvcHtmlString res;
                        using (new AutoCleanup(() => helper.PrepareRender(moduleInstanceId, viewPath, args), () => helper.CleanupRender(moduleInstanceId)))
                        {
                            res = helper.Partial(viewPath, model);
                        }

                        if (model == null)
                            model = helper.ViewData.Model;

                        var cssViewName = viewPath.Replace(extension, "css." + extension);
                        if (helper.PartialViewExists(cssViewName, model))
                            helper.RegisterInlineModule("css", cssViewName, model);

                        var jsViewName = viewPath.Replace(extension, "js." + extension);
                        if (helper.PartialViewExists(jsViewName, model))
                            helper.RegisterInlineModule("js", jsViewName, model);

                        return res;
                    }
                }
            }

            return null;
        }

        private static string ReplaceView(string viewName, string v)
        {
            if (!string.IsNullOrEmpty(v))
            {
                viewName = viewName.Replace('\\','/');
                var re = new Regex(@".+/(.+)(\..+\..+)$");
                var m = re.Match(viewName);
                if (m.Success)
                {
                    return viewName.Substring(0, m.Groups[1].Index) + v + m.Groups[2].Value;
                }
            }

            return viewName;
        }

        public static bool RenderModuleInternal(this HtmlHelper helper, string moduleFolder, IModuleView view, object model, string ext,
            out MvcHtmlString res, string moduleInstanceId, IDictionary<string, object> args, out string renderedViewName,
            string modulePath = null)
        {
            renderedViewName = null;
            if (string.IsNullOrEmpty(moduleInstanceId))
                moduleInstanceId = Guid.NewGuid().ToString();

            var folders = new List<string>();
            folders.Add("~/Views/" + ModulesFolder);
            var curIns = helper.GetCurrentModuleInstance();
            if (curIns != null)
                folders.Insert(0, curIns.Folder);

            foreach (var rootFolder in folders)
            {
                string mPath = modulePath;
                if (string.IsNullOrEmpty(mPath))
                {
                    mPath = string.Format("{0}/{1}", rootFolder, moduleFolder);
                }

                l1:
                string realViewName = null;
                string extView = string.Empty;
                if (view.IsDefault())
                    realViewName = "index";
                else if (view != null)
                {
                    realViewName = view.Value;
                    if (view.IsAuto())
                    {
                        extView = (view as ModuleAutoView).Ext;
                    }
                }

                if (string.IsNullOrEmpty(realViewName) || realViewName == Contracts.DefaultView)
                    realViewName = "index";

                if (!string.IsNullOrEmpty(moduleFolder))
                {
                    string val = null;

                    var viewNameInner = string.Format("{0}/{1}{3}.{2}", 
                        mPath,          //0
                        realViewName,   //1
                        ext,            //2
                        extView);       //3

                    if (helper.PartialViewExists(viewNameInner, model))
                    {
                        using (new AutoCleanup(() => helper.PrepareRender(moduleInstanceId, viewNameInner, args), () => helper.CleanupRender(moduleInstanceId)))
                        {
                            res = helper.Partial(viewNameInner, model);
                        }
                        renderedViewName = viewNameInner;

                        return true;
                    }
                    else if (view.IsDefault() || (view.IsAuto() && view != null && (string.IsNullOrEmpty(view.Value) || view.Value == Contracts.DefaultView)))
                    {
                        realViewName = "default";
                        viewNameInner = string.Format("{0}/{1}{3}.{2}",
                            mPath,          //0
                            realViewName,   //1
                            ext,            //2
                            extView);       //3
                        if (helper.PartialViewExists(viewNameInner, model))
                        {
                            using (new AutoCleanup(() => helper.PrepareRender(moduleInstanceId, viewNameInner, args), () => helper.CleanupRender(moduleInstanceId)))
                            {
                                res = helper.Partial(viewNameInner, model);
                            }
                            renderedViewName = viewNameInner;
                            return true;
                        }
                    }
                    else
                    {
                        var fbView = view.GetFallBack();
                        if (fbView != null)
                        {
                            view = fbView;
                            goto l1;
                        }
                    }

                    if (TryGetProp(model, view, ref val) && !string.IsNullOrEmpty(val))
                    {
                        res = new MvcHtmlString(val);
                        return true;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(mPath))
                    {
                        mPath = rootFolder;
                    }

                    l2:
                    var viewName = string.Format("{2}/{0}{3}.{1}", realViewName, ext, mPath, extView);
                    if (helper.PartialViewExists(viewName, model))
                    {
                        using (new AutoCleanup(() => helper.PrepareRender(moduleInstanceId, viewName, args), () => helper.CleanupRender(moduleInstanceId)))
                        {
                            res = helper.Partial(viewName, new WebExModuleNotFoundModel(moduleFolder, view, model));
                        }
                        renderedViewName = viewName;
                        return true;
                    }
                    else if (view.IsDefault())
                    {
                        realViewName = "default";
                        viewName = string.Format("{2}/{0}{3}.{1}", realViewName, ext, mPath, extView);
                        if (helper.PartialViewExists(viewName, model))
                        {
                            using (new AutoCleanup(() => helper.PrepareRender(moduleInstanceId, viewName, args), () => helper.CleanupRender(moduleInstanceId)))
                            {
                                res = helper.Partial(viewName, new WebExModuleNotFoundModel(moduleFolder, view, model));
                            }
                            renderedViewName = viewName;
                            return true;
                        }
                    }
                    else
                    {
                        var fbView = view.GetFallBack();
                        if (fbView != null)
                        {
                            view = fbView;

                            realViewName = null;
                            extView = string.Empty;
                            if (view.IsDefault())
                                realViewName = "index";
                            else if (view != null)
                            {
                                realViewName = view.Value;
                                if (view.IsAuto())
                                {
                                    extView = (view as ModuleAutoView).Ext;
                                }
                            }

                            if (string.IsNullOrEmpty(realViewName) || realViewName == Contracts.DefaultView)
                                realViewName = "index";

                            goto l2;
                        }
                    }
                }
            }

            res = null;
            return false;
        }

        public static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleName)
        {
            return helper.RenderModule(moduleName, (IDictionary<string, object>)null, null, null, false, null);
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleName, string view, object moduleModel = null, string moduleInstanceId = null)
        {
            return helper.RenderModule(moduleName, (IDictionary<string, object>)null, view, moduleModel, false, moduleInstanceId);
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleName, object moduleModel, string moduleInstanceId = null)
        {
            return helper.RenderModule(moduleName, (IDictionary<string, object>)null, null, moduleModel, false, moduleInstanceId);
        }
        public static MvcHtmlString RenderModuleArgs(this HtmlHelper helper, string moduleName, object args,
            string view = null, object moduleModel = null,
            bool ignoreCase = false, string moduleInstanceId = null)
        {
            return helper.RenderModule(moduleName, args == null ? (IDictionary<string, object>)null : new RouteValueDictionary(args),
                view, moduleModel, ignoreCase, moduleInstanceId);
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, string moduleName, IDictionary<string, object> args,
            string view = null, object moduleModel = null,
            bool ignoreCase = false, string moduleInstanceId = null)
        {
            Type mt = ModulesCatalog.GetModule(helper.ViewContext.HttpContext.Application, moduleName, ignoreCase);
            if (mt != null)
            {
                object res;
                if (mt != null && helper.GetStorage().TryGetValue(WebExModuleExtensions.MakeViewDataKey(mt), out res))
                {
                    var m = res as CachedModule;
                    if (m != null)
                    {
                        if (string.IsNullOrEmpty(view))
                            view = Contracts.DefaultView;

                        return helper.RenderModule(m.Inner, args, m.GetView(view, helper), moduleModel, moduleInstanceId, m);
                    }
                }
            }

            var r = helper.RenderModule(moduleName, new ModuleAutoView(view), null, moduleModel, moduleInstanceId, args);
            if (r == null && !moduleName.StartsWith("~", StringComparison.Ordinal))
            {
                {
                    var curViewPath = helper.GetViewPath();
                    if (!string.IsNullOrEmpty(curViewPath))
                    {
                        var dir = System.IO.Path.GetDirectoryName(curViewPath);
                        moduleName = System.IO.Path.Combine(dir, moduleName);
                    }
                }
                r = helper.RenderModule(moduleName, new ModuleAutoView(view), null, moduleModel, moduleInstanceId, args);
            }

            return r;
        }
        public static MvcHtmlString RenderModule(this HtmlHelper helper, Type module, IDictionary<string, object> args = null,
            string view = null,
            string moduleInstanceId = null)
        {
            object res;
            if (module != null && helper.GetStorage().TryGetValue(WebExModuleExtensions.MakeViewDataKey(module), out res))
            {
                var m = res as CachedModule;
                if (m != null)
                {
                    if (string.IsNullOrEmpty(view))
                        view = Contracts.DefaultView;

                    return helper.RenderModule(m.Inner, args, m.GetView(view, helper), null, moduleInstanceId, m);
                }
                //else
                //    return helper.RenderModuleManual(GetModuleFolder(module), res, view);
            }

            return null;
        }
        public static MvcHtmlString RenderModules(this HtmlHelper helper, string viewType,
            Func<IModule, int> getOrderWeight = null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in helper._GetModules().Select(it => new { module = it, view = it.GetView(viewType, helper) }).
                Where(it => it.view != null).
                OrderBy(it => getOrderWeight == null ? 0 : getOrderWeight(it.module.Inner)).
                ThenBy(it => it.module.Inner, new DependencyComparer(helper.ViewContext.HttpContext.Application)))
            {
                if (item.view.IsAuto())
                {
                    (item.view as ModuleAutoView).Ext = "." + viewType;
                }
                var r = helper.RenderModule(item.module.Inner, null, item.view, null, null, item.module);
                if (r != null)
                    sb.Append(r.ToString());
            }

            foreach (var item in helper.GetInlineModules(viewType))
            {
                var r = helper.Partial(item.Item1, item.Item2);
                if (r != null)
                    sb.Append(r.ToString());
            }
            return MvcHtmlString.Create(sb.ToString());
        }
        #region ModuleInstance
        private static void PrepareRender(this HtmlHelper helper, string moduleInstanceId, string viewPath, IDictionary<string, object> args)
        {
            var mi = helper.GetModuleInstance(moduleInstanceId, viewPath);
            mi.Params = args;
        }

        private static ModuleInstance GetModuleInstance(this HtmlHelper helper, string moduleInstanceId, string viewPath)
        {
            Stack<ModuleInstance> miList = null; object o = null;
            if (!helper.GetStorage().TryGetValue(webexModuleInstance, out o))
            {
                miList = new Stack<ModuleInstance>();
                helper.GetStorage()[webexModuleInstance] = miList;
            }
            else
                miList = o as Stack<ModuleInstance>;

            var mi = miList.FirstOrDefault((it) => it.InstanceId == moduleInstanceId);
            if (mi == null)
            {
                mi = new ModuleInstance { InstanceId = moduleInstanceId, Folder = System.IO.Path.GetDirectoryName(viewPath) };
                miList.Push(mi);
            }

            return mi;
        }
        private static void CleanupRender(this HtmlHelper helper, string moduleInstanceId)
        {
            var mi = helper.GetCurrentModuleInstance();
            if (mi != null)
            {
                var instances = helper.GetStorage()[webexModuleInstance] as Stack<ModuleInstance>;
                do
                {
                    mi = instances.Pop();
                } while (mi.InstanceId != moduleInstanceId && instances.Any());
            }
        }
        private static string GetModuleId(this HtmlHelper helper)
        {
            var mi = helper.GetCurrentModuleInstance();
            if (mi != null)
                return mi.InstanceId;

            return null;
        }
        public static ModuleInstance GetCurrentModuleInstance(this HtmlHelper helper)
        {
            Stack<ModuleInstance> miList = null; object o = null;
            if (helper.GetStorage().TryGetValue(webexModuleInstance, out o))
            {
                miList = o as Stack<ModuleInstance>;
                if (miList != null && miList.Any())
                    return miList.Peek();
            }

            return null;
        }
        public static object GetViewParam(this HtmlHelper helper, string paramName, object defaultValue = null)
        {
            var mi = helper.GetCurrentModuleInstance();
            if (mi != null && mi.Params != null)
            {
                mi.Params.TryGetValue(paramName, out defaultValue);
            }
            return defaultValue;
        }
        public static T GetViewParam<T>(this HtmlHelper helper, string paramName, T defaultValue)
        {
            T v;
            if (!helper.TryGetViewParam(paramName, out v))
            {
                v = defaultValue;
            }
            return v;
        }
        public static bool TryGetViewParam<T>(this HtmlHelper helper, string paramName, out T value)
        {
            value = default(T);
            var mi = helper.GetCurrentModuleInstance();
            if (mi != null && mi.Params != null)
            {
                object v;
                var r = mi.Params.TryGetValue(paramName, out v);
                if (r)
                {
                    value = (T)v;
                }
                return r;
            }

            return false;
        }
        #endregion

    }
}
