﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Compilation;
using System.Web.Hosting;

namespace WebEx.Core
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple =false)]
    public class ModuleContainerAttribute : Attribute
    {

    }
    /// <summary>
    /// Catalog of modules
    /// </summary>
    public static class ModulesCatalog
    {
        public const string _webexInternalModuleAliases = "webex:modulealiases";
        public const string _webexInternalModuleTypes = "webex:modules";

        public static Type[] GetModulesByAttribute()
        {
            return GetModules("*.dll", true);
        }
        public static Type[] GetModules(string assemblyPattern = "*webexmodule*.dll", bool checkAttribute = false)
        {
            var r = new List<Type>();

            if (BuildManager.CodeAssemblies != null)
            foreach (var ass in BuildManager.CodeAssemblies.OfType<Assembly>())
            {
                foreach (var type in ass.GetTypes())
                {
                    if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract)
                        r.Add(type);
                }
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var file in Directory.GetFiles(GetAssemblyDir(), assemblyPattern))
            {
                try
                {
                    var ass = Assembly.Load(File.ReadAllBytes(file));
                    if (ass != null)
                    {
                        var lass = loadedAssemblies.FirstOrDefault(la => la.FullName == ass.FullName);
                        if (lass != null) ass = lass;

                        if (checkAttribute)
                        {
                            if (ass.GetCustomAttribute<ModuleContainerAttribute>() == null)
                                continue;
                        }

                        foreach (var type in ass.GetTypes())
                        {
                            if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract)
                                r.Add(type);
                        }
                    }
                }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning disable CS0168 // Variable is declared but never used
                catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
                {

                }
            }

            return r.ToArray();
        }
        private static string GetAssemblyDir()
        {
            return HostingEnvironment.IsHosted
                ? HttpRuntime.BinDirectory
                : Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
        }

        public static void RegisterModules(HttpApplicationState appState, string viewExtension = null)
        {
            RegisterModules(appState, GetModules(), viewExtension);

        }
        public static void RegisterModules(HttpApplicationState appState, Type[] modules, string viewExtension = null)
        {
            appState[WebExHtmlRenderModuleExtensions.webexViewExtension] = viewExtension;
            appState[_webexInternalModuleTypes] = modules;
            foreach (var type in modules)
            {
                string alias = GetModuleName(type);
                appState[MakeAliasViewDataKey(alias)] = type.AssemblyQualifiedName;
            }
        }

        public static string GetModuleName(Type type)
        {
            var attr = type.GetCustomAttribute<ModuleAliasAttribute>();
            string alias = null;
            if (attr != null)
                alias = attr.Alias;

            if (string.IsNullOrEmpty(alias))
            {
                alias = type.Name;
                if (alias.EndsWith("module", StringComparison.InvariantCultureIgnoreCase))
                    alias = alias.Substring(0, alias.Length - 6);
            }

            return alias;
        }

        public static void SetModuleAlias(HttpApplicationState appState, Type module, string alias)
        {
            if (module == null || string.IsNullOrEmpty(alias))
                return;

            appState[MakeAliasViewDataKey(alias)] = module.AssemblyQualifiedName;

        }
        public static string MakeAliasViewDataKey(string alias)
        {
            return string.Format("{0}:{1}", _webexInternalModuleAliases, alias);
        }

        public static Type GetModule(HttpApplicationStateBase appState, string moduleName, bool ignoreCase = false)
        {
            object qname = null;
            if (appState != null)
            {
                if (appState.AllKeys.Contains(MakeAliasViewDataKey(moduleName)))
                {
                    qname = appState[MakeAliasViewDataKey(moduleName)];
                }
            }
            else
            {
                qname = moduleName;
            }

            if (qname != null)
                return Type.GetType(qname.ToString(), false, ignoreCase);

            return null;
        }
        public static void LoadModules(HttpApplicationStateBase appState, IDictionary storage, params object[] args)
        {
            var modules = appState[ModulesCatalog._webexInternalModuleTypes] as IEnumerable<Type>;
            if (modules != null)
                foreach (var module in modules)
                {
                    LoadModule(storage, module, args);
                }
        }
        public static IEnumerable<T> LoadModules<T>(HttpApplicationStateBase appState, IDictionary storage, params object[] args)
        {
            var modules = appState[ModulesCatalog._webexInternalModuleTypes] as IEnumerable<Type>;
            var l = new List<T>();
            if (modules != null)
                foreach (var module in modules)
                {
                    if (typeof(T).IsAssignableFrom(module))
                        l.Add((T)LoadModule(storage, module, args));
                }

            return l;
        }
        public static IModule LoadModule(IDictionary storage, Type type, params object[] args)
        {
            if (type == null)
                return null;

            var r = type.CreateInstance(null, args) as IModule;

            if (r != null)
            {
                WebExModuleExtensions.RegisterModule(storage, r);
            }

            return r;
        }

    }
}