global using System;
global using System.CommandLine;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.Json.Serialization.Metadata;
global using System.Threading;
global using System.Threading.Tasks;

global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.CSharp.Syntax;
global using Microsoft.CodeAnalysis.Text;

global using Common.Contracts;
global using Common.Contracts.Models;
global using Common.Json;
global using Common.Results;
global using Common.Tools;
global using Common.CliProtocol;
global using Common.Logging;
global using McpProtocol.Contracts;
global using static Common.Constants.ConstantManager;

global using AstCli.Models;
global using AstCli.Services;
