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
        Row.Flow("allocation-dos", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.Numeric,
            [
                Sink.ArraySize(),
                Sink.Ctor("System.Collections.Generic.List`1", "capacity"),
                Sink.Ctor("System.Collections.Generic.Dictionary`2", "capacity"),
                Sink.Ctor("System.Collections.Generic.HashSet`1", "capacity"),
                Sink.Ctor("System.Text.StringBuilder", "capacity"),
                Sink.Ctor("System.String", "count"),
                Sink.Call("System.Buffers.ArrayPool`1", "Rent", "minimumLength"),
                Sink.Call("System.Linq.Enumerable", "Repeat|Range", "count"),
            ],
            "Cap the size with a constant first (`Math.Min(size, Max)` or an early return).")),
        Row.Flow("argument-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Collections.ObjectModel.Collection`1", "Add|Insert", "item").On("System.Diagnostics.ProcessStartInfo", "ArgumentList"),
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").OfType(Sequence).NotShell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").OfType(Sequence).NotShell(),
            ],
            "Put `--` before arguments taken from input, or reject values that start with `-`, so they cannot become options.")),
        Row.Flow("command-argument-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").OfType(Text).NotShell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").OfType(Text).NotShell(),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "Arguments").NotShell(),
            ],
            "Pass each argument through `ProcessStartInfo.ArgumentList` instead of one command-line string.")
        {
            PublicParameters = true,
        }),
        Row.Flow("command-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Diagnostics.Process", "Start", "fileName"),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "fileName"),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "FileName"),
                Sink.Call("System.Diagnostics.Process", "Start", "arguments").Shell(),
                Sink.Ctor("System.Diagnostics.ProcessStartInfo", "arguments").Shell(),
                Sink.Set("System.Diagnostics.ProcessStartInfo", "Arguments").Shell(),
            ],
            "Run a fixed program and pass the input through `ProcessStartInfo.ArgumentList`, never through a shell.")
        {
            PublicParameters = true,
        }),
        Row.Flow("connection-string-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Set("System.Data.IDbConnection", "ConnectionString"),
                Sink.Ctor("System.Data.Common.DbConnection", "connectionString"),
                Sink.Set("System.Data.Common.DbConnectionStringBuilder", "ConnectionString"),
            ],
            "Set each part through a `DbConnectionStringBuilder` property, or pick the connection string from configuration by an allow-listed key.")),
        Row.Flow("deserialization-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [Sink.Call("System.Runtime.Serialization.IFormatter", "Deserialize", "serializationStream")],
            "Use a serializer that does not resolve types from the data, such as `System.Text.Json` with known types.")
        {
            PublicParameters = true,
        }),
        Row.Flow("dynamic-code-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript", "EvaluateAsync|RunAsync|Create", "code"),
                Sink.Call("System.Data.DataTable", "Compute", "expression"),
                Sink.Call("System.Linq.Dynamic.Core.DynamicExpressionParser", "ParseLambda", "expression"),
                Sink.Call("System.Linq.Dynamic.Core.DynamicQueryableExtensions", "Where|OrderBy|Select", "predicate|ordering|selector"),
            ],
            "Map the input to a fixed set of expressions or operations instead of evaluating it as code.")
        {
            PublicParameters = true,
        }),
        Row.Flow("filesystem-oracle", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.IO.File", "Exists", "path"),
                Sink.Call("System.IO.Directory", "Exists", "path"),
            ],
            "Check the name against an allow-list, or keep only `Path.GetFileName` of it under a fixed directory.")),
        Row.Flow("ldap-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Set("System.DirectoryServices.DirectorySearcher", "Filter"),
                Sink.Ctor("System.DirectoryServices.DirectorySearcher", "filter"),
                Sink.Ctor("System.DirectoryServices.DirectoryEntry", "path"),
                Sink.Ctor("System.DirectoryServices.Protocols.SearchRequest", "ldapFilter|distinguishedName"),
            ],
            "Escape the value for an LDAP filter (`(`, `)`, `*`, `\\` and NUL) or check it against an allow-list.")
        {
            PublicParameters = true,
        }),
        Row.Flow("log-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("Microsoft.Extensions.Logging.LoggerExtensions", "Log*", "message"),
                Sink.Call("Serilog.ILogger", "Verbose|Debug|Information|Warning|Error|Fatal|Write", "messageTemplate"),
                Sink.Call("Serilog.Log", "Verbose|Debug|Information|Warning|Error|Fatal|Write", "messageTemplate"),
            ],
            "Keep the message template constant and pass the value as a structured argument: `LogInformation(\"User {Name}\", name)`.")),
        Row.Flow("loop-bound-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted | TaintLabel.Numeric,
            [Sink.LoopBound()],
            "Cap the bound with a constant first (`Math.Min(count, Max)` or an early return).")),
        Row.Flow("no-stack-trace-disclosure", CheckClass.Taint, new TaintSpec(
            TaintLabel.ExceptionDetail,
            [
                Sink.Call("Microsoft.AspNetCore.Http.HttpResponseWritingExtensions", "WriteAsync", "text"),
                Sink.Set("Microsoft.AspNetCore.Mvc.ContentResult", "Content"),
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Content", "content"),
                Sink.Ctor("Microsoft.AspNetCore.Html.HtmlString", "value"),
                Sink.Call("Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper", "Raw", "value"),
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Ok|BadRequest|NotFound|Conflict|UnprocessableEntity|StatusCode", "value|error"),
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Problem", "detail"),
                Sink.Ctor("Microsoft.AspNetCore.Mvc.ObjectResult", "value"),
            ],
            "Log the exception and send the client a generic message or a problem id instead.")),
        Row.Flow("nosql-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("MongoDB.Bson.BsonDocument", "Parse", "json"),
                Sink.Ctor("MongoDB.Driver.JsonFilterDefinition`1", "json"),
                Sink.Ctor("MongoDB.Driver.JsonCommand`1", "json"),
            ],
            "Build the query with `Builders<T>.Filter` instead of parsing JSON text that contains the value.")
        {
            PublicParameters = true,
        }),
        Row.Flow("open-redirect", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Redirect*", "url"),
                Sink.Call("Microsoft.AspNetCore.Http.HttpResponse", "Redirect", "location"),
                Sink.Ctor("Microsoft.AspNetCore.Mvc.RedirectResult", "url"),
                Sink.Call("Microsoft.AspNetCore.Http.Results", "Redirect", "url"),
                Sink.Call("Microsoft.AspNetCore.Http.TypedResults", "Redirect", "url"),
            ],
            "Use `LocalRedirect`, or check the target with `Url.IsLocalUrl` first.")),
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
        Row.Flow("reflected-xss", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("Microsoft.AspNetCore.Http.HttpResponseWritingExtensions", "WriteAsync", "text"),
                Sink.Set("Microsoft.AspNetCore.Mvc.ContentResult", "Content"),
                Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Content", "content"),
                Sink.Ctor("Microsoft.AspNetCore.Html.HtmlString", "value"),
                Sink.Call("Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper", "Raw", "value"),
            ],
            "Encode it for HTML (`HtmlEncoder.Default.Encode`) or render it through a view that encodes by default.")),
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
            TaintLabel.Untrusted,
            [
                Sink.Ctor("System.Text.RegularExpressions.Regex", "pattern").Without("matchTimeout"),
                Sink.Call(
                    "System.Text.RegularExpressions.Regex",
                    "IsMatch|Match|Matches|Replace|Split|Count|EnumerateMatches",
                    "pattern").Without("matchTimeout"),
            ],
            "Pass a `matchTimeout`, or escape the input with `Regex.Escape` if it is meant literally.")
        {
            PublicParameters = true,
        }),
        Row.Flow("sql-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Set("System.Data.IDbCommand", "CommandText"),
                Sink.Ctor("System.Data.Common.DbCommand", "cmdText"),
                Sink.Ctor("System.Data.Common.DbDataAdapter", "selectCommandText"),
                Sink.Call("Dapper.SqlMapper", "Query*|Execute*", "sql"),
                Sink.Call("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "FromSqlRaw", "sql"),
                Sink.Call("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw*", "sql"),
            ],
            "Pass it as a `DbParameter` instead of building the SQL text from it.")
        {
            PublicParameters = true,
        }),
        Row.Handle("ssrf", CheckClass.Taint, SsrfPatterns.Ssrf),
        Row.Handle("ssrf-traversal", CheckClass.Taint, SsrfPatterns.SsrfTraversal),
        Row.Flow("untrusted-environment-variable", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [Sink.Call("System.Environment", "SetEnvironmentVariable", "variable|value")],
            "Set only variables from a fixed list, with values checked against what the program expects.")
        {
            PublicParameters = true,
        }),
        Row.Flow("untrusted-session-cookie", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [Sink.Call("Microsoft.AspNetCore.Http.IResponseCookies", "Append", "key|value")],
            "Set cookies only from values the server chose, such as a new session id, never from request input.")),
        Row.Flow("xml-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Xml.XmlDocument", "LoadXml", "xml"),
                Sink.Call("System.Xml.Linq.XDocument", "Parse", "text"),
                Sink.Call("System.Xml.Linq.XElement", "Parse", "text"),
                Sink.Call("System.Xml.XmlWriter", "WriteRaw", "data"),
            ],
            "Build the document with `XElement` or `XmlWriter` calls that escape values, instead of parsing text that contains it.")
        {
            PublicParameters = true,
        }),
        Row.Flow("xpath-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [
                Sink.Call("System.Xml.XmlNode", "SelectNodes|SelectSingleNode", "xpath"),
                Sink.Call("System.Xml.XPath.XPathNavigator", "Select|SelectSingleNode|Evaluate|Compile", "xpath"),
                Sink.Call("System.Xml.XPath.XPathExpression", "Compile", "xpath"),
            ],
            "Check the value against an allow-list, or pass it as an XPath variable instead of building the expression from it.")
        {
            PublicParameters = true,
        }),
        Row.Flow("xslt-injection", CheckClass.Taint, new TaintSpec(
            TaintLabel.Untrusted,
            [Sink.Call("System.Xml.Xsl.XslCompiledTransform", "Load", "stylesheetUri")],
            "Load stylesheets only from a fixed, trusted location.")
        {
            PublicParameters = true,
        }),
        Row.Flow("zip-slip", CheckClass.Taint, new TaintSpec(
            TaintLabel.ArchiveEntry,
            [
                Sink.Call("System.IO.Compression.ZipFileExtensions", "ExtractToFile", "destinationFileName"),
                Sink.Call("System.IO.File", "Create*|Open*|Write*|Copy|Move", "path|destFileName"),
                Sink.Ctor("System.IO.FileStream", "path"),
            ],
            "Resolve the target with `Path.GetFullPath` and check it starts with the extraction directory before writing.")),
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
