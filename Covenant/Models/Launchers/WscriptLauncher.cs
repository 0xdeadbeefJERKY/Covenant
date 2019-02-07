﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System.Linq;
using Microsoft.CodeAnalysis;

using Covenant.Models.Listeners;

namespace Covenant.Models.Launchers
{
    public class WscriptLauncher : ScriptletLauncher
    {
        public WscriptLauncher()
        {
            this.Name = "Wscript";
            this.Type = LauncherType.Wscript;
            this.Description = "Uses wscript.exe to launch a Grunt using a COM activated Delegate and ActiveXObjects.";
            this.ScriptType = ScriptletType.Plain;
            this.OutputKind = OutputKind.DynamicallyLinkedLibrary;
        }

        protected override string GetLauncher(string code)
        {
            string launcher = "wscript" + " " + "file.js";
            this.LauncherString = launcher;
            return this.LauncherString;
        }

        public override string GetHostedLauncher(Listener listener, HostedFile hostedFile)
        {
            HttpListener httpListener = (HttpListener)listener;
            if (httpListener != null)
            {
                string launcher = "wscript" + " " + hostedFile.Path.Split('/').Last();
                this.LauncherString = launcher;
                return launcher;
            }
            else { return ""; }
        }
    }
}
