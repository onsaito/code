using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.Model;

namespace SqlValidator
{
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Loading DACPAC model from {dacpacPath}...");
                
                var loadOptions = new ModelLoadOptions { LoadAsScriptBackedModel = true };
                using (var model = TSqlModel.LoadFromDacpac(dacpacPath, loadOptions))
                {
                    Console.WriteLine("Injecting SQL script for validation...");
                    string scriptText = File.ReadAllText(scriptPath);
                    model.AddOrUpdateObjects(scriptText, "TargetScript.sql", new TSqlObjectOptions());

                    Console.WriteLine("Validating against schema...");
                    var messages = model.Validate();

                    var errors = messages.Where(m => m.MessageType == DacMessageType.Error).ToList();
                    var warnings = messages.Where(m => m.MessageType == DacMessageType.Warning).ToList();

                    if (errors.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nValidation Failed ({errors.Count} errors):");
                        foreach (var e in errors)
                            Console.WriteLine($"[{e.Prefix}{e.Number}] {e.Message} (Line: {e.Line})");
                        
                        Environment.ExitCode = 1;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nValidation Passed!");
                    }

                    if (warnings.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\nWarnings ({warnings.Count}):");
                        foreach (var w in warnings)
                            Console.WriteLine($"[{w.Prefix}{w.Number}] {w.Message} (Line: {w.Line})");
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
