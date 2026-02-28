/* The MIT License (MIT)
*
* Copyright (c) 2015 Marc Clifton
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

using Clifton.Core.Assertions;
using Clifton.Core.Exceptions;
using Clifton.Core.Semantics;
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable CheckNamespace

namespace Clifton.Core.ModuleManagement
{
    public class ModuleManager : IModuleManager
    {
        protected List<IModule> registrants;

        public ReadOnlyCollection<IModule> Modules => registrants.AsReadOnly();

        /// <summary>
        /// Register modules specified in a list of assembly filenames.
        /// </summary>
        public virtual void RegisterModules(List<AssemblyFileName> moduleFilenames, OptionalPath optionalPath = null, Func<string, Assembly> assemblyResolver = null)
        {
            var modules = LoadModules(moduleFilenames, optionalPath, assemblyResolver);
            var regs = InstantiateRegistrants(modules);
            InitializeRegistrants(regs);
        }

        public virtual void RegisterModulesFrom(List<AssemblyFileName> moduleFilenames, string path, Func<string, Assembly> assemblyResolver = null)
        {
            var modules = LoadModulesFrom(moduleFilenames, path, assemblyResolver);
            var regs = InstantiateRegistrants(modules);
            InitializeRegistrants(regs);
        }

        /// <summary>
        /// Load the assemblies and return the list of loaded assemblies.  In order to register
        /// services that the module implements, we have to load the assembly.
        /// </summary>
        protected virtual List<Assembly> LoadModules(List<AssemblyFileName> moduleFilenames, OptionalPath optionalPath, Func<string, Assembly> assemblyResolver)
        {
            var modules = new List<Assembly>();

            moduleFilenames.ForEach(a =>
            {
                var assembly = LoadAssembly(a, optionalPath, assemblyResolver);
                modules.Add(assembly);
            });

            return modules;
        }

        protected virtual List<Assembly> LoadModulesFrom(List<AssemblyFileName> moduleFilenames, string path, Func<string, Assembly> assemblyResolver = null)
        {
            var modules = new List<Assembly>();

            moduleFilenames.ForEach(a =>
            {
                var assembly = LoadAssemblyFrom(a, path, assemblyResolver);
                modules.Add(assembly);
            });

            return modules;
        }

        /// <summary>
        /// Load and return an assembly given the assembly filename so we can proceed with
        /// instantiating the module and so the module can register its services.
        /// </summary>
        protected virtual Assembly LoadAssembly(AssemblyFileName assyName, OptionalPath optionalPath, Func<string, Assembly> assemblyResolver)
        {
            var fullPath = GetFullPath(assyName, optionalPath);
            Assembly assembly;

            if (!File.Exists(fullPath.Value))
            {
                Assert.Not(assemblyResolver == null, "Module " + fullPath.Value + " not found.\r\n.  An assemblyResolver must be defined when attempting to load modules from the application's resources or specify the optionalPath to locate the assembly.");
                assembly = assemblyResolver?.Invoke(assyName.Value);
            }
            else
            {
                try
                {
                    assembly = Assembly.LoadFile(fullPath.Value);
                }
                catch (Exception ex)
                {
                    throw new ModuleManagerException("Unable to load module " + assyName.Value + ": " + ex.Message);
                }
            }

            return assembly;
        }

        protected virtual Assembly LoadAssemblyFrom(AssemblyFileName assyName, string path, Func<string, Assembly> assemblyResolver = null)
        {
            var fullPath = Path.Combine(path, assyName.Value);
            Assembly assembly;

            if (!File.Exists(fullPath))
            {
                throw new ApplicationException( "Module " + fullPath + " not found.\r\n.");
                // Assert.Not(assemblyResolver == null, "Module " + fullPath + " not found.\r\n.  An assemblyResolver must be defined when attempting to load modules from the application's resources or specify the optionalPath to locate the assembly.");
                // assembly = assemblyResolver(assyName.Value);
            }
            else
            {
                try
                {
                    assembly = Assembly.LoadFile(fullPath);
                }
                catch (Exception ex)
                {
                    throw new ModuleManagerException("Unable to load module " + assyName.Value + ": " + ex.Message);
                }
            }

            return assembly;
        }

        /// <summary>
        /// Instantiate and return the list of registratants -- assemblies with classes that implement IModule.
        /// The registrants is one and only one class in the module that implements IModule, which we can then
        /// use to call the Initialize method so the module can register its services.
        /// </summary>
        protected virtual List<IModule> InstantiateRegistrants(List<Assembly> modules)
        {
            registrants = new List<IModule>();
            modules.ForEach(m =>
            {
                var registrant = InstantiateRegistrant(m);
                registrants.Add(registrant);
            });

            return registrants;
        }

        /// <summary>
        /// Instantiate a registrant.  A registrant must have one and only one class that implements IModule.
        /// The registrant is one and only one class in the module that implements IModule, which we can then
        /// use to call the Initialize method so the module can register its services.
        /// </summary>
        protected virtual IModule InstantiateRegistrant(Assembly module)
        {
            var classesImplementingInterface = module.GetTypes().
                    Where(t => t.IsClass).
                    Where(c => c.GetInterfaces().Any(i => i.Name == "IModule"));

            var implementingInterface = classesImplementingInterface.ToList();
            Assert.That(implementingInterface.Count() <= 1, "Module can only have one class that implements IModule");
            Assert.That(implementingInterface.Count() != 0, "Module does not have any classes that implement IModule");

            var implementor = implementingInterface.Single();
            var instance = Activator.CreateInstance(implementor) as IModule;

            return instance;
        }

        /// <summary>
        /// Initialize each registrant. This method should be overridden by your application needs.
        /// </summary>
        protected virtual void InitializeRegistrants(List<IModule> registrants)
        {
        }

        /// <summary>
        /// Return the full path of the executing application (here we assume that ModuleManager.dll is in that path) and concatenate the assembly name of the module.
        /// .NET requires the the full path in order to load the associated assembly.
        /// </summary>
        protected virtual FullPath GetFullPath(AssemblyFileName assemblyName, OptionalPath optionalPath)
        {
            var appLocation = AppContext.BaseDirectory;

            if (optionalPath != null)
            {
                appLocation = Path.Combine(appLocation, optionalPath.Value);
            }

            var fullPath = Path.Combine(appLocation ?? "", assemblyName.Value);

            return FullPath.Create(fullPath);
        }
    }
}
