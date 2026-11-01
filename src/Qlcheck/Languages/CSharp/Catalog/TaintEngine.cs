// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class TaintEngine
{
    private const string Text = "System.String";

    private const string Sequence = "System.Collections.Generic.IEnumerable`1";

    public static WalkRow[] Rows { get; } =
    [
        Row.Handle("allocation-dos", CheckClass.Taint, Patterns.AllocDos),
        Row.Flow("argument-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Collections.ObjectModel.Collection`1", "Add|Insert", "item").On("System.Diagnostics.ProcessStartInfo", "ArgumentList"),
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").OfType(Sequence).NotShell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").OfType(Sequence).NotShell(),
            ],
            "Put `--` before arguments taken from input, or reject values that start with `-`, so they cannot become options.")),
        Row.Flow("command-argument-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").OfType(Text).NotShell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").OfType(Text).NotShell(),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "Arguments").NotShell(),
            ],
            "Pass each argument through `ProcessStartInfo.ArgumentList` instead of one command-line string.")),
        Row.Flow("command-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("System.Diagnostics.Process", "Start", "fileName"),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "fileName"),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "FileName"),
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").Shell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").Shell(),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "Arguments").Shell(),
            ],
            "Run a fixed program and pass the input through `ProcessStartInfo.ArgumentList`, never through a shell.")),
        Row.Flow("connection-string-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Set("System.Data.IDbConnection", "ConnectionString"),
                Sink.Ctor("System.Data.Common.DbConnection", "connectionString"),
                Sink.Set("System.Data.Common.DbConnectionStringBuilder", "ConnectionString"),
            ],
            "Set each part through a `DbConnectionStringBuilder` property, or pick the connection string from configuration by an allow-listed key.")),
        Row.Flow("deserialization-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [Sink.Call("System.Runtime.Serialization.IFormatter", "Deserialize", "serializationStream")],
            "Use a serializer that does not resolve types from the data, such as `System.Text.Json` with known types.")),
        Row.Flow("dynamic-code-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript", "EvaluateAsync|RunAsync|Create", "code"),
                Sink.Call("System.Data.DataTable", "Compute", "expression"),
                Sink.Call("System.Linq.Dynamic.Core.DynamicExpressionParser", "ParseLambda", "expression"),
                Sink.Call("System.Linq.Dynamic.Core.DynamicQueryableExtensions", "Where|OrderBy|Select", "predicate|ordering|selector"),
            ],
            "Map the input to a fixed set of expressions or operations instead of evaluating it as code.")),
        Row.Flow("filesystem-oracle", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.IO.File", "Exists", "path"),
                Sink.Call("System.IO.Directory", "Exists", "path"),
            ],
            "Check the name against an allow-list, or keep only `Path.GetFileName` of it under a fixed directory.")),
        Row.Flow("ldap-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Set("System.DirectoryServices.DirectorySearcher", "Filter"),
                Sink.Ctor("System.DirectoryServices.DirectorySearcher", "filter"),
                Sink.Ctor("System.DirectoryServices.DirectoryEntry", "path"),
                Sink.Ctor("System.DirectoryServices.Protocols.SearchRequest", "ldapFilter|distinguishedName"),
            ],
            "Escape the value for an LDAP filter (`(`, `)`, `*`, `\\` and NUL) or check it against an allow-list.")),
        Row.Taint("log-injection", CheckClass.Taint, "Information"),
        Row.Handle("loop-bound-injection", CheckClass.Taint, Patterns.LoopBound),
        Row.Taint("no-stack-trace-disclosure", CheckClass.Taint, "Write"),
        Row.Flow("nosql-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("MongoDB.Bson.BsonDocument", "Parse", "json"),
                Sink.Ctor("MongoDB.Driver.JsonFilterDefinition`1", "json"),
                Sink.Ctor("MongoDB.Driver.JsonCommand`1", "json"),
            ],
            "Build the query with `Builders<T>.Filter` instead of parsing JSON text that contains the value.")),
        Row.Taint("open-redirect", CheckClass.Taint, "Redirect"),
        Row.Flow("path-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call(
                    "System.IO.File",
                    "Append*|Create*|Copy|Move|Replace|Delete|Decrypt|Encrypt|Open*|Read*|Write*|SetAttributes|SetUnixFileMode",
                    "path|sourceFileName|destFileName|destinationFileName|destinationBackupFileName|pathToTarget"),
                Sink.Call(
                    "System.IO.Directory",
                    "CreateDirectory|CreateSymbolicLink|Delete|Enumerate*|GetFiles|GetDirectories|GetFileSystemEntries|Move|SetCurrentDirectory",
                    "path|sourceDirName|destDirName|pathToTarget"),
                Sink.Ctor("System.IO.FileStream", "path"),
                Sink.Ctor("System.IO.StreamReader", "path"),
                Sink.Ctor("System.IO.StreamWriter", "path"),
                Sink.Ctor("System.IO.FileInfo", "fileName"),
                Sink.Ctor("System.IO.DirectoryInfo", "path"),
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "PhysicalFile", "physicalPath"),
            ],
            "Resolve it with `Path.GetFullPath` and check it starts with the base directory, or keep only `Path.GetFileName` of it.")),
        Row.Taint("reflected-xss", CheckClass.Taint, "Write"),
        Row.Flow("reflection-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Type", "GetType", "typeName"),
                Sink.Call("System.Type", "GetMethod|GetProperty|GetField|GetMember|InvokeMember", "name"),
                Sink.Call("System.Reflection.Assembly", "GetType", "name"),
                Sink.Call("System.Reflection.Assembly", "Load|LoadFrom|LoadFile", "assemblyString|assemblyFile|path"),
                Sink.Call("System.Activator", "CreateInstance|CreateInstanceFrom", "assemblyName|typeName|assemblyFile"),
            ],
            "Map the input to a type or member through an allow-list instead of resolving it by name.")),
        Row.Flow("regex-dos", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Ctor("System.Text.RegularExpressions.Regex", "pattern").Without("matchTimeout"),
                Sink.Call(
                    "System.Text.RegularExpressions.Regex",
                    "IsMatch|Match|Matches|Replace|Split|Count|EnumerateMatches",
                    "pattern").Without("matchTimeout"),
            ],
            "Pass a `matchTimeout`, or escape the input with `Regex.Escape` if it is meant literally.")),
        Row.Flow("sql-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Set("System.Data.IDbCommand", "CommandText"),
                Sink.Ctor("System.Data.Common.DbCommand", "cmdText"),
                Sink.Ctor("System.Data.Common.DbDataAdapter", "selectCommandText"),
                Sink.Call("Dapper.SqlMapper", "Query*|Execute*", "sql"),
                Sink.Call("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "FromSqlRaw", "sql"),
                Sink.Call("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw*", "sql"),
            ],
            "Pass it as a `DbParameter` instead of building the SQL text from it.")),
        Row.Handle("ssrf", CheckClass.Taint, SsrfPatterns.Ssrf),
        Row.Handle("ssrf-traversal", CheckClass.Taint, SsrfPatterns.SsrfTraversal),
        Row.Flow("untrusted-environment-variable", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [Sink.Call("System.Environment", "SetEnvironmentVariable", "variable|value")],
            "Set only variables from a fixed list, with values checked against what the program expects.")),
        Row.Taint("untrusted-session-cookie", CheckClass.Taint, "Append"),
        Row.Flow("xml-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("System.Xml.XmlDocument", "LoadXml", "xml"),
                Sink.Call("System.Xml.Linq.XDocument", "Parse", "text"),
                Sink.Call("System.Xml.Linq.XElement", "Parse", "text"),
                Sink.Call("System.Xml.XmlWriter", "WriteRaw", "data"),
            ],
            "Build the document with `XElement` or `XmlWriter` calls that escape values, instead of parsing text that contains it.")),
        Row.Flow("xpath-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [
                Sink.Call("System.Xml.XmlNode", "SelectNodes|SelectSingleNode", "xpath"),
                Sink.Call("System.Xml.XPath.XPathNavigator", "Select|SelectSingleNode|Evaluate|Compile", "xpath"),
                Sink.Call("System.Xml.XPath.XPathExpression", "Compile", "xpath"),
            ],
            "Check the value against an allow-list, or pass it as an XPath variable instead of building the expression from it.")),
        Row.Flow("xslt-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.PublicParameter,
            [Sink.Call("System.Xml.Xsl.XslCompiledTransform", "Load", "stylesheetUri")],
            "Load stylesheets only from a fixed, trusted location.")),
        Row.Taint("zip-slip", CheckClass.Taint, "ExtractToFile"),
    ];

    // Every sink of every Row.Flow row: the dataflow runs once per body for all of them.
    public static IReadOnlyList<Sink> FlowSinks { get; } =
        [.. Rows.Select(row => row.Taint).OfType<TaintSpec>().SelectMany(spec => spec.Sinks).Distinct()];

    public static void Apply(WalkContext ctx, IReadOnlySet<string> selected)
    {
        foreach (var row in Rows)
        {
            if (selected.Contains(row.Id))
            {
                row.Apply(ctx);
            }
        }
    }
}
