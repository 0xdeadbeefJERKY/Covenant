﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

using Covenant.Core;

namespace Covenant.Models.Grunts
{
    public class GruntTasking
    {
        public enum GruntTaskingStatus
        {
            Uninitialized,
            Tasked,
            Progressed,
            Completed
        }

        public enum GruntTaskingType
        {
            Assembly,
            Set,
            Kill
        }

        public class GruntTaskingMessage
        {
            public GruntTaskingType type { get; set; }
            public string Name { get; set; }
            public string message { get; set; }
        }

        public int Id { get; set; }
        public string Name { get; set; } = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
        public int TaskId { get; set; }
        public int GruntId { get; set; }

        public GruntTaskingStatus status { get; set; } = GruntTaskingStatus.Uninitialized;
        public GruntTaskingType type { get; set; } = GruntTaskingType.Assembly;
        public string GruntTaskOutput { get; set; } = "";

        public GruntTaskingMessage TaskingMessage
        {
            get
            {
                if (this.type == GruntTaskingType.Assembly)
                {
                    return new GruntTaskingMessage
                    {
                        type = GruntTaskingType.Assembly,
                        Name = this.Name,
                        message = this.GruntTaskingAssembly
                    };
                }
                else if(this.type == GruntTaskingType.Set)
                {
                    string message = "";
                    switch (SetType)
                    {
                        case GruntSetTaskingType.Delay: message = GruntSetTaskingType.Delay + "," + Value; break;
                        case GruntSetTaskingType.Jitter: message = GruntSetTaskingType.Jitter + "," + Value; break;
                        case GruntSetTaskingType.ConnectAttempts: message = GruntSetTaskingType.ConnectAttempts + "," + Value; break;
                        default: message = ""; break;
                    }
                    return new GruntTaskingMessage
                    {
                        type = GruntTaskingType.Set,
                        Name = this.Name,
                        message = message
                    };
                }
                else if (this.type == GruntTaskingType.Kill)
                {
                    return new GruntTaskingMessage
                    {
                        type = GruntTaskingType.Kill,
                        Name = this.Name,
                        message = "kill"
                    };
                }
                return null;
            }
        }

        // Base64-encoded compressed task assembly bytes
        public string GruntTaskingAssembly { get; private set; } = "";
        public string Compile(string TaskCode, List<string> Parameters, List<string> ReferenceAssemblies, Common.DotNetVersion dotNetFrameworkVersion)
        {
            List<Compiler.Reference> references = Common.SharpSploitReferences;
            references.AddRange(Common.DefaultReferences);
            byte[] compiled = Compiler.Compile(new Compiler.CompilationRequest
            {
                Source = TaskCode,
                SourceDirectory = Common.CovenantSrcDirectory,
                ResourceDirectory = Common.CovenantResourceDirectory,
                ReferenceDirectory = Common.CovenantReferenceDirectory,
                TargetDotNetVersion = dotNetFrameworkVersion,
                References = references,
                EmbeddedResources = Common.SharpSploitEmbeddedResources,
                Confuse = true
            });

            this.GruntTaskingAssembly = Convert.ToBase64String(Utilities.Compress(compiled));
            foreach(string Parameter in Parameters)
            {
                this.GruntTaskingAssembly += "," + Convert.ToBase64String(Common.CovenantEncoding.GetBytes(Parameter));
            }
            return this.GruntTaskingAssembly;
        }

        public enum GruntSetTaskingType
        {
            Delay,
            Jitter,
            ConnectAttempts
        }
        public GruntSetTaskingType SetType { get; set; } = GruntSetTaskingType.Delay;
        public string Value { get; set; } = "";
    }
}
