using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlValidator
{
    // 1. Create a Visitor to walk the AST and find all table references
    class TableReferenceVisitor : TSqlFragmentVisitor
    {
        public List<NamedTableReference> Tables { get; } = new List<NamedTableReference>();

        public override void ExplicitVisit(NamedTableReference node)
        {
            Tables.Add(node);
            base.ExplicitVisit(node);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SqlValidator.exe <ScriptPath> <DacpacPath>");
                Environment.Exit(1);
            }

            string scriptPath = Path.GetFullPath(args[0]);
            string dacpacPath = Path.GetFullPath(args[1]);

            try
            {
                // 2. Load DACPAC to act strictly as a dictionary of valid objects
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Loading schema dictionary from {dacpacPath}...");
                var model = TSqlModel.LoadFromDacpac(dacpacPath);
                
                // Extract all valid table names (storing in lowercase for case-insensitive matching)
                var validTables = model.GetObjects(DacQueryScopes.UserDefined, ModelSchema.Table)
                                       .Select(t => t.Name.Parts.Last().ToLower())
                                       .ToHashSet();

                // 3. Parse the DML script using ScriptDom
                Console.WriteLine($"Parsing SQL script: {scriptPath}...");
                string scriptText = File.ReadAllText(scriptPath);
                var parser = new TSql160Parser(true);
                
                using (var reader = new StringReader(scriptText))
                {
                    var fragment = parser.Parse(reader, out IList<ParseError> syntaxErrors);

                    if (syntaxErrors.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nSyntax Errors Found:");
                        foreach (var error in syntaxErrors)
                        {
                            Console.WriteLine($"[Line {error.Line}, Column {error.Column}] {error.Message}");
                        }
                        Environment.Exit(1);
                    }

                    // 4. Visit the AST to find all tables used in the script
                    var visitor = new TableReferenceVisitor();
                    fragment.Accept(visitor);

                    // 5. Cross-reference the parsed tables against the DACPAC dictionary
                    Console.WriteLine("Validating DML table references...");
                    bool hasErrors = false;

                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (var tableRef in visitor.Tables)
                    {
                        string tableName = tableRef.SchemaObject.BaseIdentifier.Value.ToLower();
                        
                        if (!validTables.Contains(tableName))
                        {
                            hasErrors = true;
                            Console.WriteLine($"[Error] Table '{tableRef.SchemaObject.BaseIdentifier.Value}' does not exist in the schema. (Line: {tableRef.StartLine}, Column: {tableRef.StartColumn})");
                        }
                    }

                    if (!hasErrors)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nValidation Passed! All tables exist in the schema.");
                    }
                    else
                    {
                        Environment.ExitCode = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nCritical Error: {ex.Message}");
                Environment.Exit(1);
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}
