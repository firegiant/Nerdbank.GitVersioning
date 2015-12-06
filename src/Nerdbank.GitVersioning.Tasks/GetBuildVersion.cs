﻿namespace Nerdbank.GitVersioning.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Nerdbank.GitVersioning;

    public class GetBuildVersion : Task
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetBuildVersion"/> class.
        /// </summary>
        public GetBuildVersion()
        {
        }

        /// <summary>
        /// Gets the version string to use in the compiled assemblies.
        /// </summary>
        [Output]
        public string Version { get; private set; }

        /// <summary>
        /// Gets the version string to use for the <see cref="System.Reflection.AssemblyVersionAttribute"/>.
        /// </summary>
        [Output]
        public string AssemblyVersion { get; private set; }

        /// <summary>
        /// Gets the version string to use in the official release name (lacks revision number).
        /// </summary>
        [Output]
        public string SimpleVersion { get; private set; }

        /// <summary>
        /// Gets or sets the major.minor version string.
        /// </summary>
        /// <value>
        /// The x.y string (no build number or revision number).
        /// </value>
        [Output]
        public string MajorMinorVersion { get; set; }

        /// <summary>
        /// Gets or sets the prerelease version, or empty if this is a final release.
        /// </summary>
        /// <value>
        /// The prerelease version.
        /// </value>
        [Output]
        public string PrereleaseVersion { get; set; }

        /// <summary>
        /// Gets the Git revision control commit id for HEAD (the current source code version).
        /// </summary>
        [Output]
        public string GitCommitId { get; private set; }

        /// <summary>
        /// Gets the number of commits in the longest single path between
        /// the specified commit and the most distant ancestor (inclusive)
        /// that set the version to the value at HEAD.
        /// </summary>
        [Output]
        public int GitVersionHeight { get; private set; }

        /// <summary>
        /// Gets the build number (git height) for this version.
        /// </summary>
        [Output]
        public int BuildNumber { get; private set; }

        public override bool Execute()
        {
            try
            {
                Version typedVersion;
                VersionOptions versionOptions;
                using (var git = this.OpenGitRepo())
                {
                    var repoRoot = git?.Info?.WorkingDirectory;
                    var relativeRepoProjectDirectory = !string.IsNullOrWhiteSpace(repoRoot)
                        ? Environment.CurrentDirectory.Substring(repoRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        : null;

                    var commit = git?.Head.Commits.FirstOrDefault();
                    this.GitCommitId = commit?.Id.Sha ?? string.Empty;
                    this.GitVersionHeight = commit?.GetVersionHeight(relativeRepoProjectDirectory) ?? 0;

                    versionOptions =
                        VersionFile.GetVersion(commit, Environment.CurrentDirectory) ??
                        VersionFile.GetVersion(Environment.CurrentDirectory);

                    this.PrereleaseVersion = versionOptions?.Version.Prerelease ?? string.Empty;

                    // Override the typedVersion with the special build number and revision components, when available.
                    typedVersion = commit?.GetIdAsVersion(relativeRepoProjectDirectory, this.GitVersionHeight) ?? versionOptions?.Version.Version;
                }

                typedVersion = typedVersion ?? new Version();
                var typedVersionWithoutRevision = typedVersion.Build > 0
                    ? new Version(typedVersion.Major, typedVersion.Minor, typedVersion.Build)
                    : new Version(typedVersion.Major, typedVersion.Minor);
                this.SimpleVersion = typedVersionWithoutRevision.ToString();
                var majorMinorVersion = new Version(typedVersion.Major, typedVersion.Minor);
                this.MajorMinorVersion = majorMinorVersion.ToString();
                this.AssemblyVersion = (versionOptions?.AssemblyVersion ?? majorMinorVersion).ToStringSafe(4);
                this.BuildNumber = Math.Max(0, typedVersion.Build);
                this.Version = typedVersion.ToString();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }

            return true;
        }

        private LibGit2Sharp.Repository OpenGitRepo()
        {
            string repoRoot = Environment.CurrentDirectory;
            while (!Directory.Exists(Path.Combine(repoRoot, ".git")))
            {
                repoRoot = Path.GetDirectoryName(repoRoot);
                if (repoRoot == null)
                {
                    return null;
                }
            }

            return new LibGit2Sharp.Repository(repoRoot);
        }
    }
}
