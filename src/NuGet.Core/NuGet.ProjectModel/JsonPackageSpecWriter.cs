﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    public class JsonPackageSpecWriter
    {
        public static void WritePackageSpec(PackageSpec packageSpec, string filePath)
        {
            JObject json = new JObject();
            WritePackageSpec(packageSpec, json);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var textWriter = new StreamWriter(fileStream))
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        json.WriteTo(jsonWriter);
                    }
                }
            }
        }

        public static void WritePackageSpec(PackageSpec packageSpec, JObject json)
        {
            SetValue(json, "title", packageSpec.Title);

            if (!packageSpec.IsDefaultVersion)
            {
                SetValue(json, "version", packageSpec.Version?.ToNormalizedString());
            }

            SetValue(json, "description", packageSpec.Description);
            SetArrayValue(json, "authors", packageSpec.Authors);
            SetValue(json, "copyright", packageSpec.Copyright);
            SetValue(json, "language", packageSpec.Language);
            SetArrayValue(json, "contentFiles", packageSpec.ContentFiles);
            SetDictionaryValue(json, "packInclude", packageSpec.PackInclude);
            SetPackOptions(json, packageSpec);
            SetMSBuildMetadata(json, packageSpec);
            SetDictionaryValues(json, "scripts", packageSpec.Scripts);

            if (packageSpec.Dependencies.Any())
            {
                SetDependencies(json, packageSpec.Dependencies);
            }

            if (packageSpec.Tools.Any())
            {
                JObject tools = new JObject();
                foreach (var tool in packageSpec.Tools)
                {
                    JObject toolObject = new JObject();
                    toolObject["version"] = tool.LibraryRange.VersionRange.ToNormalizedString();

                    if (tool.Imports.Any())
                    {
                        SetImports(toolObject, tool.Imports);
                    }
                    tools[tool.LibraryRange.Name] = toolObject;
                }

                SetValue(json, "tools", tools);
            }

            if (packageSpec.TargetFrameworks.Any())
            {
                JObject frameworks = new JObject();
                foreach (var framework in packageSpec.TargetFrameworks)
                {
                    JObject frameworkObject = new JObject();

                    SetDependencies(frameworkObject, framework.Dependencies);
                    SetImports(frameworkObject, framework.Imports);

                    frameworks[framework.FrameworkName.GetShortFolderName()] = frameworkObject;
                }                

                SetValue(json, "frameworks", frameworks);
            }

            JsonRuntimeFormat.WriteRuntimeGraph(json, packageSpec.RuntimeGraph);
        }

        private static void SetMSBuildMetadata(JObject json, PackageSpec packageSpec)
        {
            var msbuildMetadata = packageSpec.MSBuildMetadata;
            if (msbuildMetadata == null)
            {
                return;
            }

            var rawMSBuildMetadata = new JObject();
            SetValue(rawMSBuildMetadata, "projectUniqueName", msbuildMetadata.ProjectUniqueName);
            SetValue(rawMSBuildMetadata, "projectName", msbuildMetadata.ProjectName);
            SetValue(rawMSBuildMetadata, "projectPath", msbuildMetadata.ProjectPath);
            SetValue(rawMSBuildMetadata, "projectJsonPath", msbuildMetadata.ProjectJsonPath);
            SetValue(rawMSBuildMetadata, "outputPath", msbuildMetadata.OutputPath);

            if (msbuildMetadata.OutputType != RestoreOutputType.Unknown)
            {
                SetValue(rawMSBuildMetadata, "outputType", msbuildMetadata.OutputType.ToString());
            }

            SetValue(rawMSBuildMetadata, "packagesPath", msbuildMetadata.PackagesPath);


            SetArrayValue(rawMSBuildMetadata, "fallbackFolders", msbuildMetadata.FallbackFolders);

            if (msbuildMetadata.Sources?.Count > 0)
            {
                var sourcesObj = new JObject();
                rawMSBuildMetadata["sources"] = sourcesObj;

                foreach (var source in msbuildMetadata.Sources)
                {
                    // "source": {}
                    sourcesObj[source.Source] = new JObject();
                }
            }

            if (msbuildMetadata.ProjectReferences?.Count > 0)
            {
                var projectReferencesObj = new JObject();
                rawMSBuildMetadata["projectReferences"] = projectReferencesObj;

                foreach (var project in msbuildMetadata.ProjectReferences)
                {
                    projectReferencesObj[project.ProjectUniqueName] = new JObject();
                    projectReferencesObj[project.ProjectUniqueName]["projectPath"] = project.ProjectPath;
                }
            }

            if (rawMSBuildMetadata.Count > 0)
            {
                SetValue(json, JsonPackageSpecReader.MSBuild, rawMSBuildMetadata);
            }
        }

        private static void SetPackOptions(JObject json, PackageSpec packageSpec)
        {
            var packOptions = packageSpec.PackOptions;
            if (packOptions == null)
            {
                return;
            }

            var rawPackOptions = new JObject();
            SetArrayValue(rawPackOptions, "owners", packageSpec.Owners);
            SetArrayValue(rawPackOptions, "tags", packageSpec.Tags);
            SetValue(rawPackOptions, "projectUrl", packageSpec.ProjectUrl);
            SetValue(rawPackOptions, "iconUrl", packageSpec.IconUrl);
            SetValue(rawPackOptions, "summary", packageSpec.Summary);
            SetValue(rawPackOptions, "releaseNotes", packageSpec.ReleaseNotes);
            SetValue(rawPackOptions, "licenseUrl", packageSpec.LicenseUrl);
            SetValue(rawPackOptions, "requireLicenseAcceptance", packageSpec.RequireLicenseAcceptance.ToString());
            if (packOptions.PackageType != null)
            {
                if (packOptions.PackageType.Count == 1)
                {
                    SetValue(rawPackOptions, JsonPackageSpecReader.PackageType, packOptions.PackageType[0].Name);
                }
                else if (packOptions.PackageType.Count > 1)
                {
                    var packageTypeNames = packOptions.PackageType.Select(p => p.Name);
                    SetArrayValue(rawPackOptions, JsonPackageSpecReader.PackageType, packageTypeNames);
                }
            }

            if (rawPackOptions.Count > 0)
            {
                SetValue(json, JsonPackageSpecReader.PackOptions, rawPackOptions);
            }
        }

        private static void SetDependencies(JObject json, IList<LibraryDependency> libraryDependencies)
        {
            JObject dependencies = new JObject();
            JObject frameworkAssemblies = new JObject();
            foreach (var dependency in libraryDependencies)
            {
                JObject dependencyObject = new JObject();

                SetValue(dependencyObject, "include", dependency.IncludeType.ToString());
                if (!dependency.LibraryRange.VersionRange.Equals(new Versioning.VersionRange()))
                {
                    SetValue(dependencyObject, "version", dependency.LibraryRange.VersionRange.ToNormalizedString());
                }
                SetValue(dependencyObject, "suppressParent", dependency.SuppressParent.ToString());
                SetValue(dependencyObject, "type", dependency.Type.ToString());

                if (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference)
                {
                    SetValue(dependencyObject, "target", dependency.LibraryRange.TypeConstraint.ToString());
                    dependencies[dependency.Name] = dependencyObject;
                }
                else
                {
                    frameworkAssemblies[dependency.Name] = dependencyObject;
                }
            }

            if (dependencies.HasValues)
            {
                SetValue(json, "dependencies", dependencies);
            }
            if (frameworkAssemblies.HasValues)
            {
                SetValue(json, "frameworkAssemblies", frameworkAssemblies);
            }
        }

        private static void SetImports(JObject json, IReadOnlyList<NuGetFramework> frameworks)
        {
            if (frameworks.Any())
            {
                JArray imports = new JArray();
                foreach (var import in frameworks)
                {
                    imports.Add(import.Profile);
                }
                json["imports"] = imports;
            }
        }

        private static void SetValue(JObject json, string name, string value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetValue(JObject json, string name, JObject value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetArrayValue(JObject json, string name, IEnumerable<string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JArray(values);
            }
        }

        private static void SetDictionaryValue(JObject json, string name, IDictionary<string, string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = pair.Value;
                }
            }
        }

        private static void SetDictionaryValues(JObject json, string name, IDictionary<string, IEnumerable<string>> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = new JArray(pair.Value);
                }
            }
        }
    }
}
